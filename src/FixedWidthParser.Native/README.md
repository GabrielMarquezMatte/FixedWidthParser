# FixedWidthParser.Native

NativeAOT shared library exposing FixedWidthParser over a C ABI, so Python, C and C++ can read and write
fixed-width files without a .NET runtime.

- ABI reference and full contract: `include/fixedwidth.h`, plus `include/fixedwidth_arrow.h` for the
  Arrow C Data Interface reads
- Python binding: `python/`; C++ wrapper: `include/fixedwidth.hpp` and `cpp/`

## Build

    dotnet publish src/FixedWidthParser.Native/FixedWidthParser.Native.csproj -c Release -r <rid>

Output lands in `bin/Release/net10.0/<rid>/publish/FixedWidthParser.Native.{dll,so,dylib}`. On x64 the
library needs AVX2 (`x86-64-v3`).

## Layout

| File | Role |
|---|---|
| `*Api.cs`, `Layouts/`, `Reading/`, `Writing/`, `Arrow/` | Span-based implementation. This is what the tests drive. |
| `Exports*.cs` | `[UnmanagedCallersOnly]` pointer wrappers, one partial per feature. Keep logic out of here: it cannot be called from managed code. |
| `NativeHandleTable.cs` | Maps the opaque handles callers see onto layouts and readers. A handle resolves only as its own kind and is never reissued after it is freed. |
| `NativeApi.cs` | The calling thread's last error (`fw_last_error`, `fw_last_parse_error`). |
| `include/fixedwidth.h`, `include/fixedwidth_arrow.h` | Hand-written C headers; keep them in sync with `Exports*.cs`. |

Every export catches everything and returns a status (`FW_OK` is 0, failures are negative). Every options
struct starts with `struct_size`; `NULL` means all defaults.

## Exports

| Area | Functions |
|---|---|
| Errors | `fw_abi_version`, `fw_last_error`, `fw_last_parse_error` |
| Layouts | `fw_layout_create`, `fw_layout_free` |
| Columnar reads | `fw_read_file`, `fw_read_memory`, `fw_free_table` |
| Batches | `fw_reader_open_file`, `fw_reader_open_memory`, `fw_reader_next`, `fw_reader_close` |
| Arrow | `fw_read_arrow_file`, `fw_read_arrow_memory`, `fw_read_arrow_stream_file`, `fw_read_arrow_stream_memory` |
| Writes | `fw_write_file`, `fw_write_memory`, `fw_free_buffer` |

`include/fixedwidth.def` lists them for import-library generation on Windows; a test fails when it drifts
from the `[UnmanagedCallersOnly]` entry points.

## Consuming from C

Check `fw_abi_version()` against `FW_ABI_VERSION` first. NativeAOT ships no import library on Windows: load
the library dynamically (`LoadLibraryA`/`dlopen`, as `tests/FixedWidthParser.NativeSmoke` does) or generate
one from `include/fixedwidth.def` (`cpp/cmake/NativeLib.cmake` does this for CMake).

```c
fw_column_spec columns[2] = {0};
columns[0] = (fw_column_spec){ .struct_size = sizeof(fw_column_spec), .name = (const uint8_t*)"codneg",
                               .name_len = 6, .start = 12, .length = 12, .type = FW_T_STRING };
columns[1] = (fw_column_spec){ .struct_size = sizeof(fw_column_spec), .name = (const uint8_t*)"preabe",
                               .name_len = 6, .start = 56, .length = 13, .type = FW_T_FLOAT64, .scale = 2 };
fw_layout* layout;
fw_layout_create(columns, 2, NULL, &layout);

fw_read_options options = { .struct_size = sizeof(fw_read_options), .header_lines = 1, .skip_invalid = 1 };
fw_table table;
if (fw_read_file((const uint8_t*)path, (int32_t)strlen(path), layout, &options, &table, NULL) == FW_OK) {
    const double* preabe = table.columns[1].values;
    /* ... */
    fw_free_table(&table);
}
fw_layout_free(layout);
```
