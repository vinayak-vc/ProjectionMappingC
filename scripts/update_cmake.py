import os

def update_cmake():
    with open('CMakeLists.txt', 'r') as f:
        text = f.read()
    
    text = text.replace('VERSION 0.1.0', 'VERSION 1.0.0')
    
    append_text = """
find_package(Python COMPONENTS Interpreter REQUIRED)
add_custom_target(package_sdk
    COMMAND ${Python_EXECUTABLE} ${PROJECT_SOURCE_DIR}/scripts/package_release.py
    WORKING_DIRECTORY ${PROJECT_SOURCE_DIR}
    DEPENDS pmsdk
    COMMENT "Packaging the SDK..."
)
"""
    if "package_sdk" not in text:
        text += append_text

    with open('CMakeLists.txt', 'w') as f:
        f.write(text)

if __name__ == "__main__":
    update_cmake()
