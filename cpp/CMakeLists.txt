# The C++ wrapper: a header-only INTERFACE target, fw::fixedwidth, linked against the published
# FixedWidthParser.Native library. See cpp/README.md for using it from another project.
#
#     cmake -S cpp -B build/cpp
#     cmake --build build/cpp && ctest --test-dir build/cpp --output-on-failure
#     cmake --install build/cpp --prefix <prefix>      # then find_package(fixedwidth)
cmake_minimum_required(VERSION 3.16)
project(FixedWidthParser LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(FIXEDWIDTH_NATIVE_LIB "" CACHE FILEPATH
    "FixedWidthParser.Native shared library to link; empty uses the repo's published copy or downloads the release's")
set(FIXEDWIDTH_VERSION "" CACHE STRING
    "Release tag whose native library is downloaded (v2.0.0); empty takes the tag checked out, from git describe")
if(NOT FIXEDWIDTH_VERSION)
    find_package(Git QUIET)
    if(Git_FOUND)
        execute_process(COMMAND ${GIT_EXECUTABLE} describe --tags --exact-match
            WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
            OUTPUT_VARIABLE _git_tag OUTPUT_STRIP_TRAILING_WHITESPACE ERROR_QUIET RESULT_VARIABLE _git_result)
    endif()
    if(_git_result EQUAL 0 AND _git_tag)
        set(FIXEDWIDTH_VERSION "${_git_tag}")
    else()
        set(FIXEDWIDTH_VERSION "v0.0.0")
    endif()
endif()

set(_include_dir "${CMAKE_CURRENT_SOURCE_DIR}/../src/FixedWidthParser.Native/include")

include("${CMAKE_CURRENT_SOURCE_DIR}/cmake/NativeLib.cmake")
include("${CMAKE_CURRENT_SOURCE_DIR}/cmake/CopyNativeLibrary.cmake")
fixedwidth_resolve_native_lib(_native_lib)
fixedwidth_import_native_lib("${_native_lib}" "${_include_dir}/fixedwidth.def")

add_library(fw_fixedwidth INTERFACE)
target_include_directories(fw_fixedwidth INTERFACE "${_include_dir}")
target_compile_features(fw_fixedwidth INTERFACE cxx_std_23)
target_link_libraries(fw_fixedwidth INTERFACE fw::native)
add_library(fw::fixedwidth ALIAS fw_fixedwidth)

if(CMAKE_SOURCE_DIR STREQUAL PROJECT_SOURCE_DIR)
    set(_top_level ON)
else()
    set(_top_level OFF)
endif()
option(FIXEDWIDTH_INSTALL "Generate install rules and a find_package(fixedwidth) config package" ${_top_level})
option(FIXEDWIDTH_BUILD_TESTS "Build the C++ wrapper's tests" ${_top_level})

if(FIXEDWIDTH_INSTALL)
    include(GNUInstallDirs)
    include(CMakePackageConfigHelpers)

    get_filename_component(FIXEDWIDTH_INSTALLED_NATIVE_NAME "${_native_lib}" NAME)
    if(WIN32)
        set(FIXEDWIDTH_INSTALLED_NATIVE_DIR "${CMAKE_INSTALL_BINDIR}")
    else()
        set(FIXEDWIDTH_INSTALLED_NATIVE_DIR "${CMAKE_INSTALL_LIBDIR}")
    endif()
    set(FIXEDWIDTH_INSTALLED_IMPLIB_DIR "${CMAKE_INSTALL_LIBDIR}")
    set(FIXEDWIDTH_INSTALLED_INCLUDE_DIR "${CMAKE_INSTALL_INCLUDEDIR}")

    install(DIRECTORY "${_include_dir}/" DESTINATION "${CMAKE_INSTALL_INCLUDEDIR}"
        FILES_MATCHING PATTERN "*.h" PATTERN "*.hpp")
    install(FILES "${_native_lib}" DESTINATION "${FIXEDWIDTH_INSTALLED_NATIVE_DIR}")
    if(WIN32)
        get_target_property(_native_implib fixedwidth_native IMPORTED_IMPLIB)
        get_filename_component(FIXEDWIDTH_INSTALLED_IMPLIB_NAME "${_native_implib}" NAME)
        install(FILES "${_native_implib}" DESTINATION "${FIXEDWIDTH_INSTALLED_IMPLIB_DIR}")
    endif()

    set(_config_dir "${CMAKE_INSTALL_LIBDIR}/cmake/fixedwidth")
    configure_package_config_file(
        "${CMAKE_CURRENT_SOURCE_DIR}/cmake/fixedwidthConfig.cmake.in"
        "${CMAKE_CURRENT_BINARY_DIR}/fixedwidthConfig.cmake"
        INSTALL_DESTINATION "${_config_dir}"
        PATH_VARS FIXEDWIDTH_INSTALLED_NATIVE_DIR FIXEDWIDTH_INSTALLED_IMPLIB_DIR FIXEDWIDTH_INSTALLED_INCLUDE_DIR)

    # The package version takes only the tag's digits: v2.0.0-rc.1 installs as 2.0.0.
    if(FIXEDWIDTH_VERSION MATCHES "^v?([0-9]+(\\.[0-9]+)*)")
        set(_pkg_version "${CMAKE_MATCH_1}")
    else()
        set(_pkg_version "0.0.0")
    endif()
    write_basic_package_version_file(
        "${CMAKE_CURRENT_BINARY_DIR}/fixedwidthConfigVersion.cmake"
        VERSION "${_pkg_version}"
        COMPATIBILITY SameMajorVersion)

    install(FILES
        "${CMAKE_CURRENT_BINARY_DIR}/fixedwidthConfig.cmake"
        "${CMAKE_CURRENT_BINARY_DIR}/fixedwidthConfigVersion.cmake"
        "${CMAKE_CURRENT_SOURCE_DIR}/cmake/CopyNativeLibrary.cmake"
        DESTINATION "${_config_dir}")
endif()

if(FIXEDWIDTH_BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests)
endif()
