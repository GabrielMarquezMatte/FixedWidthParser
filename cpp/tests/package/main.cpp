#include <fixedwidth.hpp>

#include <cstdio>
#include <string_view>
#include <vector>

int main()
{
    std::vector<fw::ColumnSpec> columns{
        {.name = "code", .start = 0, .length = 5, .type = fw::Type::String},
        {.name = "price", .start = 5, .length = 8, .type = fw::Type::Float64, .scale = 2},
    };
    auto layout = fw::Layout::create(columns);
    if (!layout)
    {
        std::puts(layout.error().message.c_str());
        return 1;
    }
    auto table = fw::read_memory(std::string_view{"PETR400001234\nVALE300005678\n"}, *layout);
    if (!table)
    {
        std::puts(table.error().message.c_str());
        return 1;
    }
    if (table->row_count() != 2 || table->string(0, 1) != "VALE3" || table->values<double>(1)[0] != 12.34)
    {
        std::puts("unexpected values");
        return 1;
    }
    std::puts("package ok");
    return 0;
}
