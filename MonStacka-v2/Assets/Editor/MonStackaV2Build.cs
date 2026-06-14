using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MonStacka.Editor
{
    public static class MonStackaV2Build
    {
        private const string WindowsOutputRoot = "Builds/Windows";
        private const string MacOutputRoot = "Builds/Mac";
        private const string WindowsExeName = "MonStackaV2.exe";
        private const string MacAppName = "MonStackaV2.app";
        private const string AppIconAssetPath = "Assets/MonStacka/Art/AppIcon/monstacka-app-icon.png";
        private const string AppIconFilePath = "Assets/MonStacka/Art/AppIcon/monstacka-app-icon.ico";

        [MenuItem("MonStacka/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var outputDir = Path.GetFullPath(Path.Combine(projectRoot, WindowsOutputRoot));
            var outputPath = BuildPlayer(BuildTarget.StandaloneWindows64, outputDir, WindowsExeName, "Windows");
            File.WriteAllText(
                Path.Combine(outputDir, "Launch-MonStackaV2.cmd"),
                "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"%~dp0MonStackaV2.exe\"\r\n"
            );
            RemoveKnownLegacyBuilds(projectRoot);
            UpdateCurrentBuildShortcuts(outputPath, outputDir, Path.GetFullPath(Path.Combine(projectRoot, AppIconFilePath)));
            UnityEngine.Debug.Log($"MonStacka v2 Windows build complete: {outputPath}");
        }

        [MenuItem("MonStacka/Build macOS Player")]
        public static void BuildMacPlayer()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var outputDir = Path.GetFullPath(Path.Combine(projectRoot, MacOutputRoot));
            var outputPath = BuildPlayer(BuildTarget.StandaloneOSX, outputDir, MacAppName, "macOS");
            File.WriteAllText(
                Path.Combine(outputDir, "README-Mac.txt"),
                "MonStacka v2 macOS playtest build\n\n" +
                "1. Extract the whole zip.\n" +
                "2. Open MonStackaV2.app.\n" +
                "3. If macOS blocks the unsigned app, Control-click the app, choose Open, then confirm.\n\n" +
                "This playtest build is not Apple-notarized yet.\n"
            );
            UnityEngine.Debug.Log($"MonStacka v2 macOS build complete: {outputPath}");
        }

        private static string BuildPlayer(BuildTarget target, string outputDir, string outputName, string platformLabel)
        {
            Directory.CreateDirectory(outputDir);

            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (enabledScenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes were found in EditorBuildSettings.");
            }

            ConfigureStandaloneAppIcon();

            var outputPath = Path.GetFullPath(Path.Combine(outputDir, outputName));
            var options = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{platformLabel} build failed: {report.summary.result} ({report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings)."
                );
            }

            File.WriteAllText(
                Path.Combine(outputDir, "build-stamp.txt"),
                $"MonStacka v2 {platformLabel} build completed at {DateTime.UtcNow:O}"
            );

            return outputPath;
        }

        private static void ConfigureStandaloneAppIcon()
        {
            AssetDatabase.ImportAsset(AppIconAssetPath, ImportAssetOptions.ForceUpdate);
            var icon = AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(AppIconAssetPath);
            if (icon == null)
            {
                UnityEngine.Debug.LogWarning($"Could not set MonStacka app icon: missing {AppIconAssetPath}");
                return;
            }

            var iconSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
            var icons = iconSizes.Length > 0
                ? iconSizes.Select(_ => icon).ToArray()
                : new[] { icon };
            var currentIcons = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Application);
            if (currentIcons.Length == icons.Length && currentIcons.All(currentIcon => currentIcon == icon))
            {
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
        }

        private static void RemoveKnownLegacyBuilds(string projectRoot)
        {
            var repoRoot = Directory.GetParent(projectRoot)?.FullName;
            var tetrisRoot = repoRoot != null ? Directory.GetParent(repoRoot)?.FullName : null;
            if (string.IsNullOrWhiteSpace(tetrisRoot))
            {
                return;
            }

            foreach (var folderName in new[] { "monstacka_release_verify", "monstacka_publish" })
            {
                var path = Path.Combine(tetrisRoot, folderName);
                if (!Directory.Exists(path))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(path);
                var fullRoot = Path.GetFullPath(tetrisRoot);
                if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Refusing to delete outside project root: {fullPath}");
                }

                Directory.Delete(fullPath, true);
                UnityEngine.Debug.Log($"Removed stale MonStacka build folder: {fullPath}");
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                RemoveExactLegacyDirectory(Path.Combine(localAppData, "MonStacka!"));
            }

            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            if (!string.IsNullOrWhiteSpace(programs))
            {
                RemoveExactLegacyFile(Path.Combine(programs, "MonStacka!.lnk"));
            }

            var commonPrograms = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            if (!string.IsNullOrWhiteSpace(commonPrograms))
            {
                RemoveExactLegacyFile(Path.Combine(commonPrograms, "MonStacka!.lnk"));
            }
        }

        private static void RemoveExactLegacyDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            Directory.Delete(path, true);
            UnityEngine.Debug.Log($"Removed stale MonStacka build folder: {path}");
        }

        private static void RemoveExactLegacyFile(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            File.Delete(path);
            UnityEngine.Debug.Log($"Removed stale MonStacka shortcut: {path}");
        }

        private static void UpdateCurrentBuildShortcuts(string outputPath, string outputDir, string iconPath)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            var script = $@"
$ErrorActionPreference = 'Stop'
$exe = '{EscapePowerShellSingleQuotedString(outputPath)}'
$workDir = '{EscapePowerShellSingleQuotedString(outputDir)}'
$icon = '{EscapePowerShellSingleQuotedString(iconPath)}'
$shortcutPaths = @(
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\MonStacka\MonStacka Current.lnk'),
    (Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\MonStacka Current.lnk')
)
$shell = New-Object -ComObject WScript.Shell
foreach ($shortcutPath in $shortcutPaths)
{{
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $shortcutPath) | Out-Null
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $workDir
    $shortcut.Description = 'Launch the current MonStacka build'
    $shortcut.IconLocation = if (Test-Path -LiteralPath $icon) {{ $icon }} else {{ ""$exe,0"" }}
    $shortcut.Save()
}}
";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {EncodePowerShell(script)}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    UnityEngine.Debug.LogWarning("Could not update MonStacka shortcuts: failed to start PowerShell.");
                    return;
                }

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(10000))
                {
                    process.Kill();
                    UnityEngine.Debug.LogWarning("Could not update MonStacka shortcuts: PowerShell timed out.");
                    return;
                }

                if (process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogWarning($"Could not update MonStacka shortcuts: PowerShell exited {process.ExitCode}. {stderr}");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    UnityEngine.Debug.Log(stdout);
                }
            }
        }

        private static string EscapePowerShellSingleQuotedString(string value)
        {
            return value.Replace("'", "''");
        }

        private static string EncodePowerShell(string script)
        {
            return Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
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

        public static void BuildMacPlayerBatchMode()
        {
            try
            {
                BuildMacPlayer();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"MonStacka v2 macOS build failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
