using System.Collections.Generic;
using UnityEngine;

namespace DeadMiles
{
    /// <summary>
    /// Streams the desert highway in 50 m chunks: terrain meshes + collider, the dirt road,
    /// rocks, dead trees, poles, physics obstacles (crates, barrels, wrecks, breakable
    /// barricades), fuel cans, walkers and distant mountains. The world is deterministic,
    /// so players learn the route run after run, like a level.
    /// </summary>
    public class WorldGen : MonoBehaviour
    {
        public const float ChunkLen = 50f;
        public const float Goal = 2000f;
        const float Step = 0.5f;
        const float RoadHalf = 2.4f;
        const float FarLen = 200f;

        readonly Dictionary<int, GameObject> chunks = new Dictionary<int, GameObject>();
        readonly Dictionary<int, GameObject> farChunks = new Dictionary<int, GameObject>();

        Material sand, road, rock, wood, rust, wreckMat, poleMat, mountain, fuelMat, flagMat, stripeMat;

        void Awake()
        {
            sand = Mats.Lit("sand", Color.white, 0.12f, 0f, Tex.Sand(), Tex.SandNormal(), 1f);
            road = Mats.Lit("road", Color.white, 0.18f, 0f, Tex.Dirt(), Tex.DirtNormal(), 1f);
            rock = Mats.Lit("rock", Color.white, 0.2f, 0f, Tex.Rock(), Tex.RockNormal(), 1.2f);
            wood = Mats.Lit("wood", Color.white, 0.2f, 0f, Tex.Wood());
            rust = Mats.Lit("barrel", new Color(0.5f, 0.32f, 0.2f), 0.35f, 0.5f, Tex.RustPaint(), Tex.RustPaintNormal(), 0.8f);
            wreckMat = Mats.Lit("wreck", new Color(0.36f, 0.4f, 0.42f), 0.35f, 0.4f, Tex.RustPaint(), Tex.RustPaintNormal(), 0.8f);
            poleMat = Mats.Lit("pole", new Color(0.35f, 0.27f, 0.2f), 0.15f, 0f, Tex.Wood());
            mountain = Mats.Lit("mountain", new Color(0.78f, 0.66f, 0.56f), 0.05f, 0f, Tex.Rock(), Tex.RockNormal(), 0.6f);
            fuelMat = Mats.Lit("fuelcan", new Color(0.75f, 0.06f, 0.04f), 0.55f, 0.3f);
            flagMat = Mats.Emissive("flag", new Color(0.1f, 0.6f, 0.15f), new Color(0.1f, 0.9f, 0.2f));
            stripeMat = Mats.Lit("stripe", new Color(0.85f, 0.75f, 0.1f), 0.4f, 0.1f);
        }

        // ------------------------------------------------------------------ terrain function

        /// <summary>Road surface height at world x.</summary>
        public static float Height(float x)
        {
            if (x < 0f) return 0f;
            float ramp = Mathf.Clamp01((x - 40f) / 140f);
            float diff = Mathf.Clamp01(x / Goal);
            float h = (Mathf.PerlinNoise(x * 0.0032f + 10f, 0.37f) - 0.5f) * 46f
                    + (Mathf.PerlinNoise(x * 0.014f + 3f, 1.7f) - 0.5f) * (7f + 9f * diff)
                    + (Mathf.PerlinNoise(x * 0.07f, 5.1f) - 0.5f) * 1.1f;
            // subtract the value at the start so the road begins at 0 and eases in
            float h0 = (Mathf.PerlinNoise(10f, 0.37f) - 0.5f) * 46f + (Mathf.PerlinNoise(3f, 1.7f) - 0.5f) * 7f
                     + (Mathf.PerlinNoise(0f, 5.1f) - 0.5f) * 1.1f;
            return (h - h0) * ramp + Ramp(x);
        }

        const float RampStart = 160f, RampPeriod = 210f, RampLen = 18f, RampDrop = 1.3f;

        static float Ramp(float x)
        {
            if (x < RampStart || x > Goal - 60f) return 0f;
            float local = (x - RampStart) % RampPeriod;
            float height = 3.2f + 2.5f * Mathf.Clamp01(x / Goal);
            if (local < RampLen) { float u = local / RampLen; return height * u * u; }
            if (local < RampLen + RampDrop) return height * (1f - (local - RampLen) / RampDrop);
            return 0f;
        }

