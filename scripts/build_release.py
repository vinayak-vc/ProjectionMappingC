import os
import subprocess
import sys

def main():
    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    
    # 1. Increment Version
    print("--- 1. Incrementing Version ---")
    inc_script = os.path.join(root_dir, 'scripts', 'increment_version.py')
    subprocess.run([sys.executable, inc_script], check=True)

    # 2. Reconfigure CMake to pick up new version
    print("\n--- 2. Reconfiguring CMake ---")
    subprocess.run(["cmake", "--preset", "vs2022"], cwd=root_dir, check=True)

    # 3. Build the Release binaries
    print("\n--- 3. Building Release Binaries ---")
    subprocess.run(["cmake", "--build", "build/vs2022", "--config", "Release"], cwd=root_dir, check=True)

    # 4. Package the Release
    print("\n--- 4. Packaging Release ---")
    pkg_script = os.path.join(root_dir, 'scripts', 'package_release.py')
    subprocess.run([sys.executable, pkg_script], check=True)

    print("\nAll done! New release is ready in the dist/ folder.")

if __name__ == "__main__":
    main()
