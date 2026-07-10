cmake_minimum_required(VERSION 3.25)

# Options that influence the vcpkg manifest must be declared before project(),
# because the vcpkg toolchain resolves VCPKG_MANIFEST_FEATURES during project().
option(PMSDK_BUILD_TESTS "Build PMSDK unit tests" ON)
option(PMSDK_BUILD_EXAMPLES "Build PMSDK example applications" ON)
option(PMSDK_WARNINGS_AS_ERRORS "Treat compiler warnings as errors" OFF)

if(PMSDK_BUILD_TESTS)
    list(APPEND VCPKG_MANIFEST_FEATURES "tests")
endif()

project(ProjectionMappingSDK
    VERSION 0.1.0
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
