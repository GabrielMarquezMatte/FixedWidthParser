# The C++ wrapper: a header-only INTERFACE target, fw::fixedwidth, linked against the published
# FixedWidthParser.Native library.
#
#     python python/scripts/build_native.py
#     cmake -S cpp -B build/cpp -DFIXEDWIDTH_NATIVE_LIB=<published library>
#     cmake --build build/cpp && ctest --test-dir build/cpp --output-on-failure
cmake_minimum_required(VERSION 3.16)
project(FixedWidthParser LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(_include_dir "${CMAKE_CURRENT_SOURCE_DIR}/../src/FixedWidthParser.Native/include")
if(WIN32)
    set(_lib_name "FixedWidthParser.Native.dll")
elseif(APPLE)
    set(_lib_name "FixedWidthParser.Native.dylib")
else()
    set(_lib_name "FixedWidthParser.Native.so")
endif()
set(FIXEDWIDTH_NATIVE_LIB "${CMAKE_CURRENT_SOURCE_DIR}/../python/src/fixedwidth/_lib/${_lib_name}"
    CACHE FILEPATH "Path to the published FixedWidthParser.Native shared library")
if(NOT EXISTS "${FIXEDWIDTH_NATIVE_LIB}")
    message(FATAL_ERROR "FixedWidthParser.Native not found at ${FIXEDWIDTH_NATIVE_LIB}; run python/scripts/build_native.py or set FIXEDWIDTH_NATIVE_LIB")
endif()

include("${CMAKE_CURRENT_SOURCE_DIR}/cmake/NativeLib.cmake")
fixedwidth_import_native_lib("${FIXEDWIDTH_NATIVE_LIB}" "${_include_dir}/fixedwidth.def")

add_library(fw_fixedwidth INTERFACE)
target_include_directories(fw_fixedwidth INTERFACE "${_include_dir}")
target_compile_features(fw_fixedwidth INTERFACE cxx_std_23)
target_link_libraries(fw_fixedwidth INTERFACE fw::native)
add_library(fw::fixedwidth ALIAS fw_fixedwidth)

if(CMAKE_SOURCE_DIR STREQUAL PROJECT_SOURCE_DIR)
    enable_testing()
    add_subdirectory(tests)
endif()
