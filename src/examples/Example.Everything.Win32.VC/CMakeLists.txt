# File: src/examples/Example.Everything.Win32.VC/CMakeLists.txt
# Brief: CMake build configuration.
# Author: Haomin Kong
# Email: a645162@163.com
# Created: 2026-07-31

add_executable(Example.Everything.Win32.VC WIN32
    src/Main.cpp
)

target_compile_features(Example.Everything.Win32.VC PRIVATE cxx_std_11)
target_compile_definitions(Example.Everything.Win32.VC PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
target_link_libraries(Example.Everything.Win32.VC PRIVATE Lib.Everything.VC Comctl32 Shell32)

if(DEFINED EVERYTHINGSDK_CURRENT_NATIVE_DLL AND EXISTS "${EVERYTHINGSDK_CURRENT_NATIVE_DLL}")
    add_custom_command(TARGET Example.Everything.Win32.VC POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${EVERYTHINGSDK_CURRENT_NATIVE_DLL}"
            "$<TARGET_FILE_DIR:Example.Everything.Win32.VC>"
        VERBATIM
    )
endif()
