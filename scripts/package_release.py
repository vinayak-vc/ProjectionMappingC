import os
import shutil
import zipfile
import re
import sys

def get_current_version(root_dir):
    cmake_path = os.path.join(root_dir, 'CMakeLists.txt')
    with open(cmake_path, 'r') as f:
        match = re.search(r'VERSION\s+(\d+\.\d+\.\d+)', f.read())
        if match:
            return match.group(1)
    return "unknown"

def package_release():
    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    version = get_current_version(root_dir)
    print(f"Packaging Release v{version}...")

    dist_dir = os.path.join(root_dir, 'dist')
    if os.path.exists(dist_dir):
        shutil.rmtree(dist_dir)
    os.makedirs(dist_dir)

    staging_name = f"ProjectionMappingSDK_v{version}_Windows"
    staging_dir = os.path.join(dist_dir, staging_name)
    os.makedirs(staging_dir)

    # 1. Copy C_API headers
    c_api_staging_inc = os.path.join(staging_dir, 'C_API', 'include', 'PMSDK')
    os.makedirs(c_api_staging_inc)
    
    include_src = os.path.join(root_dir, 'include', 'PMSDK')
    # Copy all headers but only keep the ones we need for C API, or keep all of them?
    # Usually we want the entire include directory so users can use the C++ API too.
    shutil.copytree(include_src, c_api_staging_inc, dirs_exist_ok=True)

    # 2. Copy Libs and DLLs
    c_api_staging_lib = os.path.join(staging_dir, 'C_API', 'lib')
    c_api_staging_bin = os.path.join(staging_dir, 'C_API', 'bin')
    os.makedirs(c_api_staging_lib)
    os.makedirs(c_api_staging_bin)

    build_dir = os.path.join(root_dir, 'build', 'vs2022')
    dll_path = os.path.join(build_dir, 'bin', 'Release', 'ProjectionMappingSDK.dll')
    lib_path = os.path.join(build_dir, 'lib', 'Release', 'ProjectionMappingSDK.lib')

    if not os.path.exists(dll_path) or not os.path.exists(lib_path):
        print(f"Error: Could not find DLL/LIB in {build_dir}. Did you compile in Release mode first?")
        sys.exit(1)

    shutil.copy(dll_path, c_api_staging_bin)
    shutil.copy(lib_path, c_api_staging_lib)

    # 3. Copy Unity Plugin
    unity_src = os.path.join(root_dir, 'bindings', 'unity', 'com.viitorx.pmsdk')
    unity_dst = os.path.join(staging_dir, 'Unity', 'com.viitorx.pmsdk')
    if os.path.exists(unity_src):
        shutil.copytree(unity_src, unity_dst)
        # Inject DLL into Unity Plugins folder
        unity_plugins_dir = os.path.join(unity_dst, 'Plugins', 'x86_64')
        os.makedirs(unity_plugins_dir, exist_ok=True)
        shutil.copy(dll_path, unity_plugins_dir)
        print("Injected ProjectionMappingSDK.dll into Unity Package.")

    # 4. Copy Unreal Plugin
    unreal_src = os.path.join(root_dir, 'bindings', 'unreal', 'Plugins', 'ProjectionMapping')
    unreal_dst = os.path.join(staging_dir, 'Unreal', 'ProjectionMapping')
    if os.path.exists(unreal_src):
        shutil.copytree(unreal_src, unreal_dst)
        # Inject DLL into Unreal Binaries folder
        unreal_bin_dir = os.path.join(unreal_dst, 'Source', 'ThirdParty', 'ProjectionMappingSDK', 'bin')
        os.makedirs(unreal_bin_dir, exist_ok=True)
        shutil.copy(dll_path, unreal_bin_dir)
        print("Injected ProjectionMappingSDK.dll into Unreal Plugin.")

    # 5. Zip it up
    zip_path = os.path.join(dist_dir, f"{staging_name}.zip")
    shutil.make_archive(os.path.join(dist_dir, staging_name), 'zip', dist_dir, staging_name)
    
    print(f"Success! Packaged release to {zip_path}")

if __name__ == "__main__":
    package_release()
