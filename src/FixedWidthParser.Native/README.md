# FixedWidthParser.Native

NativeAOT shared library exposing FixedWidthParser over a C ABI, so Python, C and C++ can read and write
fixed-width files without a .NET runtime.

- ABI reference and full contract: `include/fixedwidth.h`
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
| `include/fixedwidth.h` | Hand-written C header; keep it in sync with `Exports*.cs`. |

Every export catches everything and returns a status (`FW_OK` is 0, failures are negative). Every options
struct starts with `struct_size`; `NULL` means all defaults.
