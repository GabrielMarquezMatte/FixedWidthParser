#include "common.hpp"

#include <cstdio>
#include <sstream>
#include <string>

int main()
{
    auto layout = cotahist_layout();
    auto table = fw::read_memory(content, layout, skip_header_and_invalid());
    CHECK(table.has_value());

    auto serial = fw::write_memory(*table, layout, {.degree_of_parallelism = 1});
    auto parallel = fw::write_memory(*table, layout, {.degree_of_parallelism = 4, .batch_rows = 1});
    CHECK(serial.has_value() && parallel.has_value());
    CHECK(*serial == *parallel);
    const std::string expected =
        "01PETR4       0000000123456000000000000001000\n"
        "01VALE3       0000000067890                  \n"
        "01ITUB4       0000000004321000000000000000050\n";
    CHECK(std::string(serial->begin(), serial->end()) == expected);

    auto crlf = fw::write_memory(*table, layout, {.newline = "\r\n"});
    CHECK(crlf.has_value() && crlf->size() == expected.size() + 3);

    std::ostringstream out;
    CHECK(fw::write_stream(out, *table, layout, {.degree_of_parallelism = 4, .batch_rows = 1}).has_value());
    CHECK(out.str() == std::string(serial->begin(), serial->end()));

    // failbit alone stops a stream's writes too, so it is an error, as badbit is.
    std::ostringstream failed;
    failed.setstate(std::ios::failbit);
    auto refused = fw::write_stream(failed, *table, layout);
    CHECK(!refused.has_value() && refused.error().message.find("std::ostream") != std::string::npos);
    CHECK(failed.str().empty());
    std::ostream unbuffered{nullptr};
    CHECK(!fw::write_stream(unbuffered, *table, layout).has_value());

    const std::string path = "fw_cpp_write.txt";
    CHECK(fw::write_file(path, *table, layout).has_value());
    std::FILE* file = std::fopen(path.c_str(), "rb");
    CHECK(file != nullptr);
    std::string written(expected.size(), '\0');
    CHECK(std::fread(written.data(), 1, written.size(), file) == written.size());
    std::fclose(file);
    std::remove(path.c_str());
    CHECK(written == expected);

    std::vector<fw::ColumnSpec> narrow{{.name = "tipreg", .start = 0, .length = 2, .type = fw::Type::String}};
    auto mismatch = fw::write_memory(*table, *fw::Layout::create(narrow));
    CHECK(!mismatch.has_value() && mismatch.error().status == FW_INVALID_ARGUMENT);

    std::puts("write ok");
    return 0;
}
