add_library(pmsdk)
add_library(PMSDK::pmsdk ALIAS pmsdk)

find_package(nlohmann_json CONFIG REQUIRED)
find_package(OpenCV REQUIRED)

target_sources(pmsdk PRIVATE
    Core/Config.cpp
    Core/Context.cpp
    Core/LastError.cpp
    Core/Logger.cpp
    Core/Version.cpp
    Geometry/Mesh.cpp
    Geometry/MeshBuilder.cpp
    Geometry/MeshSubdivision.cpp
    Geometry/UVMapping.cpp
    Geometry/BVH.cpp
    Geometry/KDTree.cpp
    Geometry/DynamicMesh.cpp
    Geometry/BezierPatch.cpp
    Geometry/GridWarp.cpp
    Geometry/MeshOptimizer.cpp
    Warp/Projector.cpp
    Warp/DeformationField.cpp
    Warp/WarpNode.cpp
    Warp/Sampler.cpp
    Blend/EdgeBlend.cpp
    Blend/BlendConfig.cpp
    Blend/MaskGenerator.cpp
    Serialization/GeometrySerializer.cpp
    Serialization/WarpSerializer.cpp
    Serialization/BlendSerializer.cpp
    Calibration/Intrinsics.cpp
    Calibration/Extrinsics.cpp
    Calibration/Calibrator.cpp
    Calibration/GrayCode.cpp
    C_API/GeometryAPI.cpp
    C_API/WarpAPI.cpp
    C_API/BlendAPI.cpp)

# Internal headers (src/…) are visible to the SDK only, never installed.
target_include_directories(pmsdk PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

target_include_directories(pmsdk PUBLIC
    $<BUILD_INTERFACE:${PROJECT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>)

target_link_libraries(pmsdk PRIVATE nlohmann_json::nlohmann_json ${OpenCV_LIBS})

pmsdk_apply_compiler_options(pmsdk)

# Version is single-sourced from the CMake project(); Version.cpp static_asserts
# that the hand-written header constants stay in sync.
target_compile_definitions(pmsdk PRIVATE
    PMSDK_EXPORTS
    PMSDK_VERSION_MAJOR=${PROJECT_VERSION_MAJOR}
    PMSDK_VERSION_MINOR=${PROJECT_VERSION_MINOR}
    PMSDK_VERSION_PATCH=${PROJECT_VERSION_PATCH}
    "PMSDK_VERSION_STRING=\"${PROJECT_VERSION}\"")

if(NOT BUILD_SHARED_LIBS)
    target_compile_definitions(pmsdk PUBLIC PMSDK_STATIC)
endif()

set_target_properties(pmsdk PROPERTIES
    OUTPUT_NAME "ProjectionMappingSDK"
    VERSION ${PROJECT_VERSION}
    SOVERSION ${PROJECT_VERSION_MAJOR}
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
    POSITION_INDEPENDENT_CODE ON
    DEBUG_POSTFIX "d")

include(GNUInstallDirs)
include(CMakePackageConfigHelpers)

install(TARGETS pmsdk EXPORT PMSDKTargets
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR})

install(DIRECTORY ${PROJECT_SOURCE_DIR}/include/PMSDK
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
    FILES_MATCHING PATTERN "*.h")

install(EXPORT PMSDKTargets
    NAMESPACE PMSDK::
    DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/PMSDK)

write_basic_package_version_file(
    "${CMAKE_CURRENT_BINARY_DIR}/PMSDKConfigVersion.cmake"
    VERSION ${PROJECT_VERSION}
    COMPATIBILITY SameMajorVersion)

configure_package_config_file(
    "${PROJECT_SOURCE_DIR}/cmake/PMSDKConfig.cmake.in"
    "${CMAKE_CURRENT_BINARY_DIR}/PMSDKConfig.cmake"
    INSTALL_DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/PMSDK)

install(FILES
    "${CMAKE_CURRENT_BINARY_DIR}/PMSDKConfig.cmake"
    "${CMAKE_CURRENT_BINARY_DIR}/PMSDKConfigVersion.cmake"
    DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/PMSDK)
