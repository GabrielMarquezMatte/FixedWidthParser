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

## Install

Needs a C++23 compiler and CMake 3.16 or later; on Windows, `lib.exe` (run CMake from a Visual Studio developer
prompt) or MinGW's `dlltool`. No .NET SDK: CMake downloads the prebuilt native library for your platform from
the GitHub Release of the tag you build (Windows x64, macOS arm64/x64, Linux glibc x64/arm64).

**FetchContent**, the simplest:

```cmake
include(FetchContent)
FetchContent_Declare(fixedwidth
    GIT_REPOSITORY https://github.com/GabrielMarquezMatte/FixedWidthParser.git
    GIT_TAG v2.0.0
    SOURCE_SUBDIR cpp)
FetchContent_MakeAvailable(fixedwidth)

target_link_libraries(app PRIVATE fw::fixedwidth)
fixedwidth_copy_native_library(app)   # Windows: puts the DLL next to app.exe; a no-op elsewhere
```

**Installed package**, for `find_package`:

    git clone --branch v2.0.0 https://github.com/GabrielMarquezMatte/FixedWidthParser.git
    cmake -S FixedWidthParser/cpp -B build -DCMAKE_BUILD_TYPE=Release
    cmake --install build --prefix <prefix>

```cmake
find_package(fixedwidth 2 REQUIRED)      # with <prefix> in CMAKE_PREFIX_PATH
target_link_libraries(app PRIVATE fw::fixedwidth)
fixedwidth_copy_native_library(app)
```

The install puts the headers in `include/`, the library in `bin/` (Windows) or `lib/`, the Windows import
library in `lib/`, and the package in `lib/cmake/fixedwidth`. `find_package` never downloads anything.

Settings:

- `FIXEDWIDTH_VERSION`: the release tag whose library is downloaded. Empty takes the tag checked out, which
  `GIT_TAG` and `--branch` above set.
- `FIXEDWIDTH_NATIVE_LIB` (CMake or environment variable): a library you built or downloaded yourself, such as
  musl's `fixedwidth-native-linux-musl-x64.so` from the release, saved as `FixedWidthParser.Native.so`. Nothing
  is downloaded then.

On Windows an import library is generated from `include/fixedwidth.def`. GCC's `-Wextra` warns about designated
initializers that leave defaulted options out; that is the intended use, so build with
`-Wno-missing-field-initializers`.

## Developing

    python python/scripts/build_native.py
    cmake -S cpp -B build/cpp
    cmake --build build/cpp && ctest --test-dir build/cpp --output-on-failure

In a checkout, the library `build_native.py` publishes into `python/src/fixedwidth/_lib` takes precedence over
the download. `FIXEDWIDTH_BUILD_TESTS` and `FIXEDWIDTH_INSTALL` default to on when `cpp/` is the top-level
project and off under FetchContent or `add_subdirectory`. `cpp/tests/package` builds against an installed prefix,
as CI does.
