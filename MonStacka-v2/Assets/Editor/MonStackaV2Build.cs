using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MonStacka.Editor
{
    public static class MonStackaV2Build
    {
        private const string OutputRoot = "Builds/Windows";
        private const string ExeName = "MonStackaV2.exe";

        [MenuItem("MonStacka/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var outputDir = Path.Combine(projectRoot, OutputRoot);
            Directory.CreateDirectory(outputDir);

            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (enabledScenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes were found in EditorBuildSettings.");
            }

            var outputPath = Path.Combine(outputDir, ExeName);
            var options = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows build failed: {report.summary.result} ({report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings)."
                );
            }

            File.WriteAllText(
                Path.Combine(outputDir, "build-stamp.txt"),
                $"MonStacka v2 build completed at {DateTime.UtcNow:O}"
            );
            File.WriteAllText(
                Path.Combine(outputDir, "Launch-MonStackaV2.cmd"),
                "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"%~dp0MonStackaV2.exe\"\r\n"
            );
            UnityEngine.Debug.Log($"MonStacka v2 Windows build complete: {outputPath}");
        }

        public static void BuildWindowsPlayerBatchMode()
        {
            try
            {
                BuildWindowsPlayer();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"MonStacka v2 Windows build failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
