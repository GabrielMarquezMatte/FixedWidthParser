#include "common.hpp"

#include <chrono>
#include <cstdio>
#include <iterator>
#include <optional>
#include <ranges>
#include <string>
#include <vector>

using namespace std::chrono;

// Maps part of the COTAHIST-like content in common.hpp: the record type and gaps stay unmapped.
struct Quote
{
    std::string ticker;
    double open = 0;
    std::optional<int64_t> volume;
};

template <>
struct fw::FixedWidthMapper<Quote>
{
    static constexpr auto get_bindings()
    {
        return std::tuple{
            fw::make_field("codneg", 2, 12, &Quote::ticker),
            fw::make_field("preabe", 14, 13, &Quote::open, {.scale = 2, .padding = '0'}),
            fw::make_field("quatot", 27, 18, &Quote::volume, {.padding = '0'}),
        };
    }
};

// Every other column type.
struct Trade
{
    std::string code;
    int32_t quantity = 0;
    int64_t cents = 0;
    sys_days day;
    std::optional<hh_mm_ss<microseconds>> time;
    system_clock::time_point at;

    // hh_mm_ss has no operator==.
    bool operator==(const Trade& other) const
    {
        auto duration = [](const std::optional<hh_mm_ss<microseconds>>& t) { return t.transform([](auto v) { return v.to_duration(); }); };
        return code == other.code && quantity == other.quantity && cents == other.cents && day == other.day &&
               duration(time) == duration(other.time) && at == other.at;
    }
};

template <>
struct fw::FixedWidthMapper<Trade>
{
    static constexpr auto get_bindings()
    {
        return std::tuple{
            fw::make_field("code", 0, 6, &Trade::code),
            fw::make_field("quantity", 6, 5, &Trade::quantity),
            fw::make_field("cents", 11, 9, &Trade::cents, {.scale = 2}),
            fw::make_field("day", 20, 8, &Trade::day, {.format = "yyyyMMdd"}),
            fw::make_field("time", 28, 6, &Trade::time, {.format = "HHmmss"}),
            fw::make_field("at", 34, 14, &Trade::at, {.format = "yyyyMMddHHmmss"}),
        };
    }
};

struct Small
{
    int8_t value = 0;
};

template <>
struct fw::FixedWidthMapper<Small>
{
    static constexpr auto get_bindings() { return std::tuple{fw::make_field("value", 0, 4, &Small::value)}; }
};

static_assert(fw::mapped_range<std::vector<Trade>>);
static_assert(!fw::mapped_range<std::vector<int>>);
static_assert(std::ranges::random_access_range<fw::TableView<Quote>>);

static void check_quotes(const fw::TableView<Quote>& quotes)
{
    CHECK(quotes.size() == 3);
    const Quote first = quotes[0];
    CHECK(first.ticker == "PETR4" && first.open == 1234.56 && first.volume == 1000);
    CHECK(quotes[1].ticker == "VALE3" && !quotes[1].volume.has_value());
    CHECK(quotes.at(2)->open == 43.21 && quotes.at(2)->volume == 50);
    CHECK(!quotes.at(3).has_value());
    std::vector<std::string> tickers;
    for (const Quote& quote : quotes) {
        tickers.push_back(quote.ticker);
    }
    CHECK((tickers == std::vector<std::string>{"PETR4", "VALE3", "ITUB4"}));
    CHECK(std::ranges::distance(quotes) == 3 && (*(quotes.end() - 1)).ticker == "ITUB4");
}

int main()
{
    auto quotes = fw::read_memory<Quote>(content, skip_header_and_invalid());
    CHECK(quotes.has_value());
    check_quotes(*quotes);
    CHECK(quotes->table().lines_skipped() == 3);

    auto parallel = fw::read_memory<Quote>(content, skip_header_and_invalid(4));
    CHECK(parallel.has_value());
    check_quotes(*parallel);

    auto reader = fw::TypedReader<Quote>::open_memory(content, {.read = skip_header_and_invalid(), .batch_rows = 2});
    CHECK(reader.has_value());
    std::vector<std::string> batched;
    while (true) {
        auto batch = reader->next();
        CHECK(batch.has_value());
        if (!batch->has_value()) {
            break;
        }
        for (const Quote& quote : **batch) {
            batched.push_back(quote.ticker);
        }
    }
    CHECK((batched == std::vector<std::string>{"PETR4", "VALE3", "ITUB4"}));

    auto failed = fw::read_memory<Quote>(content, {.header_lines = 1});
    CHECK(!failed.has_value() && failed.error().status == FW_PARSE_ERROR && failed.error().line_number == 5);

    const std::vector<Trade> trades{
        {"PETR4", 100, 123456, sys_days(2023y / October / 24), hh_mm_ss<microseconds>(10h + 30min + 5s),
         sys_days(2023y / October / 24) + 10h + 30min + 5s},
        {"VALE3", -7, -5, sys_days(1999y / December / 31), std::nullopt, sys_days(2000y / January / 1)},
    };
    auto written = fw::write_memory(trades);
    CHECK(written.has_value());
    const std::string text(written->begin(), written->end());
    // The decimal's two decimals are implied; the empty time is blank.
    CHECK(text == "PETR4   100   1234562023102410300520231024103005\n"
                  "VALE3    -7       -519991231      20000101000000\n");

    auto path = std::string("fw_cpp_mapped.txt");
    CHECK(fw::write_file(path, trades).has_value());
    auto from_file = fw::read_file<Trade>(path, {.degree_of_parallelism = 1});
    std::remove(path.c_str());
    CHECK(from_file.has_value());
    CHECK(from_file->size() == 2);
    CHECK((*from_file)[0] == trades[0] && (*from_file)[1] == trades[1]);

    auto too_big = fw::read_memory<Small>("  12\n 300\n");
    CHECK(!too_big.has_value() && too_big.error().status == FW_INVALID_ARGUMENT);
    CHECK(too_big.error().message.find("'value' holds 300 at row 1") != std::string::npos);

    std::puts("mapped ok");
    return 0;
}
