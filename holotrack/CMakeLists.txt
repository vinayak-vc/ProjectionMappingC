# HoloTrackSDK — Head-Tracked Holographic Projection System.
# Independent product/DLL (docs/decisions.md D-029): its own target and output name, zero
# runtime dependency on pmsdk. It reuses only the header-only pmsdk::Math value types at build
# time (no link), so the resulting DLL stands alone.

add_library(holotrack)
add_library(HoloTrack::holotrack ALIAS holotrack)

target_sources(holotrack PRIVATE
    src/Tracking/Tracker.cpp
    src/C_API/TrackingAPI.cpp)

# Public headers live in the shared include/ root (include/HoloTrack, plus header-only
# pmsdk::Math from include/PMSDK reused at build time only).
target_include_directories(holotrack PUBLIC
    $<BUILD_INTERFACE:${PROJECT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>)

pmsdk_apply_compiler_options(holotrack)

target_compile_definitions(holotrack PRIVATE HOLOTRACK_EXPORTS)
if(NOT BUILD_SHARED_LIBS)
    target_compile_definitions(holotrack PUBLIC HOLOTRACK_STATIC)
endif()

set_target_properties(holotrack PROPERTIES
    OUTPUT_NAME "HoloTrackSDK"
    VERSION ${PROJECT_VERSION}
    SOVERSION ${PROJECT_VERSION_MAJOR}
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
    POSITION_INDEPENDENT_CODE ON
    DEBUG_POSTFIX "d")

# --- Verification (pure logic, no camera/DepthAI — D-032) ---
#
# Primary verifier is a standalone assertion harness with NO third-party dependency, so it
# builds and runs with whatever MSVC toolset is doing the linking. (The prebuilt vcpkg
# GoogleTest is compiled against a newer MSVC STL than the local link toolset, so it drags in
# unresolved vectorized-algorithm symbols like __std_rotate unless a heavy lib such as OpenCV
# transitively supplies the newer CRT — which this pure test target must not depend on.) The
# GoogleTest suite is retained for CI and gated behind HOLOTRACK_BUILD_GTESTS (default OFF);
# turn it on once the build and gtest toolsets are aligned.
if(PMSDK_BUILD_TESTS)
    add_executable(holotrack_harness
        tests/harness/HoloTrackHarness.cpp
        ${CMAKE_CURRENT_SOURCE_DIR}/src/Tracking/Tracker.cpp
        ${CMAKE_CURRENT_SOURCE_DIR}/src/C_API/TrackingAPI.cpp)
    target_include_directories(holotrack_harness PRIVATE ${PROJECT_SOURCE_DIR}/include)
    target_compile_definitions(holotrack_harness PRIVATE HOLOTRACK_STATIC)
    pmsdk_apply_compiler_options(holotrack_harness)

    enable_testing()
    add_test(NAME holotrack_harness COMMAND holotrack_harness)

    option(HOLOTRACK_BUILD_GTESTS "Build the GoogleTest suite (needs a gtest-matching MSVC toolset)" OFF)
    if(HOLOTRACK_BUILD_GTESTS)
        find_package(GTest CONFIG REQUIRED)
        add_executable(holotrack_tests
            tests/FilterTests.cpp
            tests/OffAxisProjectionTests.cpp
            tests/CoordinateTransformTests.cpp
            tests/HeadEstimatorTests.cpp
            tests/ViewerSelectorTests.cpp
            tests/StateMachineTests.cpp
            tests/TrackerTests.cpp
            ${CMAKE_CURRENT_SOURCE_DIR}/src/Tracking/Tracker.cpp)
        target_include_directories(holotrack_tests PRIVATE ${PROJECT_SOURCE_DIR}/include)
        target_compile_definitions(holotrack_tests PRIVATE HOLOTRACK_STATIC)
        target_link_libraries(holotrack_tests PRIVATE GTest::gtest_main)
        target_compile_features(holotrack_tests PRIVATE cxx_std_20)
        set_property(TARGET holotrack_tests PROPERTY
            MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
        if(MSVC)
            target_compile_options(holotrack_tests PRIVATE /W4 /MP)
        endif()
        include(GoogleTest)
        gtest_discover_tests(holotrack_tests)
    endif()
endif()

# --- Install (independent package export) ---
include(GNUInstallDirs)
install(TARGETS holotrack
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR})
install(DIRECTORY ${PROJECT_SOURCE_DIR}/include/HoloTrack
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
    FILES_MATCHING PATTERN "*.h")
