# PMSDKOakCamera — optional native plugin that streams OAK-D colour frames for camera-based
# calibration (docs/decisions.md D-044). It is the only PMSDK binary that links depthai-core, so
# ProjectionMappingSDK.dll stays camera-free (same principle as HoloTrack, D-032). Deploy it next to
# depthai-core.dll (Plugins/HoloTrack in the Unity game repo) so Windows resolves the dependency.
#
# Requires an external depthai-core v2.x CMake package (D-033), e.g.
#   -DPMSDK_OAKCAM_BUILD=ON -DCMAKE_PREFIX_PATH=C:/Unity/ProjectionMappingC/build/deps/depthai-core-v2.32.0-win64

find_package(depthai CONFIG REQUIRED)

add_library(pmsdk_oakcam SHARED src/OakCameraAPI.cpp)
add_library(PMSDK::oakcam ALIAS pmsdk_oakcam)

target_link_libraries(pmsdk_oakcam PRIVATE depthai::core)
target_include_directories(pmsdk_oakcam PUBLIC
    $<BUILD_INTERFACE:${PROJECT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>)

pmsdk_apply_compiler_options(pmsdk_oakcam)
target_compile_definitions(pmsdk_oakcam PRIVATE PMOAK_EXPORTS)

set_target_properties(pmsdk_oakcam PROPERTIES
    OUTPUT_NAME "PMSDKOakCamera"
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
    DEBUG_POSTFIX "d")

# Hardware smoke test (not a CTest: needs the device and exclusive access — close OAK Viewer).
add_executable(pmsdk_oakcam_smoke tools/OakCameraSmoke.cpp)
target_link_libraries(pmsdk_oakcam_smoke PRIVATE pmsdk_oakcam)
pmsdk_apply_compiler_options(pmsdk_oakcam_smoke)

# depthai-core's API passes std::vector/std::string by value across the DLL boundary, so this plugin
# must share the dynamic CRT with the prebuilt depthai-core (/MD), not the presets' static CRT (/MT).
set_target_properties(pmsdk_oakcam pmsdk_oakcam_smoke PROPERTIES
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
