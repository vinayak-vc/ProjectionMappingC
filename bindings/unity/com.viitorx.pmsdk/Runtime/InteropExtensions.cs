using System.Runtime.CompilerServices;
using UnityEngine;

namespace vxpmsdk
{
    public static class InteropExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static pmsdk_vec2_t ToNative(this Vector2 v)
        {
            return new pmsdk_vec2_t { x = v.x, y = v.y };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static pmsdk_vec3_t ToNative(this Vector3 v)
        {
            return new pmsdk_vec3_t { x = v.x, y = v.y, z = v.z };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static pmsdk_vec4_t ToNative(this Color c)
        {
            return new pmsdk_vec4_t { x = c.r, y = c.g, z = c.b, w = c.a };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ToUnity(this pmsdk_vec2_t v)
        {
            return new Vector2(v.x, v.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ToUnity(this pmsdk_vec3_t v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ToUnity(this pmsdk_vec4_t v)
        {
            return new Color(v.x, v.y, v.z, v.w);
        }
    }
}
