add_executable(pmsdk_example_native main.cpp)

target_link_libraries(pmsdk_example_native PRIVATE PMSDK::pmsdk)

pmsdk_apply_compiler_options(pmsdk_example_native)
