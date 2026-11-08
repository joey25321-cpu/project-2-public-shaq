using System.Collections.Generic;
using UnityEngine;

namespace DeadMiles
{
    /// <summary>Mesh helpers: cached primitive meshes and generated rock meshes.</summary>
    public static class Geo
    {
        static readonly Dictionary<PrimitiveType, Mesh> prims = new Dictionary<PrimitiveType, Mesh>();
        static Mesh[] rocks;

        public static Mesh Prim(PrimitiveType t)
        {
            if (prims.TryGetValue(t, out var m) && m != null) return m;
            var go = GameObject.CreatePrimitive(t);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            prims[t] = m;
            return m;
        }

        /// <summary>Creates a visual-only (no collider) mesh object.</summary>
        public static GameObject Part(Transform parent, Mesh mesh, Material mat, Vector3 localPos, Vector3 localScale,
            Quaternion localRot, string name = "part", bool shadows = true)
        {
            var go = new GameObject(name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = localRot;
            t.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Part(Transform parent, PrimitiveType type, Material mat, Vector3 localPos,
            Vector3 localScale, Quaternion localRot, string name = "part", bool shadows = true) =>
            Part(parent, Prim(type), mat, localPos, localScale, localRot, name, shadows);

        public static GameObject Box(Transform parent, Material mat, Vector3 pos, Vector3 size, Vector3 euler = default, string name = "box") =>
            Part(parent, PrimitiveType.Cube, mat, pos, size, Quaternion.Euler(euler), name);

        /// <summary>A few noise-displaced spheres used for rocks and boulders.</summary>
        public static Mesh Rock(int variant)
        {
            if (rocks == null)
            {
                rocks = new Mesh[4];
                var src = Prim(PrimitiveType.Sphere);
                for (int i = 0; i < rocks.Length; i++)
                {
                    var verts = src.vertices;
                    float seed = 13.7f * (i + 1);
                    for (int v = 0; v < verts.Length; v++)
                    {
                        var p = verts[v].normalized;
                        float n = Mathf.PerlinNoise(p.x * 1.7f + seed, p.y * 1.7f + p.z * 1.3f + seed) * 0.55f
                                + Mathf.PerlinNoise(p.x * 4.1f + seed, p.z * 4.1f - p.y + seed) * 0.2f;
                        var q = p * (0.35f + n);
                        q.y *= 0.65f; // flatter boulders
                        if (q.y < -0.15f) q.y = -0.15f + (q.y + 0.15f) * 0.3f; // flat-ish bottom
                        verts[v] = q;
                    }
                    var m = new Mesh { name = "rock" + i, vertices = verts, triangles = src.triangles, uv = src.uv };
                    m.RecalculateNormals();
                    m.RecalculateTangents();
                    m.RecalculateBounds();
                    rocks[i] = m;
                }
            }
            return rocks[Mathf.Abs(variant) % rocks.Length];
        }
    }
}
