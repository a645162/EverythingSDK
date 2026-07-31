cmake_minimum_required(VERSION 3.24)

project(EverythingSDK VERSION 1.0.0 LANGUAGES CXX)

if(NOT WIN32)
    message(FATAL_ERROR "EverythingSDK VC support currently targets Windows only.")
endif()

include(GNUInstallDirs)
include(CMakePackageConfigHelpers)

option(EVERYTHINGSDK_BUILD_WIN32_EXAMPLE "Build the Win32 desktop example." ON)

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(EVERYTHINGSDK_INSTALL_CMAKEDIR "${CMAKE_INSTALL_LIBDIR}/cmake/EverythingSDK")

if(CMAKE_VS_PLATFORM_NAME STREQUAL "Win32")
    set(EVERYTHINGSDK_CURRENT_NATIVE_DLL "${CMAKE_CURRENT_SOURCE_DIR}/native/Everything32.dll")
elseif(CMAKE_VS_PLATFORM_NAME STREQUAL "x64")
    set(EVERYTHINGSDK_CURRENT_NATIVE_DLL "${CMAKE_CURRENT_SOURCE_DIR}/native/Everything64.dll")
elseif(CMAKE_VS_PLATFORM_NAME STREQUAL "ARM64")
    set(EVERYTHINGSDK_CURRENT_NATIVE_DLL "${CMAKE_CURRENT_SOURCE_DIR}/native/EverythingARM64.dll")
elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "^[Aa][Rr][Mm]64$")
    set(EVERYTHINGSDK_CURRENT_NATIVE_DLL "${CMAKE_CURRENT_SOURCE_DIR}/native/EverythingARM64.dll")
elseif(CMAKE_SIZEOF_VOID_P EQUAL 8)
    set(EVERYTHINGSDK_CURRENT_NATIVE_DLL "${CMAKE_CURRENT_SOURCE_DIR}/native/Everything64.dll")
else()
    set(EVERYTHINGSDK_CURRENT_NATIVE_DLL "${CMAKE_CURRENT_SOURCE_DIR}/native/Everything32.dll")
endif()

add_subdirectory(src/libs/Lib.Everything.VC)

if(EVERYTHINGSDK_BUILD_WIN32_EXAMPLE)
    add_subdirectory(src/examples/Example.Everything.Win32.VC)
endif()

install(FILES
    "${CMAKE_CURRENT_SOURCE_DIR}/native/Everything32.dll"
    "${CMAKE_CURRENT_SOURCE_DIR}/native/Everything64.dll"
    "${CMAKE_CURRENT_SOURCE_DIR}/native/EverythingARM.dll"
    "${CMAKE_CURRENT_SOURCE_DIR}/native/EverythingARM64.dll"
    DESTINATION "native"
)

configure_package_config_file(
    "${CMAKE_CURRENT_SOURCE_DIR}/cmake/EverythingSDKConfig.cmake.in"
    "${CMAKE_CURRENT_BINARY_DIR}/EverythingSDKConfig.cmake"
    INSTALL_DESTINATION "${EVERYTHINGSDK_INSTALL_CMAKEDIR}"
)

write_basic_package_version_file(
    "${CMAKE_CURRENT_BINARY_DIR}/EverythingSDKConfigVersion.cmake"
    VERSION "${PROJECT_VERSION}"
    COMPATIBILITY SameMajorVersion
)

install(EXPORT EverythingSDKTargets
    FILE EverythingSDKTargets.cmake
    NAMESPACE EverythingSDK::
    DESTINATION "${EVERYTHINGSDK_INSTALL_CMAKEDIR}"
)

install(FILES
    "${CMAKE_CURRENT_BINARY_DIR}/EverythingSDKConfig.cmake"
    "${CMAKE_CURRENT_BINARY_DIR}/EverythingSDKConfigVersion.cmake"
    DESTINATION "${EVERYTHINGSDK_INSTALL_CMAKEDIR}"
)
