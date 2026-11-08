using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadMiles
{
    /// <summary>
    /// Material factory. Base materials are generated as assets by the editor setup
    /// (Assets/DeadMiles/Resources/DeadMiles) so their shaders are included in the
    /// Android build. Every runtime material is a tinted copy of one of them.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> bases = new Dictionary<string, Material>();

        public static bool IsURP => GraphicsSettings.currentRenderPipeline != null;

        static Material Base(string name, string urpShader, string builtinShader)
        {
            string key = name + (IsURP ? "_URP" : "_BIRP");
            if (bases.TryGetValue(key, out var b) && b != null) return b;

            b = Resources.Load<Material>("DeadMiles/" + key);
            if (b == null)
            {
                var sh = Shader.Find(IsURP ? urpShader : builtinShader);
                if (sh == null) sh = Shader.Find(IsURP ? "Universal Render Pipeline/Lit" : "Standard");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                b = new Material(sh) { name = key };
            }
            bases[key] = b;
            return b;
        }

        static Material LitBase(bool normal) =>
            Base(normal ? "LitNormal" : "Lit", "Universal Render Pipeline/Lit", "Standard");

        public static Material Lit(string key, Color color, float smoothness, float metallic = 0f,
            Texture2D albedo = null, Texture2D normal = null, float normalStrength = 1f, float tiling = 1f)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(LitBase(normal != null)) { name = key };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (albedo != null)
            {
                SetTex(m, "_BaseMap", albedo, tiling);
                SetTex(m, "_MainTex", albedo, tiling);
            }
            else
            {
                SetTex(m, "_BaseMap", Texture2D.whiteTexture, 1f);
                SetTex(m, "_MainTex", Texture2D.whiteTexture, 1f);
            }
            if (normal != null)
            {
                SetTex(m, "_BumpMap", normal, tiling);
                m.SetFloat("_BumpScale", normalStrength);
                m.EnableKeyword("_NORMALMAP");
            }
            cache[key] = m;
            return m;
        }

        public static Material Emissive(string key, Color color, Color emission)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Base("LitEmissive", "Universal Render Pipeline/Lit", "Standard")) { name = key };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetColor("_EmissionColor", emission);
            m.SetFloat("_Smoothness", 0.8f);
            m.SetFloat("_Glossiness", 0.8f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            cache[key] = m;
            return m;
        }

        public static Material Particle(string key, Texture2D tex, Color tint)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Base("Particle", "Universal Render Pipeline/Particles/Unlit",
                "Legacy Shaders/Particles/Alpha Blended")) { name = key };
            SetTex(m, "_BaseMap", tex, 1f);
            SetTex(m, "_MainTex", tex, 1f);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_TintColor", tint);
            cache[key] = m;
            return m;
        }

        public static Material Sky()
        {
            if (cache.TryGetValue("sky", out var m) && m != null) return m;
            var b = Resources.Load<Material>("DeadMiles/Sky");
            if (b != null) m = new Material(b);
            else
            {
                var sh = Shader.Find("Skybox/Procedural");
                m = sh != null ? new Material(sh) : null;
            }
            if (m != null)
            {
                m.SetFloat("_SunSize", 0.035f);
                m.SetFloat("_SunSizeConvergence", 6f);
                m.SetFloat("_AtmosphereThickness", 1.25f);
                m.SetColor("_SkyTint", new Color(0.62f, 0.6f, 0.58f));
                m.SetColor("_GroundColor", new Color(0.55f, 0.45f, 0.35f));
                m.SetFloat("_Exposure", 1.2f);
                cache["sky"] = m;
            }
            return m;
        }

        static void SetTex(Material m, string prop, Texture tex, float tiling)
        {
            if (!m.HasProperty(prop)) return;
            m.SetTexture(prop, tex);
            m.SetTextureScale(prop, new Vector2(tiling, tiling));
        }
    }
}
