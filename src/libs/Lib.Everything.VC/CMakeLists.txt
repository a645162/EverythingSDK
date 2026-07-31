add_library(Lib.Everything.VC STATIC
    src/Everything.cpp
)

target_compile_features(Lib.Everything.VC PUBLIC cxx_std_11)
target_compile_definitions(Lib.Everything.VC PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)

target_include_directories(Lib.Everything.VC
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:${CMAKE_INSTALL_INCLUDEDIR}>
)

set_target_properties(Lib.Everything.VC PROPERTIES
    OUTPUT_NAME "Lib.Everything.VC"
)

install(TARGETS Lib.Everything.VC
    EXPORT EverythingSDKTargets
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
    INCLUDES DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)

install(DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/include/"
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)
