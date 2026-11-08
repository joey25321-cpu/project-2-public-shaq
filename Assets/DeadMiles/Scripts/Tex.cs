using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadMiles
{
    /// <summary>
    /// Procedurally generated, seamless (tileable) textures and normal maps.
    /// No image files are needed for the prototype; swap these for photo-scanned
    /// textures later for even more realism.
    /// </summary>
    public static class Tex
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        // ---------- noise ----------

        static float Perlin(float x, float y, float period, float seed)
        {
            // Seamless noise: blend four offset copies so the texture wraps.
            float u = x / period, v = y / period;
            float a = Mathf.PerlinNoise(x + seed, y + seed);
            float b = Mathf.PerlinNoise(x - period + seed, y + seed);
            float c = Mathf.PerlinNoise(x + seed, y - period + seed);
            float d = Mathf.PerlinNoise(x - period + seed, y - period + seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Tileable fractal noise. u,v in 0..1; baseFreq must be an integer for seamless output.</summary>
        public static float Fbm(float u, float v, int baseFreq, int octaves, float seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int f = baseFreq;
            for (int i = 0; i < octaves; i++)
            {
                sum += Perlin(u * f, v * f, f, seed + i * 17.31f) * amp;
                norm += amp;
                amp *= gain;
                f *= 2;
            }
            return sum / norm;
        }

        // ---------- builders ----------

        static Texture2D Build(string key, int size, bool linear, Func<float, float, Color> f)
        {
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                name = key,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            var px = new Color[size * size];
            float inv = 1f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = f(x * inv, y * inv);
            t.SetPixels(px);
            t.Apply(true, true);
            cache[key] = t;
            return t;
        }

        static Texture2D NormalFromHeight(string key, int size, float strength, Func<float, float, float> h)
        {
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            var hm = new float[size * size];
            float inv = 1f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    hm[y * size + x] = h(x * inv, y * inv);

            t = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = key,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float l = hm[y * size + (x - 1 + size) % size];
                    float r = hm[y * size + (x + 1) % size];
                    float d = hm[((y - 1 + size) % size) * size + x];
                    float u = hm[((y + 1) % size) * size + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    // RGB = xyz, A = 1 works with both DXT5nm (AG) and RGB unpacking.
                    px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            t.SetPixels(px);
            t.Apply(true, true);
            cache[key] = t;
            return t;
        }

        static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));

        // ---------- sand ----------

        static float SandHeight(float u, float v)
        {
            float ripple = Mathf.Sin((u * 12f + Fbm(u, v, 3, 3, 4.2f) * 3f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            return ripple * 0.35f + Fbm(u, v, 16, 3, 1.3f) * 0.65f;
        }

        public static Texture2D Sand() => Build("sand", 256, false, (u, v) =>
        {
            float n = Fbm(u, v, 4, 4, 2.1f);
            float g = Fbm(u, v, 32, 2, 9.7f);
            var c = Mix(new Color(0.66f, 0.52f, 0.36f), new Color(0.82f, 0.68f, 0.48f), n * 1.4f - 0.2f);
            c = Mix(c, new Color(0.45f, 0.36f, 0.27f), (g - 0.62f) * 4f);
            return c * (0.9f + SandHeight(u, v) * 0.15f);
        });

        public static Texture2D SandNormal() => NormalFromHeight("sandN", 256, 6f, SandHeight);

        // ---------- dirt road ----------

        static float DirtHeight(float u, float v) => Fbm(u, v, 8, 4, 3.3f) + Fbm(u, v, 48, 2, 7.1f) * 0.4f;

        public static Texture2D Dirt() => Build("dirt", 256, false, (u, v) =>
        {
            float n = Fbm(u, v, 4, 4, 5.5f);
            float pebbles = Fbm(u, v, 40, 2, 12.2f);
            // two tyre tracks across v
            float track = Mathf.Exp(-Mathf.Pow((v - 0.33f) * 9f, 2f)) + Mathf.Exp(-Mathf.Pow((v - 0.67f) * 9f, 2f));
            var c = Mix(new Color(0.36f, 0.29f, 0.22f), new Color(0.52f, 0.42f, 0.31f), n * 1.5f - 0.25f);
            c = Mix(c, new Color(0.27f, 0.22f, 0.17f), track * 0.45f);
            c = Mix(c, new Color(0.62f, 0.55f, 0.45f), (pebbles - 0.66f) * 5f);
            return c;
        });

        public static Texture2D DirtNormal() => NormalFromHeight("dirtN", 256, 5f, DirtHeight);

        // ---------- rock ----------

        static float RockHeight(float u, float v) => Fbm(u, v, 4, 5, 8.8f, 0.55f);

        public static Texture2D Rock() => Build("rock", 256, false, (u, v) =>
        {
            float h = RockHeight(u, v);
            float strata = Mathf.Sin((v * 9f + h * 2f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            var c = Mix(new Color(0.33f, 0.28f, 0.24f), new Color(0.6f, 0.5f, 0.41f), h * 1.6f - 0.3f);
            return c * (0.85f + strata * 0.2f);
        });

        public static Texture2D RockNormal() => NormalFromHeight("rockN", 256, 9f, RockHeight);

        // ---------- painted, rusted metal ----------

        public static Texture2D RustPaint() => Build("rustpaint", 256, false, (u, v) =>
        {
            float rust = Fbm(u, v, 4, 5, 21.4f);
            float dirt = Fbm(u, v, 2, 3, 30.1f);
            float scratch = Fbm(u * 0.2f, v, 64, 1, 3.3f);
            var paint = new Color(1f, 1f, 1f);
            var rustC = Mix(new Color(0.32f, 0.15f, 0.07f), new Color(0.55f, 0.27f, 0.1f), Fbm(u, v, 16, 2, 2.2f));
            var c = Mix(paint, rustC, (rust - 0.55f) * 6f);
            c = Mix(c, new Color(0.45f, 0.38f, 0.3f), (dirt - 0.5f) * 1.2f + (1f - v) * 0.3f);
            c = Mix(c, new Color(0.7f, 0.7f, 0.68f), (scratch - 0.8f) * 8f);
            return c;
        });

        public static Texture2D RustPaintNormal() =>
            NormalFromHeight("rustpaintN", 256, 3f, (u, v) => Mathf.Clamp01((Fbm(u, v, 4, 5, 21.4f) - 0.5f) * 4f));

        public static Texture2D DarkMetal() => Build("darkmetal", 128, false, (u, v) =>
        {
            float n = Fbm(u, v, 8, 4, 41.1f);
            float g = 0.16f + n * 0.12f;
            return new Color(g, g * 0.97f, g * 0.94f);
        });

        // ---------- organics ----------

        public static Texture2D Skin() => Build("skin", 128, false, (u, v) =>
        {
            float n = Fbm(u, v, 4, 4, 51.2f);
            float vein = Fbm(u, v, 16, 3, 13.4f);
            var c = Mix(new Color(0.4f, 0.45f, 0.33f), new Color(0.62f, 0.62f, 0.5f), n * 1.4f - 0.2f);
            c = Mix(c, new Color(0.35f, 0.12f, 0.1f), (vein - 0.68f) * 5f);
            return c;
        });

        public static Texture2D Cloth() => Build("cloth", 128, false, (u, v) =>
        {
            float weave = (Mathf.Sin(u * 128f * Mathf.PI) * Mathf.Sin(v * 128f * Mathf.PI)) * 0.06f;
            float stain = Fbm(u, v, 4, 4, 61.7f);
            float c = 0.85f + weave - Mathf.Clamp01((stain - 0.55f) * 3f) * 0.45f;
            return new Color(c, c * 0.97f, c * 0.93f);
        });

        public static Texture2D Wood() => Build("wood", 128, false, (u, v) =>
        {
            float grain = Mathf.Sin((v * 24f + Fbm(u, v, 4, 3, 71.3f) * 6f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            var c = Mix(new Color(0.36f, 0.25f, 0.15f), new Color(0.58f, 0.43f, 0.28f), grain);
            return c * (0.8f + Fbm(u, v, 2, 3, 2.9f) * 0.3f);
        });

        /// <summary>Soft round sprite for dust / smoke / blood particles.</summary>
        public static Texture2D Soft() => Build("soft", 64, false, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a);
            return new Color(1f, 1f, 1f, a);
        });
    }
}
