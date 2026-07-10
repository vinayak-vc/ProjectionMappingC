/**
 * @mainpage Projection Mapping SDK (PMSDK)
 * 
 * @section intro_sec Introduction
 * 
 * The Projection Mapping SDK (PMSDK) is a high-performance, engine-agnostic 
 * library designed for complex projection mapping scenarios. It provides robust 
 * tools for mesh manipulation, UV warping, edge blending, and projector calibration.
 * 
 * @section arch_sec Architecture
 * 
 * The SDK is built with a core C++ implementation and exposed through a stable, 
 * opaque-pointer C-API. This architecture ensures ABI stability across compiler 
 * versions and allows the SDK to be easily wrapped by external languages such 
 * as C#, Python, or Rust.
 * 
 * @subsection cpp_api C++ API
 * The internal C++ API provides the heavy lifting. All classes reside within 
 * the `pmsdk::` namespace. 
 * - **Geometry**: `pmsdk::Geometry::Mesh`, `DynamicMesh`, `BezierPatch`, `GridWarp`
 * - **Warp**: `pmsdk::Warp::WarpNode`, `Projector`, `DeformationField`
 * - **Blend**: `pmsdk::Blend::BlendConfig`, `EdgeBlend`
 * - **Calibration**: `pmsdk::Calibration::Calibrator`, `GrayCode`
 * - **Serialization**: `pmsdk::Serialization::*`
 * 
 * @subsection c_api C API
 * The public interface is strictly C-linkage. Objects are managed via opaque 
 * handles (e.g., `pmsdk_mesh_t`, `pmsdk_warpnode_t`).
 * - **Lifecycle**: All handles are created with `*_create()` and must be explicitly 
 *   destroyed with `*_destroy()`.
 * - **Error Handling**: All C-API functions return a `pmsdk_status_t` enum. If a 
 *   function fails, it will return a non-zero error code. C++ exceptions are caught 
 *   at the C boundary and translated into these codes.
 * 
 * @section usage_sec General Usage
 * 
 * To use the SDK in a C application:
 * 
 * ```c
 * #include <PMSDK/PMSDK_C.h>
 * 
 * int main() {
 *     pmsdk_mesh_t* mesh = pmsdk_mesh_create();
 *     
 *     // Populate mesh vertices/indices...
 *     // pmsdk_mesh_set_vertices(mesh, verts, vert_count);
 *     
 *     pmsdk_warpnode_t* warp_node = pmsdk_warpnode_create();
 *     pmsdk_mesh_t* out_mesh = pmsdk_mesh_create();
 *     
 *     // Process mesh through warp hierarchy
 *     if (pmsdk_warpnode_process_mesh(warp_node, mesh, out_mesh) == PMSDK_SUCCESS) {
 *         // Success!
 *     }
 *     
 *     // Cleanup
 *     pmsdk_warpnode_destroy(warp_node);
 *     pmsdk_mesh_destroy(out_mesh);
 *     pmsdk_mesh_destroy(mesh);
 *     
 *     return 0;
 * }
 * ```
 */
