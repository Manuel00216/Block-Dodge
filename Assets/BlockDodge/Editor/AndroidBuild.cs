using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace BlockDodge.Editor
{
    public static class AndroidBuild
    {
        public static void Run()
        {
            string output = Environment.GetEnvironmentVariable("BLOCK_DODGE_ANDROID_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("BLOCK_DODGE_ANDROID_OUTPUT must specify the APK path.");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Android build failed: " + report.summary.result);
        }
    }
}
