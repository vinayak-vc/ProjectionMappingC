find_package(GTest CONFIG REQUIRED)

add_executable(pmsdk_tests
    Core/ConfigTests.cpp
    Core/ContextTests.cpp
    Core/ErrorCodeTests.cpp
    Core/HandleRegistryTests.cpp
    Core/LastErrorTests.cpp
    Core/LoggerTests.cpp
    Core/StatusTests.cpp
    Core/VersionTests.cpp
    Math/VectorTests.cpp
    Math/QuaternionTests.cpp
    Math/Matrix4Tests.cpp
    Math/TransformTests.cpp
    Math/GeometryMathTests.cpp
    Geometry/MeshTests.cpp
    Geometry/MeshBuilderTests.cpp
    Geometry/IntersectionTests.cpp
    Geometry/BVHTests.cpp
    Geometry/CurveTests.cpp
    Geometry/KDTreeTests.cpp
    Geometry/DynamicMeshTests.cpp
    Geometry/BezierPatchTests.cpp
    Geometry/GridWarpTests.cpp
    Geometry/MeshOptimizerTests.cpp
    Warp/ProjectorTests.cpp
    Warp/DeformationFieldTests.cpp
    Warp/WarpNodeTests.cpp
    Warp/SamplerTests.cpp
    Blend/EdgeBlendTests.cpp
    Blend/BlendConfigTests.cpp
    Blend/MaskGeneratorTests.cpp
    Serialization/GeometrySerializerTests.cpp
    Serialization/WarpSerializerTests.cpp
    Serialization/BlendSerializerTests.cpp
    Calibration/IntrinsicsTests.cpp
    Calibration/ExtrinsicsTests.cpp
    Calibration/GrayCodeTests.cpp
    C_API/GeometryAPITests.cpp
    C_API/WarpAPITests.cpp
    C_API/CalibrationAPITests.cpp
    C_API/DecoderTests.cpp
    Warp/DeformationFieldEdgeTests.cpp
    Serialization/ErrorHandlingTests.cpp
    # Internal (non-exported) code under test is compiled into the test binary
    # directly — its symbols are hidden inside the SDK DLL.
    ${PROJECT_SOURCE_DIR}/src/Core/LastError.cpp)

# Tests may reach internal headers; consumers must not.
target_include_directories(pmsdk_tests PRIVATE 
    ${PROJECT_SOURCE_DIR}/src
    ${OpenCV_INCLUDE_DIRS}
)

target_link_libraries(pmsdk_tests
    PRIVATE
        PMSDK::pmsdk
        GTest::gtest_main
        ${OpenCV_LIBS}
)

target_compile_features(pmsdk_tests PRIVATE cxx_std_20)
if(MSVC)
    target_compile_options(pmsdk_tests PRIVATE /W4 /MP)
endif()

include(GoogleTest)
gtest_discover_tests(pmsdk_tests)
