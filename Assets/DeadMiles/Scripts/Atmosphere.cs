using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeadMiles
{
    /// <summary>Sun, sky, ambient light, dusty fog and URP post-processing (ACES, bloom, grading, vignette).</summary>
    public static class Atmosphere
    {
        public static Light Sun { get; private set; }

        public static void Setup(Transform parent, Camera cam)
        {
            // late-afternoon desert sun
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(32f, -35f, 0f);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = new Color(1f, 0.86f, 0.68f);
            Sun.intensity = 1.9f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.85f;
            Sun.shadowBias = 0.05f;
            Sun.shadowNormalBias = 0.4f;
            RenderSettings.sun = Sun;

            var sky = Mats.Sky();
            if (sky != null) RenderSettings.skybox = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.52f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.29f, 0.22f);
            RenderSettings.ambientIntensity = 1f;
            DynamicGI.UpdateEnvironment();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.8f, 0.7f, 0.58f);
            RenderSettings.fogDensity = 0.0032f;

            QualitySettings.shadowDistance = 90f;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

            if (Mats.IsURP) SetupPost(parent, cam);
        }

        static void SetupPost(Transform parent, Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.renderShadows = true;
                data.antialiasing = AntialiasingMode.None; // MSAA from the pipeline asset is cheaper on mobile
            }

            var go = new GameObject("PostFX");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;

            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.65f);

            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.25f);
            ca.contrast.Override(14f);
            ca.saturation.Override(-6f);
            ca.colorFilter.Override(new Color(1f, 0.96f, 0.9f));

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(8f);

            var vg = profile.Add<Vignette>(true);
            vg.intensity.Override(0.24f);
            vg.smoothness.Override(0.45f);
        }
    }
}
