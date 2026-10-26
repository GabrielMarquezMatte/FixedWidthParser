#include "common.hpp"

#include <cstdio>
#include <sstream>
#include <string>
#include <vector>

static void check_table(const fw::Table& table)
{
    CHECK(table.row_count() == 3);
    CHECK(table.column_count() == 4);
    CHECK(table.string(1, 0) == "PETR4");
    CHECK(table.string(1, 2) == "ITUB4");
    auto prices = table.values<double>(2);
    CHECK(prices.size() == 3 && prices[0] == 1234.56 && prices[2] == 43.21);
    auto quantities = table.values<int64_t>(3);
    CHECK(quantities[0] == 1000 && quantities[2] == 50);
    CHECK(table.is_valid(3, 0) && !table.is_valid(3, 1));
    CHECK(table.is_valid(2, 1));
}

// The codes of every batch read from `in`, checking each read succeeds.
static std::vector<std::string> streamed_codes(std::istream& in, const fw::Layout& layout)
{
    auto streamed = fw::Reader::open_stream(in, layout, {.read = skip_header_and_invalid(), .batch_rows = 2});
    CHECK(streamed.has_value());
    std::vector<std::string> codes;
    while (true) {
        auto batch = streamed->next();
        CHECK(batch.has_value());
        if (!batch->has_value()) {
            return codes;
        }
        for (int64_t row = 0; row < (*batch)->row_count(); ++row) {
            codes.emplace_back((*batch)->string(1, row));
        }
    }
}

int main()
{
    auto layout = cotahist_layout();

    auto memory = fw::read_memory(content, layout, skip_header_and_invalid());
    CHECK(memory.has_value());
    check_table(*memory);
    CHECK(memory->lines_read() == 3 && memory->lines_skipped() == 3);

    auto parallel = fw::read_memory(content, layout, skip_header_and_invalid(4));
    CHECK(parallel.has_value());
    check_table(*parallel);

    const std::string path = "fw_cpp_read.txt";
    std::FILE* file = std::fopen(path.c_str(), "wb");
    CHECK(file != nullptr);
    std::fwrite(content.data(), 1, content.size(), file);
    std::fclose(file);
    auto from_file = fw::read_file(path, layout, skip_header_and_invalid());
    CHECK(from_file.has_value());
    check_table(*from_file);
    std::remove(path.c_str());

    auto failed = fw::read_memory(content, layout, {.header_lines = 1});
    CHECK(!failed.has_value());
    CHECK(failed.error().status == FW_PARSE_ERROR);
    CHECK(failed.error().line_number == 5 && failed.error().column == 2);
    CHECK(failed.error().message.find("ABCDE") != std::string::npos);

    auto reader = fw::Reader::open_memory(content, layout, {.read = skip_header_and_invalid(), .batch_rows = 2});
    CHECK(reader.has_value());
    int64_t rows = 0;
    while (true) {
        auto batch = reader->next();
        CHECK(batch.has_value());
        if (!batch->has_value()) {
            break;
        }
        rows += (*batch)->row_count();
    }
    CHECK(rows == 3);

    // The same batches from a std::istream, read forward without seeking.
    std::istringstream in{std::string(content)};
    CHECK((streamed_codes(in, layout) == std::vector<std::string>{"PETR4", "VALE3", "ITUB4"}));

    // A stream with exceptions enabled throws at its end too; the bytes before it still count.
    std::istringstream throwing{std::string(content)};
    throwing.exceptions(std::ios::failbit | std::ios::badbit);
    CHECK((streamed_codes(throwing, layout) == std::vector<std::string>{"PETR4", "VALE3", "ITUB4"}));

    // A stream that fails before its end is an error, not an empty file.
    std::istringstream failing{std::string(content)};
    failing.setstate(std::ios::failbit);
    auto refused = fw::Reader::open_stream(failing, layout, {.read = skip_header_and_invalid()});
    CHECK(!refused.has_value() || !refused->next().has_value());

    std::vector<fw::ColumnSpec> overlap{
        {.name = "a", .start = 0, .length = 4, .type = fw::Type::String},
        {.name = "b", .start = 2, .length = 4, .type = fw::Type::String},
    };
    auto bad = fw::Layout::create(overlap);
    CHECK(!bad.has_value() && bad.error().status == FW_INVALID_ARGUMENT);
    CHECK(bad.error().message.find("Overlapping") != std::string::npos);

    std::puts("read ok");
    return 0;
}
