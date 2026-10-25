# fixedwidth_resolve_native_lib(<out_var>) finds the FixedWidthParser.Native shared library for this platform:
#
# 1. FIXEDWIDTH_NATIVE_LIB, as a CMake or environment variable: a library you built or downloaded.
# 2. The copy python/scripts/build_native.py publishes into python/src/fixedwidth/_lib, in a checkout of the repo.
# 3. Otherwise the release asset for FIXEDWIDTH_VERSION, downloaded once into the build tree.
set(_fixedwidth_repo_root "${CMAKE_CURRENT_LIST_DIR}/../..")

function(fixedwidth_resolve_native_lib out_var)
    if(WIN32)
        set(_os "win")
        set(_ext "dll")
    elseif(APPLE)
        set(_os "osx")
        set(_ext "dylib")
    else()
        set(_os "linux")
        set(_ext "so")
    endif()
    if(CMAKE_SYSTEM_PROCESSOR MATCHES "^(arm64|aarch64|ARM64)$")
        set(_arch "arm64")
    else()
        set(_arch "x64")
    endif()
    set(_lib_name "FixedWidthParser.Native.${_ext}")

    if(FIXEDWIDTH_NATIVE_LIB)
        set(_lib "${FIXEDWIDTH_NATIVE_LIB}")
    elseif(DEFINED ENV{FIXEDWIDTH_NATIVE_LIB})
        set(_lib "$ENV{FIXEDWIDTH_NATIVE_LIB}")
    elseif(EXISTS "${_fixedwidth_repo_root}/python/src/fixedwidth/_lib/${_lib_name}")
        set(_lib "${_fixedwidth_repo_root}/python/src/fixedwidth/_lib/${_lib_name}")
    else()
        # Saved under the library's own name, not the asset's: the import library on Windows and the SONAME on
        # Linux both record the name the loader looks for.
        set(_lib "${CMAKE_BINARY_DIR}/fixedwidth-native/${FIXEDWIDTH_VERSION}/${_os}-${_arch}/${_lib_name}")
        if(NOT EXISTS "${_lib}")
            set(_url "https://github.com/GabrielMarquezMatte/FixedWidthParser/releases/download/${FIXEDWIDTH_VERSION}/fixedwidth-native-${_os}-${_arch}.${_ext}")
            message(STATUS "Downloading FixedWidthParser.Native: ${_url}")
            file(DOWNLOAD "${_url}" "${_lib}" STATUS _status)
            list(GET _status 0 _code)
            if(NOT _code EQUAL 0)
                file(REMOVE "${_lib}")
                list(GET _status 1 _message)
                message(FATAL_ERROR "Downloading ${_url} failed: ${_message}. Set FIXEDWIDTH_VERSION to a release "
                    "tag, or FIXEDWIDTH_NATIVE_LIB to a library you have.")
            endif()
        endif()
    endif()

    # A relative path would be re-resolved against the build directory once baked into the imported target.
    get_filename_component(_lib "${_lib}" ABSOLUTE BASE_DIR "${CMAKE_BINARY_DIR}")
    if(NOT EXISTS "${_lib}")
        message(FATAL_ERROR "FixedWidthParser.Native not found at ${_lib}")
    endif()
    set(${out_var} "${_lib}" PARENT_SCOPE)
endfunction()

# fixedwidth_import_native_lib(<library> <def file>) creates the IMPORTED target fw::native.
#
# NativeAOT ships no import library on Windows, so one is generated from the checked-in .def (lib.exe
# under MSVC, dlltool under MinGW), with a LIBRARY line naming the actual DLL so the loader finds it.
function(fixedwidth_import_native_lib library def_file)
    add_library(fixedwidth_native SHARED IMPORTED GLOBAL)
    set_target_properties(fixedwidth_native PROPERTIES IMPORTED_LOCATION "${library}")

    if(WIN32)
        get_filename_component(_dll_name "${library}" NAME)
        set(_generated_def "${CMAKE_BINARY_DIR}/fixedwidth.generated.def")
        set(_implib "${CMAKE_BINARY_DIR}/fixedwidth_native.lib")
        file(READ "${def_file}" _exports)
        file(WRITE "${_generated_def}" "LIBRARY ${_dll_name}\n${_exports}")
        if(MSVC)
            # The target's architecture, as the compiler reports it (x64, ARM64, X86), not the host's:
            # an x64 machine building for ARM64 (-A ARM64, or an ARM64 developer prompt) needs an ARM64 import library.
            set(_machine "${CMAKE_CXX_COMPILER_ARCHITECTURE_ID}")
            if(NOT _machine)
                message(FATAL_ERROR "cannot tell the MSVC target architecture for the import library")
            endif()
            find_program(_lib_exe NAMES lib REQUIRED)
            execute_process(COMMAND "${_lib_exe}" "/def:${_generated_def}" "/out:${_implib}" "/machine:${_machine}"
                RESULT_VARIABLE _result)
        else()
            find_program(_dlltool NAMES dlltool REQUIRED)
            execute_process(COMMAND "${_dlltool}" -d "${_generated_def}" -l "${_implib}" RESULT_VARIABLE _result)
        endif()
        if(NOT _result EQUAL 0)
            message(FATAL_ERROR "generating the import library for ${_dll_name} failed")
        endif()
        set_target_properties(fixedwidth_native PROPERTIES IMPORTED_IMPLIB "${_implib}")
    endif()

    add_library(fw::native ALIAS fixedwidth_native)
endfunction()
