# File: libs/EverythingSDK/tests/Test.Everything.VC/CMakeLists.txt
# Brief: POSIX 后端可重复的端到端测试（固定目录 + 强制 scan 后端）。
# Author: Haomin Kong
# Email: konghm@beim.com.cn / a645162@163.com
# Created: 2026-10-31

if(NOT WIN32)
    add_executable(Test.Everything.VC main.cpp)

    target_link_libraries(Test.Everything.VC PRIVATE Lib.Everything.VC)
    target_compile_features(Test.Everything.VC PRIVATE cxx_std_11)

    add_test(NAME Test.Everything.VC COMMAND Test.Everything.VC)
endif()
