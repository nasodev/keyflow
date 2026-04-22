using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

namespace KeyFlow.Editor
{
    public static class ApkBuilder
    {
        [MenuItem("KeyFlow/Build APK")]
        public static void Build()
        {
            string dir = "Builds";
            Directory.CreateDirectory(dir);
            string apk = Path.Combine(dir, "keyflow-w6-sp2.apk");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/GameplayScene.unity" },
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"APK build failed: {report.summary.result}");
            UnityEngine.Debug.Log($"[KeyFlow] APK built at {apk}, size {report.summary.totalSize / 1024 / 1024} MB");
        }

        [MenuItem("KeyFlow/Build APK (Profile)")]
        public static void BuildProfile()
        {
            string dir = "Builds";
            Directory.CreateDirectory(dir);
            string apk = Path.Combine(dir, "keyflow-w6-sp3-profile.apk");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/GameplayScene.unity" },
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging
            };

            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Profile APK build failed: {report.summary.result}");
            UnityEngine.Debug.Log($"[KeyFlow] Profile APK built at {apk}, size {report.summary.totalSize / 1024 / 1024} MB");
        }
    }
}
