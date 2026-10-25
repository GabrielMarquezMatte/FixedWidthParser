# FixedWidthParser for C++

`fixedwidth.hpp` (and `fixedwidth_arrow.hpp`) wrap the C ABI in C++23: RAII `Layout`, `Table`, `Reader`,
`ArrowResult` and `ArrowStream`, options structs with defaults, and `std::expected<T, fw::Error>` results.

```cpp
#include <fixedwidth.hpp>

std::vector<fw::ColumnSpec> columns{
    {.name = "codneg", .start = 12, .length = 12, .type = fw::Type::String},
    {.name = "preabe", .start = 56, .length = 13, .type = fw::Type::Float64, .scale = 2},
};
auto layout = fw::Layout::create(columns).value();
auto table = fw::read_file("COTAHIST_A2023.TXT", layout, {.header_lines = 1, .skip_invalid = true});
if (!table) { std::puts(table.error().message.c_str()); return 1; }
std::span<const double> prices = table->values<double>(1);
```

## Structs

Specialize `fw::FixedWidthMapper<T>` once, as with ExcelReader's `ExcelMapper`, and the layout comes from the
bindings: `read_file<T>`, `read_memory<T>` and `TypedReader<T>` read rows as `T`, and `write_file`/`write_memory`
write a range of them.

```cpp
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
            fw::make_field("codneg", 12, 12, &Quote::ticker),
            fw::make_field("preabe", 56, 13, &Quote::open, {.scale = 2}),
            fw::make_field("quatot", 152, 18, &Quote::volume),
        };
    }
};

auto quotes = fw::read_file<Quote>("COTAHIST_A2023.TXT", {.header_lines = 1, .skip_invalid = true});
for (const Quote& quote : *quotes) { /* ... */ }
auto bytes = fw::write_memory(std::vector<Quote>{/* ... */});
```

A field's type picks the column type: `std::string` or `std::string_view` (String), an integer (Int64, or
Decimal holding the unscaled value when the field sets a `scale`), `float` or `double` (Float64),
`std::chrono::sys_days` or `year_month_day` (Date), `std::chrono::microseconds` or `hh_mm_ss<microseconds>`
(Time), `std::chrono::system_clock::time_point` (Timestamp). `std::optional<T>` makes the column nullable; a
null read leaves a plain field at its default. `make_field`'s last argument, `fw::FieldOptions`, takes the
rest of `ColumnSpec`: `scale`, `format`, `trim_char`, `trim_mode`, `alignment`, `padding`, `overflow`.

`TableView<T>` keeps the library's columns and converts a row on access; a `std::string_view` field points
into it. `view[i]` is unchecked, `view.at(i)` returns an empty optional out of range, and `view.table()` is
the `fw::Table` underneath, with `lines_read()` and `lines_skipped()`. `fw::TypedReader<T>::open_file` or
`open_memory` reads in batches: `next()` returns the next `TableView<T>`, or an empty optional at the end.
`fw::layout_for<T>()` returns the `fw::Layout` the bindings describe, for the untyped API.

An integer field narrower than `int64_t` that a value does not fit fails the read with
`FW_INVALID_ARGUMENT` instead of wrapping.

## CMake

    python python/scripts/build_native.py
    cmake -S cpp -B build/cpp
    cmake --build build/cpp && ctest --test-dir build/cpp --output-on-failure

`add_subdirectory(cpp)` gives the `fw::fixedwidth` target; `FIXEDWIDTH_NATIVE_LIB` points at the published
library (by default the copy in `python/src/fixedwidth/_lib`). On Windows an import library is generated from
`include/fixedwidth.def` with `lib.exe` or `dlltool`, and `fixedwidth_copy_native_library(<target>)` copies
the DLL next to an executable. GCC's `-Wextra` warns about designated initializers that leave defaulted
options out; that is the intended use, so build with `-Wno-missing-field-initializers`.
