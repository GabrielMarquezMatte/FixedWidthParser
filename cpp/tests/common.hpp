#pragma once

#include <fixedwidth.hpp>

#include <cstdio>
#include <cstdlib>
#include <string_view>
#include <vector>

#define CHECK(cond)                                                              \
    do {                                                                         \
        if (!(cond)) {                                                           \
            std::fprintf(stderr, "%s:%d: check failed: %s\n", __FILE__, __LINE__, #cond); \
            std::exit(1);                                                        \
        }                                                                        \
    } while (0)

// A COTAHIST-like file: a header, three quotes (one with a blank quantity), an invalid line, a trailer.
inline constexpr std::string_view content =
    "00COTAHIST.2023\n"
    "01PETR4       0000000123456000000000000001000\n"
    "01VALE3       0000000067890                  \n"
    "01ITUB4       0000000004321000000000000000050\n"
    "01XXXXX       00000000ABCDE000000000000000001\n"
    "99TRAILER\n";

inline fw::Layout cotahist_layout()
{
    std::vector<fw::ColumnSpec> columns{
        {.name = "tipreg", .start = 0, .length = 2, .type = fw::Type::String},
        {.name = "codneg", .start = 2, .length = 12, .type = fw::Type::String},
        {.name = "preabe", .start = 14, .length = 13, .type = fw::Type::Float64, .scale = 2, .padding = '0'},
        {.name = "quatot", .start = 27, .length = 18, .type = fw::Type::Int64, .nullable = true, .padding = '0'},
    };
    auto layout = fw::Layout::create(columns);
    CHECK(layout.has_value());
    return std::move(*layout);
}

inline fw::ReadOptions skip_header_and_invalid(int32_t degree = 1)
{
    return {.header_lines = 1, .skip_invalid = true, .degree_of_parallelism = degree};
}
