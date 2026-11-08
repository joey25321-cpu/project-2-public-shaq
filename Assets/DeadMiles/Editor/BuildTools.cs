using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeadMiles.EditorTools
{
    /// <summary>Menu: Dead Miles ▸ Build Android APK / Build and Run on Phone. Also the CI entry point.</summary>
    public static class BuildTools
    {
        const string DefaultPath = "Builds/Android/DeadMiles.apk";

        [MenuItem("Dead Miles/Build Android APK", priority = 20)]
        public static void BuildApk() => Build(DefaultPath, false);

        [MenuItem("Dead Miles/Build and Run on Phone (USB)", priority = 21)]
        public static void BuildAndRun() => Build(DefaultPath, true);

        [MenuItem("Dead Miles/Reset Save Data", priority = 40)]
        public static void ResetSave()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Dead Miles] Save data cleared.");
        }

        /// <summary>Used by the GitHub Actions workflow (game-ci/unity-builder, buildMethod).</summary>
        public static void BuildCI()
        {
            string path = "build/Android/DeadMiles.apk";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-customBuildPath") path = args[i + 1];
            if (!path.EndsWith(".apk")) path = Path.Combine(path, "DeadMiles.apk");

            bool ok = Build(path, false);
            if (!ok) EditorApplication.Exit(1);
        }

        static bool Build(string path, bool run)
        {
            ProjectSetup.Run();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Dead Miles] APK built: {Path.GetFullPath(path)} ({summary.totalSize / (1024 * 1024)} MB)");
                if (!Application.isBatchMode && !run) EditorUtility.RevealInFinder(path);
                return true;
            }

            Debug.LogError($"[Dead Miles] Build failed: {summary.result}. Is 'Android Build Support' installed in Unity Hub?");
            return false;
        }
    }
}
