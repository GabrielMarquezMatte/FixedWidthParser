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

## CMake

    python python/scripts/build_native.py
    cmake -S cpp -B build/cpp
    cmake --build build/cpp && ctest --test-dir build/cpp --output-on-failure

`add_subdirectory(cpp)` gives the `fw::fixedwidth` target; `FIXEDWIDTH_NATIVE_LIB` points at the published
library (by default the copy in `python/src/fixedwidth/_lib`). On Windows an import library is generated from
`include/fixedwidth.def` with `lib.exe` or `dlltool`, and `fixedwidth_copy_native_library(<target>)` copies
the DLL next to an executable. GCC's `-Wextra` warns about designated initializers that leave defaulted
options out; that is the intended use, so build with `-Wno-missing-field-initializers`.
