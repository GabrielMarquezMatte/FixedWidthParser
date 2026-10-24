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

# Copies the native library next to an executable: on Windows a DLL beside neither the executable nor
# PATH is not found, and the process exits before main. A no-op elsewhere, where the rpath resolves it.
function(fixedwidth_copy_native_library target)
    if(NOT WIN32)
        return()
    endif()
    add_custom_command(TARGET ${target} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:fw::native>" "$<TARGET_FILE_DIR:${target}>"
        COMMENT "Copying FixedWidthParser.Native next to ${target}")
endfunction()
