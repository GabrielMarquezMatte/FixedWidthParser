#include "common.hpp"

#include <cstdio>
#include <string>

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
