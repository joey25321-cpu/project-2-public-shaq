using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

namespace DeadMiles.EditorTools
{
    /// <summary>
    /// One-click (actually zero-click) project setup. Runs automatically the first time
    /// the project is opened, and again from the menu: Dead Miles ▸ Run Project Setup.
    /// - creates and assigns a mobile-tuned URP pipeline asset
    /// - creates the base materials the game clones at runtime (so shaders ship in the build)
    /// - configures Android player settings for the Galaxy S26 Ultra class of device
    /// - creates the (empty) main scene and adds it to the build
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string SettingsDir = "Assets/DeadMiles/Settings";
        const string ResDir = "Assets/DeadMiles/Resources/DeadMiles";
        const string Marker = "ProjectSettings/DeadMilesSetup.txt";
        const int Version = 1;

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(Marker) && File.ReadAllText(Marker).Trim() == Version.ToString()) return;
                Run();
            };
        }

        [MenuItem("Dead Miles/Run Project Setup", priority = 0)]
        public static void Run()
        {
            Debug.Log("[Dead Miles] Setting up project…");
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(ResDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            EnsureURP();
            CreateBaseMaterials();
            ConfigurePlayer();
            EnsureScene();
            KeepFogVariants();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            File.WriteAllText(Marker, Version.ToString());
            Debug.Log("[Dead Miles] Setup complete. Press Play to test, or use Dead Miles ▸ Build and Run on Phone.");
        }

        // ------------------------------------------------------------------ URP

        static void EnsureURP()
        {
            var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
            {
                string path = SettingsDir + "/DeadMiles_URP.asset";
                asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null)
                {
                    var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rd, SettingsDir + "/DeadMiles_Renderer.asset");
                    AssignPostProcessData(rd);
                    asset = UniversalRenderPipelineAsset.Create(rd);
                    AssetDatabase.CreateAsset(asset, path);
                }
                GraphicsSettings.defaultRenderPipeline = asset;
            }

            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);

            // mobile-friendly but good-looking defaults
            asset.renderScale = 0.9f;
            asset.msaaSampleCount = 4;
            asset.supportsHDR = true;
            asset.shadowDistance = 90f;
            asset.shadowCascadeCount = 2;
            var so = new SerializedObject(asset);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetInt(so, "m_MainLightShadowmapResolution", 2048);
            SetBool(so, "m_SupportsTerrainHoles", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            foreach (var rd in Resources.FindObjectsOfTypeAll<UniversalRendererData>()) AssignPostProcessData(rd);
        }

        static void AssignPostProcessData(ScriptableObject rendererData)
        {
            var so = new SerializedObject(rendererData);
            var prop = so.FindProperty("postProcessData");
            if (prop == null || prop.objectReferenceValue != null) return;
            foreach (var guid in AssetDatabase.FindAssets("t:PostProcessData"))
            {
                var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (obj == null) continue;
                prop.objectReferenceValue = obj;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rendererData);
                return;
            }
        }

        static void SetBool(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v;
            else if (p.propertyType == SerializedPropertyType.Integer) p.intValue = v ? 1 : 0;
        }
        static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Integer) p.intValue = v;
            else if (p.propertyType == SerializedPropertyType.Enum) p.intValue = v;
        }

        // ------------------------------------------------------------------ materials

        static void CreateBaseMaterials()
        {
            bool urp = GraphicsSettings.defaultRenderPipeline != null;
            string sfx = urp ? "_URP" : "_BIRP";
            string lit = urp ? "Universal Render Pipeline/Lit" : "Standard";
            string particle = urp ? "Universal Render Pipeline/Particles/Unlit" : "Legacy Shaders/Particles/Alpha Blended";

            var flat = AssetDatabase.LoadAssetAtPath<Texture2D>(ResDir + "/flatNormal.asset");
            if (flat == null)
            {
                flat = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color(0.5f, 0.5f, 1f, 1f);
                flat.SetPixels(px);
                flat.Apply();
                AssetDatabase.CreateAsset(flat, ResDir + "/flatNormal.asset");
            }

            MakeMat("Lit" + sfx, lit, m => { });
            MakeMat("LitNormal" + sfx, lit, m =>
            {
                m.SetTexture("_BumpMap", flat);
                m.EnableKeyword("_NORMALMAP");
            });
            MakeMat("LitEmissive" + sfx, lit, m =>
            {
                m.SetColor("_EmissionColor", Color.white);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            MakeMat("Particle" + sfx, particle, m =>
            {
                if (!urp) return;
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
            });
            MakeMat("Sky", "Skybox/Procedural", m => { });
        }

        static void MakeMat(string name, string shaderName, System.Action<Material> setup)
        {
            string path = ResDir + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var sh = Shader.Find(shaderName);
            if (sh == null) { Debug.LogWarning("[Dead Miles] Shader not found: " + shaderName); return; }
            var m = new Material(sh) { name = name };
            setup(m);
            AssetDatabase.CreateAsset(m, path);
        }

        // ------------------------------------------------------------------ player settings

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Shaq";
            PlayerSettings.productName = "Dead Miles";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.shaq.deadmiles");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
#else
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.shaq.deadmiles");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
#endif
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)28;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = 1;
            // Optimized Frame Pacing (Swappy) for smooth 120 Hz
            var ps = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            if (ps != null)
            {
                var pso = new SerializedObject(ps);
                SetBool(pso, "androidUseSwappy", true);
                pso.ApplyModifiedPropertiesWithoutUndo();
            }
            PlayerSettings.Android.renderOutsideSafeArea = true;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            EditorUserBuildSettings.buildAppBundle = false;
        }

        // ------------------------------------------------------------------ scene

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // The game builds everything at runtime. Fog is enabled here only so its shader
                // variants are kept in the Android build.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.003f;
                var sky = AssetDatabase.LoadAssetAtPath<Material>(ResDir + "/Sky.mat");
                if (sky != null) RenderSettings.skybox = sky;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            if (!Application.isBatchMode)
            {
                var active = EditorSceneManager.GetActiveScene();
                if (active.path != ScenePath && !active.isDirty) EditorSceneManager.OpenScene(ScenePath);
            }
        }

        /// <summary>Make sure exponential fog survives shader stripping even if scenes change.</summary>
        static void KeepFogVariants()
        {
            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gs == null || gs.Length == 0) return;
            var so = new SerializedObject(gs[0]);
            SetInt(so, "m_FogStripping", 1); // custom
            SetBool(so, "m_FogKeepLinear", true);
            SetBool(so, "m_FogKeepExp", true);
            SetBool(so, "m_FogKeepExp2", true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
