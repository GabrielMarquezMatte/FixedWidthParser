# Builds and runs smoke.c against the published FixedWidthParser.Native shared library. The library is
# loaded dynamically (see smoke.c), so nothing links against it at build time.
#
# Usage, from the repository root, after `python python/scripts/build_native.py`:
#     cmake -S tests/FixedWidthParser.NativeSmoke -B build/nativesmoke
#     cmake --build build/nativesmoke --config Release
#     ctest --test-dir build/nativesmoke --output-on-failure
cmake_minimum_required(VERSION 3.16)
project(FixedWidthParserNativeSmoke C)

set(FIXEDWIDTH_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../..")

if(WIN32)
    set(_lib_name "FixedWidthParser.Native.dll")
elseif(APPLE)
    set(_lib_name "FixedWidthParser.Native.dylib")
else()
    set(_lib_name "FixedWidthParser.Native.so")
endif()

# build_native.py publishes the library and copies it to this OS-normalized path for the Python package.
set(FIXEDWIDTH_LIB_PATH "${FIXEDWIDTH_ROOT}/python/src/fixedwidth/_lib/${_lib_name}"
    CACHE FILEPATH "Path to the published FixedWidthParser.Native shared library")

add_executable(fw_smoke smoke.c)
target_include_directories(fw_smoke PRIVATE "${FIXEDWIDTH_ROOT}/src/FixedWidthParser.Native/include")
target_compile_definitions(fw_smoke PRIVATE FIXEDWIDTH_LIB_PATH_DEFAULT="${FIXEDWIDTH_LIB_PATH}")
if(MSVC)
    # C4152: the smoke test casts GetProcAddress results to function pointers on purpose.
    target_compile_options(fw_smoke PRIVATE /W4 /WX /wd4152)
else()
    target_compile_options(fw_smoke PRIVATE -Wall -Wextra -Werror)
endif()
if(NOT WIN32)
    target_link_libraries(fw_smoke PRIVATE ${CMAKE_DL_LIBS})
endif()

enable_testing()
add_test(NAME fw_smoke COMMAND fw_smoke "${FIXEDWIDTH_LIB_PATH}" WORKING_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}")
set_tests_properties(fw_smoke PROPERTIES PASS_REGULAR_EXPRESSION "smoke ok")
