using UnityEngine;

namespace SoR.Core
{
    public static class VectorExtensions
    {
        public static Vector3 Flat(this Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }

        public static Vector3 WithY(this Vector3 v, float y)
        {
            return new Vector3(v.x, y, v.z);
        }

        public static float DistanceFlat(this Vector3 a, Vector3 other)
        {
            float dx = a.x - other.x;
            float dz = a.z - other.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