        public static bool NearRamp(float x)
        {
            if (x < RampStart - 6f) return false;
            float local = (x - RampStart + 6f) % RampPeriod;
            return local < RampLen + RampDrop + 14f;
        }

        /// <summary>Ground height including the decorative strips in front of and behind the road.</summary>
        public static float GroundHeight(float x, float z)
        {
            float h = Height(x);
            float az = Mathf.Abs(z);
            if (az <= 3f) return h;
            if (z < 0f)
            {
                // embankment falling toward the camera
                return h - (az - 3f) * 0.45f + (Mathf.PerlinNoise(x * 0.05f, z * 0.1f) - 0.5f) * 1.5f;
            }
            float k = Mathf.Clamp01((z - 3f) / 18f);
            float dunes = (Mathf.PerlinNoise(x * 0.025f + 50f, z * 0.04f) - 0.4f) * 9f
                        + (Mathf.PerlinNoise(x * 0.09f, z * 0.13f + 9f) - 0.5f) * 1.5f;
            return h + dunes * k + (z - 3f) * 0.06f;
        }

        static float MountainHeight(float x, float z)
        {
            float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.004f + 7f, z * 0.004f) * 2f - 1f);
            float m = ridge * ridge * 110f + Mathf.PerlinNoise(x * 0.015f, z * 0.02f + 3f) * 25f;
            float k = Mathf.Clamp01((z - 90f) / 120f);
            return Height(Mathf.Max(0f, x)) * 0.3f - 10f + m * k;
        }

        // ------------------------------------------------------------------ streaming

        public void ResetWorld()
        {
            foreach (var c in chunks.Values) if (c != null) Destroy(c);
            chunks.Clear();
            Stream(0f);
        }

        public void Stream(float x)
        {
            int a = Mathf.FloorToInt((x - 110f) / ChunkLen);
            int b = Mathf.FloorToInt((x + 190f) / ChunkLen);
            a = Mathf.Max(a, -3);
            for (int c = a; c <= b; c++)
                if (!chunks.ContainsKey(c)) chunks[c] = BuildChunk(c);

            var remove = new List<int>();
            foreach (var k in chunks.Keys) if (k < a || k > b + 2) remove.Add(k);
            foreach (var k in remove) { if (chunks[k] != null) Destroy(chunks[k]); chunks.Remove(k); }

            int fa = Mathf.FloorToInt((x - 450f) / FarLen), fb = Mathf.FloorToInt((x + 650f) / FarLen);
            for (int c = fa; c <= fb; c++)
                if (!farChunks.ContainsKey(c)) farChunks[c] = BuildFar(c);
            remove.Clear();
            foreach (var k in farChunks.Keys) if (k < fa || k > fb) remove.Add(k);
            foreach (var k in remove) { if (farChunks[k] != null) Destroy(farChunks[k]); farChunks.Remove(k); }
        }

        // ------------------------------------------------------------------ chunk building

        GameObject BuildChunk(int ci)
        {
            var go = new GameObject("Chunk " + ci);
            go.transform.SetParent(transform, false);
            float x0 = ci * ChunkLen, x1 = x0 + ChunkLen;

            float[] front = { -24f, -16f, -10f, -6f, -RoadHalf };
            float[] back = { RoadHalf, 4.5f, 8f, 13f, 20f, 30f, 42f, 58f, 80f };
            MakeGrid(go.transform, "groundFront", x0, x1, Step, front, sand, 0.11f, false);
            MakeGrid(go.transform, "groundBack", x0, x1, Step, back, sand, 0.11f, false);
            MakeGrid(go.transform, "road", x0, x1, Step, new[] { -RoadHalf, RoadHalf }, road, 0f, true);
            MakeCollider(go.transform, x0, x1);

            var rng = new System.Random(ci * 7919 + 1301);
            Decorate(go.transform, ci, x0, rng);
            if (ci >= 1) Populate(go.transform, ci, x0, rng);
            if (Goal >= x0 && Goal < x1) BuildGoal(go.transform);
            if (ci == 0) BuildStartSign(go.transform);
            return go;
        }

        void MakeGrid(Transform parent, string name, float x0, float x1, float step, float[] zs, Material mat, float uvScale, bool isRoad)
        {
            int nx = Mathf.RoundToInt((x1 - x0) / step) + 1;
            int nz = zs.Length;
            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = x0 + ix * step;
                    float z = zs[iz];
                    float y = isRoad ? Height(x) : GroundHeight(x, z);
                    verts[iz * nx + ix] = new Vector3(x, y, z);
                    uvs[iz * nx + ix] = isRoad
                        ? new Vector2(x / 7f, (z + RoadHalf) / (RoadHalf * 2f))
                        : new Vector2(x * uvScale, z * uvScale);
                }
            }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int iz = 0; iz < nz - 1; iz++)
            {
                for (int ix = 0; ix < nx - 1; ix++)
                {
                    int a = iz * nx + ix, b = a + 1, c = a + nx, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void MakeCollider(Transform parent, float x0, float x1)
        {
            int nx = Mathf.RoundToInt((x1 - x0) / Step) + 1;
            float[] zs = { -9f, 9f };
            var verts = new Vector3[nx * 2];
            for (int iz = 0; iz < 2; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = x0 + ix * Step;
                    verts[iz * nx + ix] = new Vector3(x, Height(x), zs[iz]);
                }
            var tris = new int[(nx - 1) * 6];
            int t = 0;
            for (int ix = 0; ix < nx - 1; ix++)
            {
                int a = ix, b = ix + 1, c = nx + ix, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
            var mesh = new Mesh { name = "col", vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            var go = new GameObject("collider");
            go.transform.SetParent(parent, false);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            Compat.SetFriction(mc, 0.9f);
        }

        GameObject BuildFar(int fi)
        {
            var go = new GameObject("Far " + fi);
            go.transform.SetParent(transform, false);
            float x0 = fi * FarLen, x1 = x0 + FarLen;
            float[] zs = { 75f, 110f, 160f, 230f, 320f, 420f };
            int nx = Mathf.RoundToInt(FarLen / 4f) + 1;
            var verts = new Vector3[nx * zs.Length];
            var uvs = new Vector2[verts.Length];
            for (int iz = 0; iz < zs.Length; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = x0 + ix * 4f, z = zs[iz];
                    float y = iz == 0 ? GroundHeight(x, 80f) - 0.5f : MountainHeight(x, z);
                    verts[iz * nx + ix] = new Vector3(x, y, z);
                    uvs[iz * nx + ix] = new Vector2(x * 0.02f, y * 0.02f);
                }
            var tris = new int[(nx - 1) * (zs.Length - 1) * 6];
            int t = 0;
            for (int iz = 0; iz < zs.Length - 1; iz++)
                for (int ix = 0; ix < nx - 1; ix++)
                {
                    int a = iz * nx + ix, b = a + 1, c = a + nx, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            var mesh = new Mesh { name = "far", vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mountain;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ scenery

        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        void Decorate(Transform parent, int ci, float x0, System.Random rng)
        {
            // boulders behind the road
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                float x = x0 + R(rng, 0f, ChunkLen);
                float z = R(rng, 5f, 55f);
                float s = R(rng, 0.6f, 1.5f) * (1f + z * 0.06f);
                var p = new Vector3(x, GroundHeight(x, z) - s * 0.12f, z);
                Geo.Part(parent, Geo.Rock(rng.Next(4)), rock, p, new Vector3(s * R(rng, 0.8f, 1.6f), s, s * R(rng, 0.8f, 1.3f)),
                    Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), "rock");
            }
            // a few small rocks in the foreground
            for (int i = 0; i < 3; i++)
            {
                float x = x0 + R(rng, 0f, ChunkLen);
                float z = R(rng, -14f, -4f);
                float s = R(rng, 0.3f, 1.1f);
                var p = new Vector3(x, GroundHeight(x, z) - s * 0.1f, z);
                Geo.Part(parent, Geo.Rock(rng.Next(4)), rock, p, Vector3.one * s, Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), "rockF");
            }
            // dead trees
            for (int i = 0; i < 2; i++)
            {
                float x = x0 + R(rng, 0f, ChunkLen);
                float z = R(rng, 7f, 40f);
                DeadTree(parent, new Vector3(x, GroundHeight(x, z) - 0.2f, z), R(rng, 0.8f, 1.6f), rng);
            }
            // telegraph poles every 40 m
            float first = Mathf.Ceil(x0 / 40f) * 40f;
            for (float x = first; x < x0 + ChunkLen; x += 40f)
            {
                var p = new Vector3(x, GroundHeight(x, 6.5f) - 0.3f, 6.5f);
                Geo.Part(parent, PrimitiveType.Cylinder, poleMat, p + Vector3.up * 4f, new Vector3(0.25f, 4f, 0.25f), Quaternion.Euler(0f, 0f, R(rng, -3f, 3f)), "pole");
                Geo.Box(parent, poleMat, p + Vector3.up * 7.3f, new Vector3(0.15f, 0.15f, 2.2f), default, "crossbar");
            }
        }

        void DeadTree(Transform parent, Vector3 p, float s, System.Random rng)
        {
            var t = new GameObject("tree").transform;
            t.SetParent(parent, false);
            t.position = p;
            t.localScale = Vector3.one * s;
            t.rotation = Quaternion.Euler(0f, R(rng, 0f, 360f), 0f);
            var bark = Mats.Lit("bark", new Color(0.3f, 0.24f, 0.19f), 0.1f, 0f, Tex.Wood());
            Geo.Part(t, PrimitiveType.Cylinder, bark, new Vector3(0f, 1.6f, 0f), new Vector3(0.28f, 1.6f, 0.28f), Quaternion.Euler(R(rng, -4f, 4f), 0f, R(rng, -6f, 6f)), "trunk");
            int branches = 2 + rng.Next(3);
            for (int i = 0; i < branches; i++)
            {
                float h = R(rng, 1.6f, 3f);
                var rot = Quaternion.Euler(R(rng, -60f, 60f), R(rng, 0f, 360f), R(rng, 30f, 65f));
                Geo.Part(t, PrimitiveType.Cylinder, bark, new Vector3(0f, h, 0f) + rot * Vector3.up * 0.6f,
                    new Vector3(0.12f, 0.65f, 0.12f), rot, "branch");
            }
        }

        void BuildStartSign(Transform parent)
        {
            var post = Mats.Lit("signPost", new Color(0.5f, 0.5f, 0.5f), 0.4f, 0.8f);
            var board = Mats.Lit("signBoard", new Color(0.12f, 0.35f, 0.18f), 0.4f, 0.2f);
            Geo.Box(parent, post, new Vector3(14f, 1.6f, 3.6f), new Vector3(0.12f, 3.2f, 0.12f));
            Geo.Box(parent, board, new Vector3(14f, 3f, 3.55f), new Vector3(2.6f, 1.2f, 0.06f));
        }

        void BuildGoal(Transform parent)
        {
            float x = Goal;
            float y = Height(x);
            var metal = Mats.Lit("goalMetal", new Color(0.3f, 0.32f, 0.3f), 0.4f, 0.8f, Tex.DarkMetal());
            Geo.Box(parent, metal, new Vector3(x, y + 3f, -3.5f), new Vector3(0.4f, 6f, 0.4f), default, "gateL");
            Geo.Box(parent, metal, new Vector3(x, y + 3f, 3.5f), new Vector3(0.4f, 6f, 0.4f), default, "gateR");
            Geo.Box(parent, stripeMat, new Vector3(x, y + 6.2f, 0f), new Vector3(0.5f, 0.8f, 7.6f), default, "banner");
            Geo.Box(parent, flagMat, new Vector3(x + 0.3f, y + 7.5f, 3.5f), new Vector3(0.05f, 0.8f, 1.2f), default, "flag");
            // sandbag walls & a helipad pad beyond the gate
            var bag = Mats.Lit("sandbag", new Color(0.55f, 0.5f, 0.38f), 0.1f, 0f, Tex.Cloth());
            for (int i = 0; i < 6; i++)
                Geo.Box(parent, bag, new Vector3(x + 2f + i * 0.9f, GroundHeight(x + 2f + i * 0.9f, 4.5f) + 0.3f, 4.5f), new Vector3(0.85f, 0.6f, 0.6f), default, "bag");
            var pad = Mats.Lit("helipad", new Color(0.25f, 0.25f, 0.25f), 0.3f, 0f);
            Geo.Part(parent, PrimitiveType.Cylinder, pad, new Vector3(x + 30f, Height(x + 30f) + 0.05f, 9f), new Vector3(14f, 0.05f, 14f), Quaternion.identity, "pad");
        }

        // ------------------------------------------------------------------ gameplay objects

        void Populate(Transform parent, int ci, float x0, System.Random rng)
        {
            float diff = Mathf.Clamp01(x0 / Goal);
            bool pastGoal = x0 > Goal;

            // walkers
            int walkers = pastGoal ? 0 : 2 + Mathf.RoundToInt(diff * 9f) + (ci < 2 ? 0 : rng.Next(3));
            for (int i = 0; i < walkers; i++)
            {
                float x = x0 + R(rng, 2f, ChunkLen - 2f);
                float z = R(rng, -1.6f, 1.6f);
                Zombie.Spawn(parent, new Vector3(x, Height(x), z), rng);
            }
            // horde
            if (!pastGoal && ci >= 2 && rng.NextDouble() < 0.25f + diff * 0.45f)
            {
                float hx = x0 + R(rng, 8f, ChunkLen - 8f);
                int size = 4 + rng.Next(5 + Mathf.RoundToInt(diff * 8f));
                for (int i = 0; i < size; i++)
                {
                    float x = hx + R(rng, -4f, 4f);
                    Zombie.Spawn(parent, new Vector3(x, Height(x), R(rng, -1.7f, 1.7f)), rng);
                }
            }

            if (ci < 2 || pastGoal) return;

            // obstacles
            int obstacles = 1 + rng.Next(2 + Mathf.RoundToInt(diff * 2f));
            for (int i = 0; i < obstacles; i++)
            {
                float x = x0 + R(rng, 4f, ChunkLen - 4f);
                if (NearRamp(x)) continue;
                double roll = rng.NextDouble();
                if (roll < 0.32) CrateStack(parent, x, rng);
                else if (roll < 0.58) Barrels(parent, x, rng);
                else if (roll < 0.8) Wreck(parent, x, rng);
                else Barricade(parent, x);
            }

            // fuel can
            if (ci >= 3 && rng.NextDouble() < 0.3)
            {
                float x = x0 + R(rng, 5f, ChunkLen - 5f);
                if (!NearRamp(x)) FuelCan.Spawn(parent, new Vector3(x, Height(x) + 0.9f, 0f), fuelMat);
            }
        }

        static Rigidbody Dynamic(GameObject go, float mass, float friction)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.SetDamping(0.05f, 0.2f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var col = go.GetComponent<Collider>();
            if (col != null) Compat.SetFriction(col, friction);
            rb.Sleep();
            return rb;
        }

        static Quaternion SlopeRot(float x)
        {
            float a = Mathf.Atan2(Height(x + 0.5f) - Height(x - 0.5f), 1f) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, a);
        }

        void CrateStack(Transform parent, float x, System.Random rng)
        {
            int count = 3 + rng.Next(4);
            float s = 0.85f;
            int row = 0, inRow = 0, perRow = 3;
            for (int i = 0; i < count; i++)
            {
                if (inRow >= perRow) { row++; inRow = 0; perRow = Mathf.Max(1, perRow - 1); }
                float px = x + (inRow - (perRow - 1) * 0.5f) * (s + 0.02f);
                var p = new Vector3(px, Height(px) + s * 0.5f + row * (s + 0.01f) + 0.02f, R(rng, -0.6f, 0.6f));
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "crate";
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(p, SlopeRot(px));
                go.transform.localScale = Vector3.one * s;
                go.GetComponent<MeshRenderer>().sharedMaterial = wood;
                Dynamic(go, 25f, 0.6f);
                inRow++;
            }
        }

        void Barrels(Transform parent, float x, System.Random rng)
        {
            int count = 2 + rng.Next(3);
            for (int i = 0; i < count; i++)
            {
                float px = x + i * 0.75f;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "barrel";
                go.transform.SetParent(parent, false);
                go.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f);
                go.transform.SetPositionAndRotation(new Vector3(px, Height(px) + 0.47f, R(rng, -1.2f, 1.2f)), SlopeRot(px));
                go.GetComponent<MeshRenderer>().sharedMaterial = rust;
                // primitive cylinders come with a capsule collider; swap for a box-ish convex fit
                Object.DestroyImmediate(go.GetComponent<Collider>());
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = Geo.Prim(PrimitiveType.Cylinder);
                mc.convex = true;
                Dynamic(go, 45f, 0.5f);
                Geo.Part(go.transform, PrimitiveType.Cylinder, stripeMat, Vector3.up * 0.4f, new Vector3(1.01f, 0.06f, 1.01f), Quaternion.identity, "ring");
            }
        }

        void Wreck(Transform parent, float x, System.Random rng)
        {
            var go = new GameObject("wreck");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(x, Height(x) + 0.75f, R(rng, -0.4f, 0.4f)),
                SlopeRot(x) * Quaternion.Euler(0f, rng.NextDouble() < 0.5 ? 0f : 180f, rng.NextDouble() < 0.15 ? 180f : 0f));
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.05f, 0f);
            box.size = new Vector3(4.1f, 1.2f, 1.8f);
            var dark = Mats.Lit("burnt", new Color(0.12f, 0.11f, 0.1f), 0.2f, 0.3f, Tex.DarkMetal());
            var glass = Mats.Lit("carGlass", new Color(0.05f, 0.07f, 0.08f), 0.95f, 0.1f);
            Geo.Box(go.transform, wreckMat, new Vector3(0f, 0f, 0f), new Vector3(4.1f, 0.75f, 1.8f), default, "body");
            Geo.Box(go.transform, wreckMat, new Vector3(-0.2f, 0.62f, 0f), new Vector3(2.0f, 0.5f, 1.6f), default, "cab");
            Geo.Box(go.transform, glass, new Vector3(-0.2f, 0.64f, 0f), new Vector3(1.85f, 0.4f, 1.64f), default, "glass");
            foreach (float wx in new[] { -1.35f, 1.35f })
                foreach (float wz in new[] { -0.82f, 0.82f })
                    Geo.Part(go.transform, PrimitiveType.Cylinder, dark, new Vector3(wx, -0.38f, wz), new Vector3(0.7f, 0.12f, 0.7f), Quaternion.Euler(90f, 0f, 0f), "tyre");
            Dynamic(go, 950f, 0.6f);
        }

        void Barricade(Transform parent, float x)
        {
            float y = Height(x);
            // posts (static) sit outside the truck's path, planks are breakable rigidbodies
            Geo.Box(parent, poleMat, new Vector3(x, y + 0.9f, -1.85f), new Vector3(0.2f, 1.8f, 0.2f), default, "post");
            Geo.Box(parent, poleMat, new Vector3(x, y + 0.9f, 1.85f), new Vector3(0.2f, 1.8f, 0.2f), default, "post");
            for (int i = 0; i < 4; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "plank";
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(x - 0.15f, y + 0.35f + i * 0.38f, 0f);
                go.transform.rotation = Quaternion.Euler((i % 2 == 0 ? 4f : -4f), 0f, 0f);
                go.transform.localScale = new Vector3(0.08f, 0.28f, 3.8f);
                go.GetComponent<MeshRenderer>().sharedMaterial = wood;
                var rb = Dynamic(go, 14f, 0.6f);
                var j = go.AddComponent<FixedJoint>();
                j.breakForce = 9000f;
                j.breakTorque = 6000f;
                rb.WakeUp();
            }
            // warning stripes
            Geo.Box(parent, stripeMat, new Vector3(x, y + 1.85f, -1.85f), new Vector3(0.22f, 0.12f, 0.22f), default, "cap");
            Geo.Box(parent, stripeMat, new Vector3(x, y + 1.85f, 1.85f), new Vector3(0.22f, 0.12f, 0.22f), default, "cap");
        }
    }

    /// <summary>Jerry can that refills part of the tank.</summary>
    public class FuelCan : MonoBehaviour
    {
        float baseY;

        public static void Spawn(Transform parent, Vector3 pos, Material mat)
        {
            var go = new GameObject("fuel");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var dark = Mats.Lit("fuelCap", new Color(0.12f, 0.12f, 0.12f), 0.4f, 0.6f);
            Geo.Box(go.transform, mat, Vector3.zero, new Vector3(0.5f, 0.65f, 0.22f), default, "can");
            Geo.Box(go.transform, dark, new Vector3(0.12f, 0.38f, 0f), new Vector3(0.12f, 0.12f, 0.1f), default, "cap");
            Geo.Box(go.transform, mat, new Vector3(-0.08f, 0.38f, 0f), new Vector3(0.22f, 0.06f, 0.08f), default, "handle");
            var glow = new GameObject("glow").AddComponent<Light>();
            glow.transform.SetParent(go.transform, false);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.4f, 0.2f);
            glow.range = 3f;
            glow.intensity = 1.5f;
            glow.shadows = LightShadows.None;
            var t = go.AddComponent<SphereCollider>();
            t.isTrigger = true;
            t.radius = 1.4f;
            var f = go.AddComponent<FuelCan>();
            f.baseY = pos.y;
        }

        void Update()
        {
            transform.rotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
            var p = transform.position;
            p.y = baseY + Mathf.Sin(Time.time * 3f) * 0.12f;
            transform.position = p;
        }

        void OnTriggerEnter(Collider other)
        {
            var car = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<CarController>() : null;
            if (car == null || !car.released) return;
            car.AddFuel(0.25f);
            Sfx.Play(Sfx.Pickup(), 0.8f);
            GameManager.I?.OnPickup(transform.position);
            Destroy(gameObject);
        }
    }
}
