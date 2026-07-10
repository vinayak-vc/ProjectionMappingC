#pragma once

// Umbrella header for the ProjectionMappingSDK C++ convenience API.
// Native consumers may include this; bindings should use PMSDK/CAPI (Milestone 10).

#include "PMSDK/Core/Config.h"
#include "PMSDK/Core/Context.h"
#include "PMSDK/Core/ErrorCode.h"
#include "PMSDK/Core/Export.h"
#include "PMSDK/Core/Log.h"
#include "PMSDK/Core/Status.h"
#include "PMSDK/Core/Version.h"

#include "PMSDK/Math/Vector2.h"
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Vector4.h"
#include "PMSDK/Math/Quaternion.h"
#include "PMSDK/Math/Matrix4.h"
#include "PMSDK/Math/Transform.h"
#include "PMSDK/Math/BoundingBox.h"
#include "PMSDK/Math/Ray.h"
#include "PMSDK/Math/Plane.h"

#include "PMSDK/Geometry/Vertex.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Geometry/MeshBuilder.h"
#include "PMSDK/Geometry/MeshSubdivision.h"
#include "PMSDK/Geometry/Intersection.h"
#include "PMSDK/Geometry/BVH.h"
#include "PMSDK/Geometry/KDTree.h"
#include "PMSDK/Geometry/BezierCurve.h"
#include "PMSDK/Geometry/Spline.h"
#include "PMSDK/Geometry/UVMapping.h"
