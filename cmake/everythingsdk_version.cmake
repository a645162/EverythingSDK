# File: everythingsdk_version.cmake
# Brief: Everythingsdk version.
# Author: Haomin Kong
# Email: a645162@163.com
# Created: 2026-08-04

function(everythingsdk_load_version_from_props VERSION_PROPS)
  file(READ "${VERSION_PROPS}" _everythingsdk_version_props)

  foreach(_field IN ITEMS
      EverythingSdkVersion
      EverythingSdkAssemblyVersion
      EverythingSdkFileVersion
      EverythingSdkInformationalVersion)
    string(REGEX MATCH "<${_field}>([^<]+)</${_field}>"
        _match "${_everythingsdk_version_props}")
    if(NOT _match)
      message(FATAL_ERROR
          "Missing ${_field} in version props: ${VERSION_PROPS}")
    endif()
    set(${_field} "${CMAKE_MATCH_1}" PARENT_SCOPE)
  endforeach()
endfunction()
