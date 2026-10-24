# FixedWidthParser.NativeSmoke

The reference C consumer of `FixedWidthParser.Native`. `smoke.c` loads the published library dynamically
(`LoadLibraryA`/`dlopen`: NativeAOT ships no import library on Windows), pins the offset and size of every
ABI struct with static asserts, and calls every export: layouts, whole and parallel reads, batches, Arrow
arrays and streams, writes, parse errors, and `NULL`/`struct_size`/freed-handle misuse.

    dotnet publish src/FixedWidthParser.Native -c Release -r <rid>
    cmake -S tests/FixedWidthParser.NativeSmoke -B build/nativesmoke -DFIXEDWIDTH_LIB_PATH=<published library>
    cmake --build build/nativesmoke --config Release
    ctest --test-dir build/nativesmoke --output-on-failure

Without `-DFIXEDWIDTH_LIB_PATH`, it uses the copy `python/scripts/build_native.py` puts in
`python/src/fixedwidth/_lib/`. A failing static assert means `include/fixedwidth.h` and the managed structs
disagree.
