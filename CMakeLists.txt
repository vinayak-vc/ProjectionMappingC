cmake_minimum_required(VERSION 3.25)

# Options that influence the vcpkg manifest must be declared before project(),
# because the vcpkg toolchain resolves VCPKG_MANIFEST_FEATURES during project().
option(PMSDK_BUILD_TESTS "Build PMSDK unit tests" ON)
option(PMSDK_BUILD_EXAMPLES "Build PMSDK example applications" ON)
option(PMSDK_WARNINGS_AS_ERRORS "Treat compiler warnings as errors" OFF)
# HoloTrack OAK-D device source (DepthAI). OFF by default: the DLL and its tests build with no
# camera and no DepthAI dependency; the device path is compiled only when this is ON (D-032).
option(HOLOTRACK_WITH_DEPTHAI "Build the HoloTrack OAK-D device source (requires DepthAI)" OFF)
option(HOLOTRACK_DEPTHAI_USE_VCPKG "Resolve DepthAI through the vcpkg manifest feature instead of an external CMake package" OFF)

if(PMSDK_BUILD_TESTS)
    list(APPEND VCPKG_MANIFEST_FEATURES "tests")
endif()
if(HOLOTRACK_WITH_DEPTHAI AND HOLOTRACK_DEPTHAI_USE_VCPKG)
    list(APPEND VCPKG_MANIFEST_FEATURES "depthai")
endif()

project(ProjectionMappingSDK
    VERSION 1.0.3
    DESCRIPTION "Engine-agnostic projection mapping SDK"
    LANGUAGES CXX)

option(BUILD_SHARED_LIBS "Build PMSDK as a shared library" ON)

list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")
include(PMSDKCompilerOptions)

if(PROJECT_IS_TOP_LEVEL)
    # Keep the SDK binary and test/example executables in one directory so
    # Windows finds the DLL at test time without PATH manipulation.
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
    set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib")
endif()

add_subdirectory(src)

if(PMSDK_BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests)
endif()

if(PMSDK_BUILD_EXAMPLES)
    add_subdirectory(examples)
endif()

# HoloTrackSDK — independent head-tracking product/DLL (docs/decisions.md D-029).
# Placed after enable_testing() so its gtest_discover_tests registers cleanly.
option(HOLOTRACK_BUILD "Build the HoloTrack head-tracking SDK" ON)
if(HOLOTRACK_BUILD)
    add_subdirectory(holotrack)
endif()

find_package(Python COMPONENTS Interpreter REQUIRED)
add_custom_target(package_sdk
    COMMAND ${Python_EXECUTABLE} ${PROJECT_SOURCE_DIR}/scripts/package_release.py
    WORKING_DIRECTORY ${PROJECT_SOURCE_DIR}
    DEPENDS pmsdk
    COMMENT "Packaging the SDK..."
)
