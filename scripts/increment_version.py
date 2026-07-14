import os
import re
import sys

def increment_version():
    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    cmake_path = os.path.join(root_dir, 'CMakeLists.txt')
    header_path = os.path.join(root_dir, 'include', 'PMSDK', 'Core', 'Version.h')

    # Read CMakeLists.txt
    with open(cmake_path, 'r') as f:
        cmake_content = f.read()

    # Find VERSION X.Y.Z
    match = re.search(r'VERSION\s+(\d+)\.(\d+)\.(\d+)', cmake_content)
    if not match:
        print("Error: Could not find VERSION in CMakeLists.txt")
        sys.exit(1)

    major = int(match.group(1))
    minor = int(match.group(2))
    patch = int(match.group(3))

    new_patch = patch + 1
    new_version_str = f"VERSION {major}.{minor}.{new_patch}"

    # Replace in CMakeLists.txt
    new_cmake_content = cmake_content[:match.start()] + new_version_str + cmake_content[match.end():]
    with open(cmake_path, 'w') as f:
        f.write(new_cmake_content)

    # Read Version.h
    with open(header_path, 'r') as f:
        header_content = f.read()

    # Find kHeaderVersion{X, Y, Z}
    h_match = re.search(r'inline\s+constexpr\s+Version\s+kHeaderVersion\{(\d+),\s*(\d+),\s*(\d+)\};', header_content)
    if not h_match:
        print("Error: Could not find kHeaderVersion in Version.h")
        sys.exit(1)

    new_h_version_str = f"inline constexpr Version kHeaderVersion{{{major}, {minor}, {new_patch}}};"
    
    # Replace in Version.h
    new_header_content = header_content[:h_match.start()] + new_h_version_str + header_content[h_match.end():]
    with open(header_path, 'w') as f:
        f.write(new_header_content)

    print(f"Incremented version to {major}.{minor}.{new_patch}")
    return f"{major}.{minor}.{new_patch}"

if __name__ == "__main__":
    increment_version()
