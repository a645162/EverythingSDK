# File: src/libs/Lib.Everything.VC/CMakeLists.txt
# Brief: CMake build configuration.
# Author: Haomin Kong
# Email: a645162@163.com
# Created: 2026-07-31

add_library(Lib.Everything.VC STATIC)

if(WIN32)
    target_sources(Lib.Everything.VC PRIVATE
        src/Everything.cpp
    )
else()
    # POSIX 没有 Everything 服务：用 plocate/locate、find 或进程内扫描提供同一套 API。
    target_sources(Lib.Everything.VC PRIVATE
        src/everything_posix.cpp
        src/posix/file_metadata.cpp
        src/posix/file_metadata.h
        src/posix/process_runner.cpp
        src/posix/process_runner.h
        src/posix/search_backend.cpp
        src/posix/search_backend.h
        src/posix/search_query.cpp
        src/posix/search_query.h
        src/posix/wide_string.cpp
        src/posix/wide_string.h
    )
endif()

set(_everythingsdk_generated_include_dir
    "${CMAKE_CURRENT_BINARY_DIR}/generated/include")
set(_everythingsdk_generated_header_dir
    "${_everythingsdk_generated_include_dir}/Lib/Everything")
file(MAKE_DIRECTORY "${_everythingsdk_generated_header_dir}")

set(EVERYTHINGSDK_VERSION "${PROJECT_VERSION}")
set(EVERYTHINGSDK_ASSEMBLY_VERSION "${EverythingSdkAssemblyVersion}")
set(EVERYTHINGSDK_FILE_VERSION "${EverythingSdkFileVersion}")
set(EVERYTHINGSDK_VERSION_MAJOR "${PROJECT_VERSION_MAJOR}")
set(EVERYTHINGSDK_VERSION_MINOR "${PROJECT_VERSION_MINOR}")
set(EVERYTHINGSDK_VERSION_PATCH "${PROJECT_VERSION_PATCH}")

configure_file(
    "${EverythingSDK_SOURCE_DIR}/cmake/everythingsdk_library_version.h.in"
    "${_everythingsdk_generated_header_dir}/version.h"
    @ONLY
)

target_compile_features(Lib.Everything.VC PUBLIC cxx_std_11)

if(WIN32)
    target_compile_definitions(Lib.Everything.VC PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
endif()

target_include_directories(Lib.Everything.VC
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<BUILD_INTERFACE:${_everythingsdk_generated_include_dir}>
        $<INSTALL_INTERFACE:${CMAKE_INSTALL_INCLUDEDIR}>
)

set_target_properties(Lib.Everything.VC PROPERTIES
    OUTPUT_NAME "Lib.Everything.VC"
    VERSION "${PROJECT_VERSION}"
)

install(TARGETS Lib.Everything.VC
    EXPORT EverythingSDKTargets
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
    INCLUDES DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)

install(DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/include/"
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)

install(FILES
    "${_everythingsdk_generated_header_dir}/version.h"
    DESTINATION "${CMAKE_INSTALL_INCLUDEDIR}/Lib/Everything"
)
