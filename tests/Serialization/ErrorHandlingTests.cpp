#include <gtest/gtest.h>
#include "PMSDK/Serialization/GeometrySerializer.h"
#include <string>

using namespace pmsdk;
using namespace pmsdk::Serialization;
using namespace pmsdk::Geometry;

TEST(ErrorHandlingTests, DeserializeMalformedJson) {
    std::string malformed = "{ \"mesh\": [ missing brackets }";
    Mesh outMesh;
    EXPECT_ANY_THROW(DeserializeMesh(malformed, outMesh));
}

TEST(ErrorHandlingTests, DeserializeMissingFields) {
    std::string incomplete = R"({
        "type": "mesh",
        "version": 1
    })";
    
    Mesh outMesh;
    // Missing "vertices" and "indices" should throw
    EXPECT_ANY_THROW(DeserializeMesh(incomplete, outMesh));
}
