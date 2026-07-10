import os
import subprocess

def run(cmd):
    print(f"Running: {cmd}")
    subprocess.run(cmd, shell=True, check=True)

# Read current full files
with open('include/PMSDK/PMSDK.h', 'r') as f:
    pmsdk_h_full = f.read()
with open('src/CMakeLists.txt', 'r') as f:
    src_cmake_full = f.read()
with open('tests/CMakeLists.txt', 'r') as f:
    tests_cmake_full = f.read()

# --- M1 & M2 ---
pmsdk_h_m12 = pmsdk_h_full.split('#include "PMSDK/Math/Vector2.h"')[0].strip() + '\n'

src_cmake_m12 = src_cmake_full.replace("""    Geometry/Mesh.cpp
    Geometry/MeshBuilder.cpp
    Geometry/MeshSubdivision.cpp
    Geometry/UVMapping.cpp
    Geometry/BVH.cpp
    Geometry/KDTree.cpp)""", ")")

tests_cmake_m12 = tests_cmake_full.replace("""    Math/VectorTests.cpp
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
""", "")

with open('include/PMSDK/PMSDK.h', 'w') as f: f.write(pmsdk_h_m12)
with open('src/CMakeLists.txt', 'w') as f: f.write(src_cmake_m12)
with open('tests/CMakeLists.txt', 'w') as f: f.write(tests_cmake_m12)

run('git add .gitignore README.md .clang-format .editorconfig .github/ AGENTS.md CMakeLists.txt CMakePresets.json cmake/ docs/ examples/ include/PMSDK/Core/ src/Core/ tests/Core/ vcpkg.json src/CMakeLists.txt tests/CMakeLists.txt include/PMSDK/PMSDK.h third_party/vcpkg .gitmodules')
run('git commit -m "Milestone 1 & 2: Repository Setup and Core Module"')


# --- M3 ---
pmsdk_h_m3 = pmsdk_h_full.split('#include "PMSDK/Geometry/Vertex.h"')[0].strip() + '\n'

tests_cmake_m3 = tests_cmake_full.replace("""    Geometry/MeshTests.cpp
    Geometry/MeshBuilderTests.cpp
    Geometry/IntersectionTests.cpp
    Geometry/BVHTests.cpp
    Geometry/CurveTests.cpp
    Geometry/KDTreeTests.cpp
""", "")

with open('include/PMSDK/PMSDK.h', 'w') as f: f.write(pmsdk_h_m3)
# src/CMakeLists.txt is the same as M12 since Math is header only
with open('tests/CMakeLists.txt', 'w') as f: f.write(tests_cmake_m3)

run('git add include/PMSDK/Math tests/Math src/CMakeLists.txt tests/CMakeLists.txt include/PMSDK/PMSDK.h docs/')
run('git commit -m "Milestone 3: Math Library"')


# --- M4 ---
with open('include/PMSDK/PMSDK.h', 'w') as f: f.write(pmsdk_h_full)
with open('src/CMakeLists.txt', 'w') as f: f.write(src_cmake_full)
with open('tests/CMakeLists.txt', 'w') as f: f.write(tests_cmake_full)

run('git add include/PMSDK/Geometry src/Geometry tests/Geometry src/CMakeLists.txt tests/CMakeLists.txt include/PMSDK/PMSDK.h docs/')
run('git commit -m "Milestone 4: Geometry Library"')

run('git push')
