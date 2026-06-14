using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MonStacka.Core;
using MonStacka.Story;
using MonStacka.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonStacka.Editor
{
    /// <summary>
    /// Headless regression harness for broad MonStacka smoke coverage.
    /// Keep this separate from MonStackaV2Verification so we can add player-flow,
    /// scene, report, and visual checks without making the fast core verifier noisy.
    /// </summary>
    public static class MonStackaV2Harness
    {
        private const string Root = "Assets/MonStacka";
        private const string HomeScenePath = Root + "/Scenes/Home.unity";
        private const string GameScenePath = Root + "/Scenes/Game.unity";
        private static readonly string ReportDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Reports");

        private readonly struct HarnessScenario
        {
            public HarnessScenario(string name, Action run)
            {
                Name = name;
                Run = run;
            }

            public string Name { get; }
            public Action Run { get; }
        }

        private sealed class HarnessResult
        {
            public string Name;
            public bool Passed;
            public long Milliseconds;
            public string Detail;
        }

        public static void RunBatchMode()
        {
            var exitCode = RunHarness(writeReport: true);
            EditorApplication.Exit(exitCode);
        }

        [MenuItem("MonStacka/Run Regression Harness")]
        public static void RunFromMenu()
        {
            RunHarness(writeReport: true);
        }

        private static int RunHarness(bool writeReport)
        {
            var results = new List<HarnessResult>();
            foreach (var scenario in BuildScenarios())
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    scenario.Run();
                    results.Add(new HarnessResult
                    {
                        Name = scenario.Name,
                        Passed = true,
                        Milliseconds = stopwatch.ElapsedMilliseconds,
                        Detail = "ok",
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new HarnessResult
                    {
                        Name = scenario.Name,
                        Passed = false,
                        Milliseconds = stopwatch.ElapsedMilliseconds,
                        Detail = ex.Message,
                    });
                }
            }

            var report = BuildReport(results);
            if (writeReport)
            {
                Directory.CreateDirectory(ReportDir);
                var reportPath = Path.Combine(ReportDir, "monstacka-harness-latest.txt");
                File.WriteAllText(reportPath, report);
                UnityEngine.Debug.Log($"MonStacka harness report: {reportPath}");
            }

            if (results.Any(result => !result.Passed))
            {
                UnityEngine.Debug.LogError(report);
                return 1;
            }

            UnityEngine.Debug.Log(report);
            return 0;
        }

        private static IReadOnlyList<HarnessScenario> BuildScenarios() =>
            new[]
            {
                new HarnessScenario("core vertical slice verifier", MonStackaV2Verification.Run),
                new HarnessScenario("mode ability matrix", VerifyModeAbilityMatrix),
                new HarnessScenario("story modifier scenarios", VerifyStoryModifierScenarios),
                new HarnessScenario("records stay split by variant", VerifyRecordSeparation),
                new HarnessScenario("settings and controls smoke", VerifySettingsAndControlsSmoke),
                new HarnessScenario("ability reference text is player-facing", VerifyAbilityReferenceText),
                new HarnessScenario("scene wiring smoke", VerifySceneWiringSmoke),
                new HarnessScenario("current Windows build artifacts", VerifyCurrentBuildArtifacts),
            };

        private static void VerifyModeAbilityMatrix()
        {
            Expect(!AssistEffectSystem.IsEnabledFor(MonStackaMode.Ogbm, false), "Classic O.G.B.M. must disable friendly abilities.");
            Expect(AssistEffectSystem.IsEnabledFor(MonStackaMode.Ogbm, true), "Zany O.G.B.M. must enable friendly abilities.");
            Expect(!AssistEffectSystem.IsEnabledFor(MonStackaMode.Sprint40, false), "Classic X(4)-LINES must disable friendly abilities.");
            Expect(AssistEffectSystem.IsEnabledFor(MonStackaMode.Sprint40, true), "Zany X(4)-LINES must enable friendly abilities.");
            Expect(!AssistEffectSystem.IsEnabledFor(MonStackaMode.Training, false), "Training classic toggle must disable friendly abilities.");
            Expect(AssistEffectSystem.IsEnabledFor(MonStackaMode.Training, true), "Training zany toggle must enable friendly abilities.");
            Expect(AssistEffectSystem.IsEnabledFor(MonStackaMode.Story, false), "Story must always enable friendly abilities.");

            MonStackaAppState.ResetDefaults();
            Expect(MonStackaAppState.MusicVolume == 20, "Default music volume should stay below SFX.");
            Expect(MonStackaAppState.SfxVolume == 90, "Default SFX volume should be punchy enough for match feedback.");
            Expect(MonStackaAppState.DitherEnabled, "Dither should default on.");
            Expect(MonStackaAppState.VisualExtrasEnabled, "Visual extras should default on.");
        }

        private static void VerifyStoryModifierScenarios()
        {
            var combinedSpec = new StoryChapterSpec
            {
                Id = "harness-all-modifiers",
                Title = "Harness All Modifiers",
                DifficultyTier = 5,
                NextPreviewCount = 2,
                HoldEnabled = false,
                Modifiers = Enum.GetValues(typeof(StoryModifier)).Cast<StoryModifier>().ToArray(),
            };
            var combinedBoard = new BoardState(new[] { PieceType.T }, seed: 1001);
            var combinedSystem = new StoryModifierSystem(combinedSpec, combinedBoard, seed: 1001);
            combinedSystem.OnMatchStart();
            var status = combinedSystem.BuildEnemyAbilityStatus();
            foreach (var label in new[]
            {
                "Guard Pressure",
                "Territory Cells",
                "Calculated Planning",
                "Precision Pressure",
                "Ghost Flicker",
                "Echolocation Dim",
                "Resilient Cells",
                "Muted Hints",
                "Hunger Meter",
                "Sedation",
                "Adrenaline Monitor",
                "Signal Relay",
                "Reduced Preview",
                "No Hold",
            })
            {
                Expect(status.Contains(label), $"Enemy status should include {label}.");
            }

            Expect(combinedSystem.LockDelayMultiplier < 1f, "Guard Pressure should tighten lock delay.");
            Expect(combinedBoard.GetGarbageCells().Count > 0, "Territory Cells should seed enemy cells on match start.");

            var planningSpec = new StoryChapterSpec
            {
                Id = "harness-calculated",
                Title = "Harness Calculated Planning",
                DifficultyTier = 3,
                NextPreviewCount = 5,
                Modifiers = new[] { StoryModifier.CalculatedPlanning },
            };
            var planningBoard = new BoardState(new[] { PieceType.T }, seed: 1002);
            var planningSystem = new StoryModifierSystem(planningSpec, planningBoard, seed: 1002);
            Expect(planningBoard.TryRotate(1), "Planning harness rotation 1 should succeed.");
            Expect(planningBoard.TryRotate(1), "Planning harness rotation 2 should succeed.");
            Expect(planningBoard.TryRotate(1), "Planning harness rotation 3 should succeed.");
            Expect(planningBoard.HardDrop(), "Planning harness piece should lock.");
            Expect(planningBoard.GetGarbageCells().Count > 0, "Calculated Planning should punish extra rotations.");
            Expect(planningSystem.BuildEnemyAbilityStatus().Contains("rotations"), "Calculated Planning status should mention rotations.");

            var precisionSpec = new StoryChapterSpec
            {
                Id = "harness-precision",
                Title = "Harness Precision Pressure",
                DifficultyTier = 3,
                Modifiers = new[] { StoryModifier.PrecisionPressure },
            };
            var precisionBoard = new BoardState(new[] { PieceType.T }, seed: 1003);
            _ = new StoryModifierSystem(precisionSpec, precisionBoard, seed: 1003);
            Expect(precisionBoard.TryMove(0, 1), "Precision harness should move active piece down.");
            Expect(precisionBoard.TryMove(0, 1), "Precision harness should move active piece down again.");
            Expect(precisionBoard.LockPiece(), "Precision harness piece should lock.");
            Expect(precisionBoard.GetGarbageCells().Count > 0, "Precision Pressure should punish unsupported overhangs.");

            var hungerSpec = new StoryChapterSpec
            {
                Id = "harness-hunger",
                Title = "Harness Hunger",
                DifficultyTier = 10,
                Modifiers = new[] { StoryModifier.HungerMeter },
            };
            var hungerBoard = new BoardState(new[] { PieceType.O }, seed: 1004);
            var hungerSystem = new StoryModifierSystem(hungerSpec, hungerBoard, seed: 1004);
            hungerSystem.OnMatchStart();
            hungerSystem.Tick(20f);
            Expect(hungerBoard.GetGarbageCells().Count > 0, "Hunger Meter should add garbage when its timer fills.");

            var adrenalineSpec = new StoryChapterSpec
            {
                Id = "harness-adrenaline",
                Title = "Harness Adrenaline",
                DifficultyTier = 5,
                Modifiers = new[] { StoryModifier.AdrenalineMonitor },
            };
            var adrenalineBoard = new BoardState(new[] { PieceType.I }, seed: 1005);
            adrenalineBoard.Grid[PieceDefinitions.TotalRows - 14, 0] = (int)PieceType.I;
            var adrenalineSystem = new StoryModifierSystem(adrenalineSpec, adrenalineBoard, seed: 1005);
            Expect(adrenalineSystem.GravityMultiplier < 1f, "Adrenaline Monitor should speed gravity when the stack is high.");

            foreach (var chapter in StoryCatalog.Chapters)
            {
                Expect(chapter.Objective.HasBossHealth, $"Story chapter {chapter.Id} should have boss health.");
            }
        }

        private static void VerifyRecordSeparation()
        {
            const string OgbmKey = "monstacka.records.ogbm.scores";
            const string OgbmZanyKey = "monstacka.records.ogbm.zany.scores";
            const string SprintPureKey = "monstacka.records.sprint.pure";
            const string SprintAssistedKey = "monstacka.records.sprint.assisted";
            var keys = new[] { OgbmKey, OgbmZanyKey, SprintPureKey, SprintAssistedKey };
            var snapshots = keys.ToDictionary(key => key, PlayerPrefs.GetString);

            try
            {
                foreach (var key in keys)
                {
                    PlayerPrefs.DeleteKey(key);
                }

                Expect(MonStackaRecords.TryAddOgbmScore(1000, zany: false), "Classic O.G.B.M. score should record.");
                Expect(MonStackaRecords.TryAddOgbmScore(2000, zany: true), "Zany O.G.B.M. score should record.");
                Expect(MonStackaRecords.GetOgbmScores(false).SequenceEqual(new[] { 1000 }), "Classic O.G.B.M. scores should stay separate.");
                Expect(MonStackaRecords.GetOgbmScores(true).SequenceEqual(new[] { 2000 }), "Zany O.G.B.M. scores should stay separate.");

                Expect(MonStackaRecords.TryAddSprintTime(90000, zany: false), "Classic sprint time should record.");
                Expect(MonStackaRecords.TryAddSprintTime(70000, zany: true), "Zany sprint time should record.");
                Expect(MonStackaRecords.GetSprintTimes(false).SequenceEqual(new[] { 90000 }), "Classic sprint times should stay separate.");
                Expect(MonStackaRecords.GetSprintTimes(true).SequenceEqual(new[] { 70000 }), "Zany sprint times should stay separate.");
            }
            finally
            {
                foreach (var key in keys)
                {
                    if (string.IsNullOrEmpty(snapshots[key]))
                    {
                        PlayerPrefs.DeleteKey(key);
                    }
                    else
                    {
                        PlayerPrefs.SetString(key, snapshots[key]);
                    }
                }

                PlayerPrefs.Save();
            }
        }

        private static void VerifySettingsAndControlsSmoke()
        {
            var summary = MonStackaControls.BuildControlsSummaryText();
            foreach (var action in MonStackaControls.OrderedActions)
            {
                Expect(summary.Contains(MonStackaControls.GetActionLabel(action)), $"Controls summary should include {action}.");
            }

            Expect(summary.Contains("Hold Queue Swap: 1 / 2 / 3"), "Controls summary should include hold queue swap keys.");
            Expect(MonStackaAppState.MusicVolume < MonStackaAppState.SfxVolume, "Default music should be quieter than SFX.");
        }

        private static void VerifyAbilityReferenceText()
        {
            var homeMenuText = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), Root, "Scripts", "UI", "HomeMenuController.cs"));
            var referenceText = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), Root, "BlockAbilityReference.txt"));
            foreach (var forbidden in new[] { "partly wired", "not fully enforced", "current code lists", "not enforced yet" })
            {
                Expect(!homeMenuText.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"Home menu text should not contain stale internal wording: {forbidden}");
                Expect(!referenceText.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"Ability reference should not contain stale internal wording: {forbidden}");
            }

            foreach (var required in new[]
            {
                "2 safe successful rotations",
                "Unsupported overhangs seed enemy territory cells",
                "instant danger-save payout by itself",
            })
            {
                Expect(referenceText.Contains(required, StringComparison.OrdinalIgnoreCase), $"Ability reference should include: {required}");
            }
        }

        private static void VerifySceneWiringSmoke()
        {
            EditorSceneManager.OpenScene(HomeScenePath);
            Expect(UnityEngine.Object.FindFirstObjectByType<HomeMenuController>() != null, "Home scene should contain HomeMenuController.");
            Expect(UnityEngine.Object.FindFirstObjectByType<DitherOverlay>() != null, "Home scene should contain DitherOverlay.");

            EditorSceneManager.OpenScene(GameScenePath);
            Expect(UnityEngine.Object.FindFirstObjectByType<GameManager>() != null, "Game scene should contain GameManager.");
            Expect(UnityEngine.Object.FindFirstObjectByType<HUDController>() != null, "Game scene should contain HUDController.");
            Expect(UnityEngine.Object.FindFirstObjectByType<GameSceneShellController>() != null, "Game scene should contain GameSceneShellController.");
            Expect(UnityEngine.Object.FindFirstObjectByType<DitherOverlay>() != null, "Game scene should contain DitherOverlay.");
        }

        private static void VerifyCurrentBuildArtifacts()
        {
            var buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
            var exePath = Path.Combine(buildDir, "MonStackaV2.exe");
            var dataDir = Path.Combine(buildDir, "MonStackaV2_Data");
            var launchScript = Path.Combine(buildDir, "Launch-MonStackaV2.cmd");
            var stamp = Path.Combine(buildDir, "build-stamp.txt");

            Expect(File.Exists(exePath), "Current Windows build exe should exist.");
            Expect(Directory.Exists(dataDir), "Current Windows build data folder should exist.");
            Expect(File.Exists(launchScript), "Current Windows build launch script should exist.");
            Expect(File.Exists(stamp), "Current Windows build stamp should exist.");
        }

        private static string BuildReport(IReadOnlyList<HarnessResult> results)
        {
            var passed = results.Count(result => result.Passed);
            var report = new StringBuilder();
            report.AppendLine($"MonStacka harness {(passed == results.Count ? "PASS" : "FAIL")} {passed}/{results.Count}");
            report.AppendLine($"Generated: {DateTime.UtcNow:O}");
            report.AppendLine();
            foreach (var result in results)
            {
                report.Append(result.Passed ? "[PASS] " : "[FAIL] ");
                report.Append(result.Name);
                report.Append(" (");
                report.Append(result.Milliseconds);
                report.Append("ms)");
                if (!string.IsNullOrWhiteSpace(result.Detail))
                {
                    report.Append(" - ");
                    report.Append(result.Detail.Replace(Environment.NewLine, " "));
                }
                report.AppendLine();
            }

            return report.ToString();
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
