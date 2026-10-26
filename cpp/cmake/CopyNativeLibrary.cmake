# fixedwidth_copy_native_library(<target>) copies the native library next to an executable after it links.
#
# On Windows a DLL beside neither the executable nor PATH is not found, and the process exits with 0xc0000135
# (STATUS_DLL_NOT_FOUND) before main runs. A no-op on Linux and macOS, where the rpath resolves it.
function(fixedwidth_copy_native_library target)
    if(NOT WIN32)
        return()
    endif()
    if(NOT TARGET ${target})
        message(FATAL_ERROR "fixedwidth_copy_native_library: '${target}' is not a target")
    endif()
    add_custom_command(TARGET ${target} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:fw::native>" "$<TARGET_FILE_DIR:${target}>"
        COMMENT "Copying FixedWidthParser.Native next to ${target}")
endfunction()
