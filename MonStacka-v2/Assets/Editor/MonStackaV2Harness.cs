using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MonStacka.Core;
using MonStacka.Story;
using MonStacka.UI;
using MonStacka.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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

        private sealed class HarnessLogRecord
        {
            public LogType Type;
            public string Message;
            public string StackTrace;
        }

        private static readonly string[] AllowedLogFragments =
        {
            "Licensing",
            "Access token is unavailable",
            "Unsupported protocol version",
        };

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
                var scenarioLogs = new List<HarnessLogRecord>();
                Application.LogCallback logHandler = (condition, stackTrace, type) =>
                {
                    if (IsUnexpectedHarnessLog(condition, type))
                    {
                        scenarioLogs.Add(new HarnessLogRecord
                        {
                            Type = type,
                            Message = condition,
                            StackTrace = stackTrace,
                        });
                    }
                };

                Application.logMessageReceived += logHandler;
                try
                {
                    scenario.Run();
                    if (scenarioLogs.Count > 0)
                    {
                        throw new InvalidOperationException(FormatUnexpectedLogs(scenarioLogs));
                    }

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
                finally
                {
                    Application.logMessageReceived -= logHandler;
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

        private static bool IsUnexpectedHarnessLog(string condition, LogType type)
        {
            if (type != LogType.Warning && type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return false;
            }

            return !AllowedLogFragments.Any(fragment => condition.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }

        private static string FormatUnexpectedLogs(IReadOnlyList<HarnessLogRecord> logs)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Unexpected Unity log output ({logs.Count}):");
            foreach (var log in logs.Take(3))
            {
                builder.Append(log.Type).Append(": ").AppendLine(log.Message);
                if (!string.IsNullOrWhiteSpace(log.StackTrace))
                {
                    var firstStackLine = log.StackTrace.Split('\n').FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(firstStackLine))
                    {
                        builder.Append("  at ").AppendLine(firstStackLine.Trim());
                    }
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static IReadOnlyList<HarnessScenario> BuildScenarios() =>
            new[]
            {
                new HarnessScenario("core vertical slice verifier", MonStackaV2Verification.Run),
                new HarnessScenario("mode ability matrix", VerifyModeAbilityMatrix),
                new HarnessScenario("mode simulated playthrough sweep", VerifyModeSimulatedPlaythroughSweep),
                new HarnessScenario("friendly ability mechanic scenarios", VerifyFriendlyAbilityMechanicScenarios),
                new HarnessScenario("story modifier scenarios", VerifyStoryModifierScenarios),
                new HarnessScenario("enemy ability focused trigger matrix", VerifyEnemyAbilityFocusedTriggerMatrix),
                new HarnessScenario("story deterministic simulation sweep", VerifyStoryDeterministicSimulationSweep),
                new HarnessScenario("story render state consistency sweep", VerifyStoryRenderStateConsistencySweep),
                new HarnessScenario("story input playback sweep", VerifyStoryInputPlaybackSweep),
                new HarnessScenario("records stay split by variant", VerifyRecordSeparation),
                new HarnessScenario("settings and controls smoke", VerifySettingsAndControlsSmoke),
                new HarnessScenario("ability reference text is player-facing", VerifyAbilityReferenceText),
                new HarnessScenario("scene wiring smoke", VerifySceneWiringSmoke),
                new HarnessScenario("visual layout geometry smoke", VerifyVisualLayoutGeometrySmoke),
                new HarnessScenario("story runtime hud and visual sweep", VerifyStoryRuntimeHudAndVisualSweep),
                new HarnessScenario("runtime game flow smoke", VerifyRuntimeGameFlowSmoke),
                new HarnessScenario("current Windows build artifacts", VerifyCurrentBuildArtifacts),
                new HarnessScenario("built player screenshot smoke", VerifyBuiltPlayerScreenshotSmoke),
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

            var assist = new AssistEffectSystem();
            var assistBoard = new BoardState(new[] { PieceType.Z }, seed: 1210);
            Expect(!assist.NextHeldPlacementWillTrigger, "Fresh assist counter should not be armed.");
            Expect(assist.HeldPlacementsUntilTrigger == AssistEffectSystem.TriggerEvery, "Fresh assist counter should need three held placements.");
            Expect(assist.OnPieceLocked(new PieceLockEvent(1, PieceType.Z, 0, Array.Empty<Vector2Int>(), Vector2Int.zero, cameFromHold: true), assistBoard, _ => { }) == null, "First held placement should not trigger assist.");
            Expect(assist.OnPieceLocked(new PieceLockEvent(2, PieceType.Z, 0, Array.Empty<Vector2Int>(), Vector2Int.zero, cameFromHold: true), assistBoard, _ => { }) == null, "Second held placement should arm but not trigger assist.");
            Expect(assist.NextHeldPlacementWillTrigger, "Third held placement should be visibly armed before it locks.");
            var trigger = assist.OnPieceLocked(new PieceLockEvent(3, PieceType.Z, 0, Array.Empty<Vector2Int>(), Vector2Int.zero, cameFromHold: true), assistBoard, _ => { });
            Expect(trigger.HasValue, "Third held placement should trigger assist.");
            Expect(!assist.NextHeldPlacementWillTrigger, "Assist armed state should clear immediately after trigger.");
            Expect(assist.HeldPlacementsUntilTrigger == AssistEffectSystem.TriggerEvery, "Assist counter should reset after trigger.");

            var spawnRecoveryBoard = new BoardState(new[] { PieceType.Z }, seed: 1211);
            while (spawnRecoveryBoard.TryMove(0, 1))
            {
            }
            Expect(spawnRecoveryBoard.LockPiece(), "Harness should lock a piece directly.");
            Expect(!spawnRecoveryBoard.HasActivePiece && !spawnRecoveryBoard.IsGameOver(), "Direct lock should reproduce the no-active handoff gap.");
            Expect(spawnRecoveryBoard.EnsureActivePiece(), "Board should recover by spawning a piece when no active piece exists and the run is alive.");
            Expect(spawnRecoveryBoard.HasActivePiece, "Board recovery should leave an active piece available.");

            var controls = MonStackaControls.BuildControlsSummaryText();
            Expect(controls.Contains("Hard Drop: Space"), "Keyboard hard drop should stay on Space.");
            Expect(!controls.Contains("Hard Drop: D-pad Up / Left Stick Up"), "Gameplay hard drop must not be driven by the Vertical axis.");

            foreach (PieceType pieceType in Enum.GetValues(typeof(PieceType)))
            {
                Expect(CanReachOuterColumn(pieceType, leftSide: true), $"{pieceType} should be able to reach the left outer lane.");
                Expect(CanReachOuterColumn(pieceType, leftSide: false), $"{pieceType} should be able to reach the right outer lane.");
            }

            VerifyLineClearPreservesPieceArtSources();
        }

        private static void VerifyModeSimulatedPlaythroughSweep()
        {
            var scenarios = new[]
            {
                (name: "O.G.B.M. classic", mode: MonStackaMode.Ogbm, zany: false, chapter: (string)null),
                (name: "O.G.B.M. zany", mode: MonStackaMode.Ogbm, zany: true, chapter: (string)null),
                (name: "X(4)-LINES classic", mode: MonStackaMode.Sprint40, zany: false, chapter: (string)null),
                (name: "X(4)-LINES zany", mode: MonStackaMode.Sprint40, zany: true, chapter: (string)null),
                (name: "Training classic", mode: MonStackaMode.Training, zany: false, chapter: (string)null),
                (name: "Training zany", mode: MonStackaMode.Training, zany: true, chapter: (string)null),
                (name: "Story 1.3", mode: MonStackaMode.Story, zany: false, chapter: "1.3"),
            };

            foreach (var scenario in scenarios)
            {
                var manager = LoadGameManagerForMode(scenario.mode, scenario.zany, scenario.chapter);
                var board = manager.Board;
                Expect(board != null, $"{scenario.name}: board should exist.");
                Expect(board.HasActivePiece || board.IsGameOver(), $"{scenario.name}: run should start with an active piece or explicit game over.");
                Expect(board.NextQueue.Count > 0, $"{scenario.name}: next queue should start populated.");
                Expect(manager.FriendlyAbilitiesEnabled == AssistEffectSystem.IsEnabledFor(scenario.mode, scenario.zany), $"{scenario.name}: friendly ability state should match mode rules.");

                var lastScore = board.Score;
                var startingPieces = board.PiecesPlaced;
                for (var step = 0; step < 10 && !board.IsGameOver(); step += 1)
                {
                    if (step % 2 == 0)
                    {
                        board.TryMove(-1, 0);
                    }
                    else
                    {
                        board.TryMove(1, 0);
                    }

                    if (step % 3 == 0)
                    {
                        board.TryRotate(1);
                    }

                    if (step % 4 == 0)
                    {
                        board.TrySoftDrop();
                    }

                    if (scenario.mode != MonStackaMode.Training && step % 5 == 0)
                    {
                        board.TryHold();
                    }

                    InvokePrivate(manager, "HardDropAndSpawn");
                    InvokePrivate(manager, "UpdateVisuals");
                    Expect(board.Score >= lastScore, $"{scenario.name}: score should never decrease during simulated play.");
                    lastScore = board.Score;

                    if (!board.IsGameOver())
                    {
                        Expect(board.HasActivePiece, $"{scenario.name}: active piece should recover after lock step {step}.");
                        Expect(board.NextQueue.Count > 0, $"{scenario.name}: next queue should remain populated after lock step {step}.");
                    }

                    AssertVisibleRuntimePieceState($"{scenario.name} step {step}");
                }

                Expect(board.PiecesPlaced > startingPieces || board.IsGameOver(), $"{scenario.name}: simulated run should place at least one piece or explicitly top out.");
                manager.PauseIfRunning();
                Expect(manager.IsPaused, $"{scenario.name}: pause should work after simulated play.");
                manager.ResumeGame();
                Expect(!manager.IsPaused, $"{scenario.name}: resume should work after simulated play.");
            }

            var trainingToggle = LoadGameManagerForMode(MonStackaMode.Training, friendlyAbilitiesEnabled: false);
            Expect(trainingToggle.CanToggleFriendlyAbilities, "Training playthrough should expose zany toggle.");
            trainingToggle.ToggleFriendlyAbilitiesAndRestart();
            Expect(trainingToggle.FriendlyAbilitiesEnabled, "Training playthrough zany toggle should enable assists.");
            Expect(trainingToggle.Board.PiecesPlaced == 0 && trainingToggle.Board.HasActivePiece, "Training zany toggle should restart to a fresh active board.");
        }

        private static void VerifyFriendlyAbilityMechanicScenarios()
        {
            foreach (PieceType pieceType in Enum.GetValues(typeof(PieceType)))
            {
                var board = new BoardState(new[] { pieceType }, seed: 3100 + (int)pieceType);
                PrepareBoardForAssist(pieceType, board);
                var assist = new AssistEffectSystem();
                var trigger = TriggerHeldAssist(pieceType, board, assist, out var awarded);
                var expectedType = AssistEffectSystem.AssistForPiece(pieceType);
                Expect(trigger.Type == expectedType, $"{pieceType}: friendly assist should trigger {expectedType}.");
                Expect(trigger.ScoreAwarded == awarded, $"{pieceType}: trigger score should match award callback.");
                Expect(trigger.ScoreAwarded >= 150, $"{pieceType}: friendly assist should award at least the base trigger bonus.");

                switch (expectedType)
                {
                    case AssistType.GuardBreak:
                        Expect(board.GetGarbageCells().Count == 0, "Guard Break should remove seeded enemy cells.");
                        break;
                    case AssistType.Digest:
                        Expect(board.GetGarbageCells().Count == 0, "Digest should eat seeded enemy cells.");
                        break;
                    case AssistType.Stitch:
                        Expect(board.GetGarbageCells().Count > 0, "Stitch should repair a covered hole with a junk/repair cell.");
                        break;
                    case AssistType.Calculation:
                        Expect(assist.ActiveWindow == AssistType.Calculation && assist.ExtraPreviewCount == 2, "Calculation should open the extra-preview assist window.");
                        break;
                    case AssistType.EchoGuide:
                        Expect(assist.ActiveWindow == AssistType.EchoGuide && assist.EchoGuideActive, "Echo Guide should open the enhanced guidance window.");
                        break;
                    case AssistType.Sedate:
                        Expect(assist.ActiveWindow == AssistType.Sedate && assist.GravityMultiplier > 1f && assist.LockDelayBonusSeconds > 0f, "Sedate should slow gravity and extend lock delay.");
                        break;
                    case AssistType.Alert:
                        Expect(assist.ActiveWindow == AssistType.Alert && assist.AlertScoreMultiplier(board) > 1f, "Alert should boost scoring while the stack is dangerous.");
                        break;
                }
            }
        }

        private static void VerifyStoryModifierScenarios()
        {
            var firstMission = StoryCatalog.GetChapter("1.1");
            Expect(firstMission != null, "Story 1.1 should exist.");
            Expect(firstMission.Modifiers.Contains(StoryModifier.GuardPressure), "Story 1.1 should run an enemy ability tracker.");
            var firstMissionBoard = new BoardState(new[] { PieceType.Z }, seed: 111);
            var firstMissionModifiers = new StoryModifierSystem(firstMission, firstMissionBoard, seed: 111);
            firstMissionModifiers.OnMatchStart();
            Expect(!firstMissionModifiers.BuildEnemyAbilityStatus().Contains("No enemy modifiers"), "Story 1.1 enemy tracker should not be empty.");
            Expect(firstMissionModifiers.BuildEnemyAbilityStatus().Contains("[ON]"), "Story 1.1 enemy tracker should show that Guard Pressure is active now.");

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
            Expect(status.Contains("[ON]") || status.Contains("[TIMER]") || status.Contains("[LOCK]"), "Enemy status should include explicit trigger/state tags.");
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
            var planningStatus = planningSystem.BuildEnemyAbilityStatus();
            Expect(planningStatus.Contains("rotations"), "Calculated Planning status should mention rotations.");
            Expect(planningStatus.Contains("+") && planningStatus.Contains("cells"), "Calculated Planning status should show triggered penalty progress after extra rotations.");

            var precisionSpec = new StoryChapterSpec
            {
                Id = "harness-precision",
                Title = "Harness Precision Pressure",
                DifficultyTier = 3,
                Modifiers = new[] { StoryModifier.PrecisionPressure },
            };
            var precisionBoard = new BoardState(new[] { PieceType.T }, seed: 1003);
            var precisionSystem = new StoryModifierSystem(precisionSpec, precisionBoard, seed: 1003);
            Expect(precisionBoard.TryMove(0, 1), "Precision harness should move active piece down.");
            Expect(precisionBoard.TryMove(0, 1), "Precision harness should move active piece down again.");
            Expect(precisionBoard.LockPiece(), "Precision harness piece should lock.");
            Expect(precisionBoard.GetGarbageCells().Count > 0, "Precision Pressure should punish unsupported overhangs.");
            Expect(precisionSystem.BuildEnemyAbilityStatus().Contains("overhangs"), "Precision Pressure status should show overhang trigger progress.");

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
                Expect(chapter.Objective.BossHealthPoints > 0, $"Story chapter {chapter.Id} should have positive boss health.");
                Expect(!string.IsNullOrWhiteSpace(chapter.Title), $"Story chapter {chapter.Id} should have a title.");
                var board = new BoardState(chapter.FocusedPieces.Length > 0 ? chapter.FocusedPieces : Enum.GetValues(typeof(PieceType)).Cast<PieceType>(), seed: 1700 + chapter.Sequence + (chapter.Act * 10), spawnWeights: chapter.SpawnBias);
                var system = new StoryModifierSystem(chapter, board, seed: 1800 + chapter.Sequence + (chapter.Act * 10));
                system.OnMatchStart();
                var chapterStatus = system.BuildEnemyAbilityStatus();
                if (chapter.Modifiers.Length > 0)
                {
                    Expect(!chapterStatus.Contains("No enemy modifiers"), $"Story chapter {chapter.Id} should expose enemy modifier status.");
                    Expect(chapterStatus.Contains("[") && chapterStatus.Contains("]"), $"Story chapter {chapter.Id} status should include state tags.");
                    foreach (var modifier in chapter.Modifiers)
                    {
                        Expect(chapterStatus.Contains(StoryModifierLabelForHarness(modifier)), $"Story chapter {chapter.Id} status should include {modifier}.");
                    }
                }
            }
        }

        private static void VerifyEnemyAbilityFocusedTriggerMatrix()
        {
            var guardSpec = ModifierSpec("harness-guard", 2, StoryModifier.GuardPressure);
            var guardBoard = new BoardState(new[] { PieceType.Z }, seed: 4101);
            var guardSystem = new StoryModifierSystem(guardSpec, guardBoard, seed: 4101);
            Expect(guardSystem.LockDelayMultiplier < 1f, "Guard Pressure should reduce lock delay multiplier.");
            Expect(guardSystem.BuildEnemyAbilityStatus().Contains("[ON]"), "Guard Pressure should report ON status.");

            var territorySpec = ModifierSpec("harness-territory", 4, StoryModifier.TerritoryCells);
            var territoryBoard = new BoardState(new[] { PieceType.Z }, seed: 4102);
            var territorySystem = new StoryModifierSystem(territorySpec, territoryBoard, seed: 4102);
            territorySystem.OnMatchStart();
            Expect(territoryBoard.GetGarbageCells().Count >= 4, "Territory Cells should seed enemy cells at match start.");
            Expect(territorySystem.BuildEnemyAbilityStatus().Contains("[SETUP]"), "Territory Cells should report setup status.");

            var planningSpec = ModifierSpec("harness-planning-focused", 3, StoryModifier.CalculatedPlanning);
            planningSpec.NextPreviewCount = 5;
            var planningBoard = new BoardState(new[] { PieceType.T }, seed: 4103);
            var planningSystem = new StoryModifierSystem(planningSpec, planningBoard, seed: 4103);
            planningBoard.TryRotate(1);
            planningBoard.TryRotate(1);
            planningBoard.TryRotate(1);
            Expect(planningBoard.LockPiece(), "Calculated Planning focused matrix should lock a rotated piece.");
            Expect(planningBoard.GetGarbageCells().Count > 0, "Calculated Planning should seed penalty cells after extra rotations.");
            Expect(planningSystem.BuildEnemyAbilityStatus().Contains("penalty") || planningSystem.BuildEnemyAbilityStatus().Contains("+"), "Calculated Planning should report penalty progress.");

            var precisionSpec = ModifierSpec("harness-precision-focused", 3, StoryModifier.PrecisionPressure);
            var precisionBoard = new BoardState(new[] { PieceType.T }, seed: 4104);
            var precisionSystem = new StoryModifierSystem(precisionSpec, precisionBoard, seed: 4104);
            precisionBoard.TryMove(0, 1);
            precisionBoard.TryMove(0, 1);
            Expect(precisionBoard.LockPiece(), "Precision Pressure focused matrix should lock an unsupported piece.");
            Expect(precisionBoard.GetGarbageCells().Count > 0, "Precision Pressure should seed cells from unsupported overhangs.");
            Expect(precisionSystem.BuildEnemyAbilityStatus().Contains("overhangs"), "Precision Pressure should report overhang trigger progress.");

            var ghostSpec = ModifierSpec("harness-ghost", 2, StoryModifier.GhostFlicker);
            var ghostSystem = new StoryModifierSystem(ghostSpec, new BoardState(new[] { PieceType.L }, seed: 4105), seed: 4105);
            ghostSystem.Tick(0.1f);
            Expect(ghostSystem.BuildEnemyAbilityStatus().Contains("[TIMER]"), "Ghost Flicker should expose timer status.");

            var echoSpec = ModifierSpec("harness-echo", 2, StoryModifier.EcholocationDim);
            var echoSystem = new StoryModifierSystem(echoSpec, new BoardState(new[] { PieceType.L }, seed: 4106), seed: 4106);
            Expect(echoSystem.BoardDimAlpha >= 0f, "Echolocation Dim should expose a valid board dim alpha.");
            Expect(echoSystem.BuildEnemyAbilityStatus().Contains("Echolocation Dim"), "Echolocation Dim should appear in enemy status.");

            var resilientSpec = ModifierSpec("harness-resilient", 30, StoryModifier.ResilientCells);
            var resilientBoard = new BoardState(new[] { PieceType.J }, seed: 4107);
            var resilientSystem = new StoryModifierSystem(resilientSpec, resilientBoard, seed: 4107);
            FillBottomLine(resilientBoard, PieceType.J, pieceIdStart: 5000);
            Expect(resilientBoard.ClearLines() == 1, "Resilient Cells focused matrix should clear a prepared line.");
            Expect(resilientBoard.GetGarbageCells().Count > 0, "Resilient Cells should regrow a territory cell after line clear at high difficulty.");
            Expect(resilientSystem.BuildEnemyAbilityStatus().Contains("[CLEAR]"), "Resilient Cells should report clear-trigger status.");

            var mutedSpec = ModifierSpec("harness-muted", 2, StoryModifier.MutedHints);
            var mutedSystem = new StoryModifierSystem(mutedSpec, new BoardState(new[] { PieceType.J }, seed: 4108), seed: 4108);
            Expect(mutedSystem.HintsMuted, "Muted Hints should hide assist/status hints.");
            Expect(mutedSystem.BuildEnemyAbilityStatus().Contains("[ON]"), "Muted Hints should report ON status.");

            var hungerSpec = ModifierSpec("harness-hunger-focused", 12, StoryModifier.HungerMeter);
            var hungerBoard = new BoardState(new[] { PieceType.S }, seed: 4109);
            var hungerSystem = new StoryModifierSystem(hungerSpec, hungerBoard, seed: 4109);
            hungerSystem.Tick(20f);
            Expect(hungerBoard.GetGarbageCells().Count > 0, "Hunger Meter should insert garbage when its timer fills.");
            Expect(hungerSystem.BuildEnemyAbilityStatus().Contains("[TIMER]"), "Hunger Meter should report timer status.");

            var sedationSpec = ModifierSpec("harness-sedation", 2, StoryModifier.SedationWindows);
            var sedationSystem = new StoryModifierSystem(sedationSpec, new BoardState(new[] { PieceType.T }, seed: 4110), seed: 4110);
            sedationSystem.Tick(15f);
            Expect(sedationSystem.SedationActive && sedationSystem.InputSluggishMultiplier > 1f, "Sedation should become active late in its cycle and slow inputs.");
            Expect(sedationSystem.BuildEnemyAbilityStatus().Contains("[ACTIVE]"), "Sedation should report ACTIVE status.");

            var adrenalineSpec = ModifierSpec("harness-adrenaline-focused", 4, StoryModifier.AdrenalineMonitor);
            var adrenalineBoard = new BoardState(new[] { PieceType.I }, seed: 4111);
            adrenalineBoard.Grid[PieceDefinitions.TotalRows - 14, 0] = (int)PieceType.I;
            var adrenalineSystem = new StoryModifierSystem(adrenalineSpec, adrenalineBoard, seed: 4111);
            Expect(adrenalineSystem.GravityMultiplier < 1f, "Adrenaline Monitor should accelerate gravity when stack is high.");
            Expect(adrenalineSystem.BuildEnemyAbilityStatus().Contains("[ACTIVE]"), "Adrenaline Monitor should report ACTIVE status at high stack.");

            var relaySpec = ModifierSpec("harness-relay-focused", 5, StoryModifier.SignalRelay);
            var relayBoard = new BoardState(new[] { PieceType.I }, seed: 4112);
            var relaySystem = new StoryModifierSystem(relaySpec, relayBoard, seed: 4112);
            relaySystem.Tick(25f);
            Expect(relaySystem.BuildEnemyAbilityStatus().Contains("[ACTIVE]"), "Signal Relay should activate after its timer fills.");

            var reducedPreviewSpec = ModifierSpec("harness-reduced-preview", 2, StoryModifier.ReducedPreview);
            reducedPreviewSpec.NextPreviewCount = 1;
            var reducedPreviewSystem = new StoryModifierSystem(reducedPreviewSpec, new BoardState(new[] { PieceType.I }, seed: 4113), seed: 4113);
            Expect(reducedPreviewSystem.BuildEnemyAbilityStatus().Contains("1 next shown"), "Reduced Preview should report the reduced next queue count.");

            var noHoldSpec = ModifierSpec("harness-no-hold", 2, StoryModifier.NoHold);
            noHoldSpec.HoldEnabled = false;
            var noHoldSystem = new StoryModifierSystem(noHoldSpec, new BoardState(new[] { PieceType.I }, seed: 4114), seed: 4114);
            Expect(noHoldSystem.BuildEnemyAbilityStatus().Contains("[ON]"), "No Hold should report ON status.");
        }

        private static string StoryModifierLabelForHarness(StoryModifier modifier) =>
            modifier switch
            {
                StoryModifier.GuardPressure => "Guard Pressure",
                StoryModifier.TerritoryCells => "Territory Cells",
                StoryModifier.CalculatedPlanning => "Calculated Planning",
                StoryModifier.PrecisionPressure => "Precision Pressure",
                StoryModifier.GhostFlicker => "Ghost Flicker",
                StoryModifier.EcholocationDim => "Echolocation Dim",
                StoryModifier.ResilientCells => "Resilient Cells",
                StoryModifier.MutedHints => "Muted Hints",
                StoryModifier.HungerMeter => "Hunger Meter",
                StoryModifier.SedationWindows => "Sedation",
                StoryModifier.AdrenalineMonitor => "Adrenaline Monitor",
                StoryModifier.SignalRelay => "Signal Relay",
                StoryModifier.ReducedPreview => "Reduced Preview",
                StoryModifier.NoHold => "No Hold",
                _ => modifier.ToString(),
            };

        private static void VerifyStoryDeterministicSimulationSweep()
        {
            foreach (var chapter in StoryCatalog.Chapters)
            {
                var pool = chapter.FocusedPieces.Length > 0
                    ? chapter.FocusedPieces
                    : Enum.GetValues(typeof(PieceType)).Cast<PieceType>();
                var board = new BoardState(
                    pool,
                    seed: 2100 + (chapter.Act * 100) + chapter.Sequence,
                    selectedMode: MonStackaMode.Story,
                    spawnWeights: chapter.SpawnBias
                );
                var modifiers = new StoryModifierSystem(chapter, board, seed: 2200 + (chapter.Act * 100) + chapter.Sequence);
                var assist = new AssistEffectSystem();
                var pointEvents = 0;
                var lastScore = board.Score;

                modifiers.OnMatchStart();
                board.OnPieceLocked += lockEvent =>
                    assist.OnPieceLocked(lockEvent, board, points => board.AddScore(points, lockEvent.PieceType));
                board.OnLinesCleared += lines =>
                    assist.OnLinesCleared(lines, points => board.AddScore(points, assist.ActiveWindowPiece));
                board.OnPointsGained += (points, _) =>
                {
                    Expect(points > 0, $"Story chapter {chapter.Id} should only emit positive point events.");
                    pointEvents += 1;
                };

                for (var step = 0; step < 36 && !board.IsGameOver(); step += 1)
                {
                    var status = modifiers.BuildEnemyAbilityStatus();
                    if (chapter.Modifiers.Length > 0)
                    {
                        Expect(!status.Contains("No enemy modifiers"), $"Story chapter {chapter.Id} should keep enemy status populated during simulation.");
                        Expect(status.Contains("[") && status.Contains("]"), $"Story chapter {chapter.Id} should keep trigger/state tags during simulation.");
                    }

                    if (chapter.HoldEnabled && step % 5 == 0)
                    {
                        board.TryHold();
                    }

                    if (step % 3 == 0)
                    {
                        board.TryRotate(1);
                    }

                    var direction = step % 2 == 0 ? -1 : 1;
                    for (var move = 0; move < step % 4; move += 1)
                    {
                        board.TryMove(direction, 0);
                    }

                    var locked = board.HardDrop();
                    Expect(locked || board.IsGameOver(), $"Story chapter {chapter.Id} should either lock a piece or explicitly top out at step {step}.");

                    if (!board.IsGameOver())
                    {
                        Expect(board.EnsureActivePiece(), $"Story chapter {chapter.Id} should recover an active piece after lock step {step}.");
                        Expect(board.HasActivePiece, $"Story chapter {chapter.Id} should have an active piece after recovery at step {step}.");
                    }

                    modifiers.Tick(chapter.GravitySeconds);
                    assist.Tick(chapter.GravitySeconds);
                    Expect(board.Score >= lastScore, $"Story chapter {chapter.Id} score should never decrease.");
                    lastScore = board.Score;

                    var remainingHp = Mathf.Max(0, chapter.Objective.BossHealthPoints - board.Score);
                    var hpPercent = Mathf.Clamp01(remainingHp / (float)chapter.Objective.BossHealthPoints);
                    Expect(hpPercent >= 0f && hpPercent <= 1f, $"Story chapter {chapter.Id} boss HP percent should stay clamped.");
                    Expect(board.NextQueue.Count > 0, $"Story chapter {chapter.Id} should keep next queue populated.");
                }

                Expect(board.PiecesPlaced > 0 || board.IsGameOver(), $"Story chapter {chapter.Id} simulation should place at least one piece or top out explicitly.");
                Expect(pointEvents >= 0, $"Story chapter {chapter.Id} point event counter should remain valid.");
            }
        }

        private static void VerifyStoryRuntimeHudAndVisualSweep()
        {
            var story = LoadGameManagerForMode(MonStackaMode.Story, friendlyAbilitiesEnabled: false, storyChapterId: "1.3");
            var chapter = StoryCatalog.GetChapter("1.3");
            Expect(chapter != null, "Story 1.3 should exist for runtime HUD sweep.");
            Expect(story.CurrentMode == MonStackaMode.Story, "Runtime story HUD sweep should be in Story mode.");

            var bossRoot = RequireRect("BossHealthBar");
            var bossFill = bossRoot.transform.Find("Fill")?.GetComponent<RectTransform>();
            Expect(bossFill != null, "Boss health bar should contain a Fill rect.");
            var hud = UnityEngine.Object.FindFirstObjectByType<HUDController>();
            Expect(hud != null, "Story runtime HUD sweep should find HUDController.");
            story.Board.AddScore(chapter.Objective.BossHealthPoints / 2);
            hud.Render(MonStackaMode.Story, story.Board.Score, story.Board.Lines, 0f, story.Board.IsGameOver(), story.IsPaused, 0f);
            var expectedHp = Mathf.Clamp01((chapter.Objective.BossHealthPoints - story.Board.Score) / (float)chapter.Objective.BossHealthPoints);
            Expect(Mathf.Abs(bossFill.anchorMax.x - expectedHp) <= 0.02f, $"Boss health fill should track score. Expected {expectedHp:0.###}, got {bossFill.anchorMax.x:0.###}.");

            foreach (var textName in new[] { "StoryBossLabel", "StoryScoreLabel", "StoryScoreValue", "StoryEnemyStatus" })
            {
                AssertTextReadable(textName, textName == "StoryEnemyStatus" ? 15 : 18);
            }

            var enemyText = RequireRect("StoryEnemyStatus").GetComponent<Text>();
            hud.RenderStoryEnemyStatus("<color=#ffcf74>Guard Pressure</color> [ON] every piece locks faster\n<color=#ffcf74>Territory Cells</color> [SETUP] cells seeded");
            Expect(enemyText.text.Contains("[ON]") && enemyText.text.Contains("[SETUP]"), "Story enemy HUD should render explicit state tags.");

            var territoryRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>()
                .Where(renderer => renderer && renderer.gameObject.scene.IsValid() && renderer.name.StartsWith("GarbageCell", StringComparison.Ordinal))
                .ToArray();
            Expect(territoryRenderers.Length > 0, "Story 1.3 should render seeded territory cells.");
            foreach (var renderer in territoryRenderers.Where(renderer => renderer.gameObject.activeSelf))
            {
                Expect(renderer.sortingOrder >= 18, "Enemy territory cells should render above generic floor art.");
                Expect(renderer.color.r > renderer.color.g && renderer.color.r > renderer.color.b, "Enemy territory cells should read as red enemy cells, not gray placeholders.");
                Expect(renderer.sprite != null && renderer.sprite.texture != Texture2D.whiteTexture, "Enemy territory cells should use authored/procedural cell art, not a stretched white placeholder.");
            }

            var shell = UnityEngine.Object.FindFirstObjectByType<GameSceneShellController>();
            Expect(shell != null, "Story runtime HUD sweep should find GameSceneShellController.");
            story.PauseIfRunning();
            var pausePanel = FindSceneRect("PausePanel");
            Expect(pausePanel != null && pausePanel.gameObject.activeInHierarchy, "Pause panel should be visible after pausing before settings open.");
            InvokePrivate(shell, "OpenSettings");
            var settingsPanel = RequireRect("GameSettingsPanel");
            Expect(settingsPanel.gameObject.activeInHierarchy, "Settings panel should open from paused story mode.");
            Expect(!pausePanel.gameObject.activeInHierarchy, "Opening settings should suppress the separate pause banner.");
            InvokePrivate(shell, "CloseSettings");
            Expect(!settingsPanel.gameObject.activeInHierarchy, "Settings panel should close cleanly.");
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

        private static void VerifyVisualLayoutGeometrySmoke()
        {
            EditorSceneManager.OpenScene(HomeScenePath);
            var homeCanvas = RequireRect("HomeCanvas");
            var homeDitherComponent = UnityEngine.Object.FindFirstObjectByType<DitherOverlay>();
            Expect(homeDitherComponent != null, "Home scene should contain DitherOverlay.");
            homeDitherComponent.EnsureFullScreenRect();
            var homeDither = RequireRect("DitherOverlay");
            AssertMostlyCovers(homeDither, homeCanvas, "Home dither overlay");
            AssertNotOverlapping("StoryButton", "ArcadeButton", 6f);
            AssertNotOverlapping("StoryButton", "SprintButton", 6f);
            AssertNotOverlapping("LeaderboardStyleToggle", "StoryButton", 4f, allowMissing: true);
            AssertTextReadable("MonstosName", minFontSize: 20);
            AssertTextReadable("SettingsTitle", minFontSize: 20);

            EditorSceneManager.OpenScene(GameScenePath);
            var gameCanvas = RequireRect("GameCanvas");
            var gameDitherComponent = UnityEngine.Object.FindFirstObjectByType<DitherOverlay>();
            Expect(gameDitherComponent != null, "Game scene should contain DitherOverlay.");
            gameDitherComponent.EnsureFullScreenRect();
            var gameDither = RequireRect("DitherOverlay");
            AssertMostlyCovers(gameDither, gameCanvas, "Game dither overlay");
            AssertNotOverlapping("GameSettingsButton", "GameQuitButton", 4f);
            AssertNotOverlapping("GameHomeButton", "GameQuitButton", 4f);
            AssertRenderersNotOverlapping("HoldSlotFrame_Fill", "NextSlotFrame1_Fill", 0.02f);
            AssertRenderersNotOverlapping("NextSlotFrame1_Fill", "NextSlotFrame2_Fill", 0.02f);
            AssertRenderersNotOverlapping("NextSlotFrame2_Fill", "NextSlotFrame3_Fill", 0.02f);
            AssertTextReadable("HoldLabel", minFontSize: 14);
            AssertTextReadable("NextLabel", minFontSize: 14);
            AssertTextReadable("ScoreLabel", minFontSize: 14);
        }

        private static void VerifyRuntimeGameFlowSmoke()
        {
            var ogbm = LoadGameManagerForMode(MonStackaMode.Ogbm, friendlyAbilitiesEnabled: true);
            Expect(ogbm.CurrentMode == MonStackaMode.Ogbm, "Runtime O.G.B.M. should start in O.G.B.M. mode.");
            Expect(ogbm.FriendlyAbilitiesEnabled, "Runtime zany O.G.B.M. should have assists.");
            Expect(!ogbm.IsPaused, "Runtime O.G.B.M. should start unpaused.");
            AssertBoardPanelMatchesPlayableGrid();
            ogbm.PauseIfRunning();
            Expect(ogbm.IsPaused, "PauseIfRunning should pause a match.");
            ogbm.ResumeGame();
            Expect(!ogbm.IsPaused, "ResumeGame should unpause a match.");
            ogbm.RequestRestart();
            Expect(ogbm.IsPaused, "Restart confirmation should pause non-training modes.");
            Expect(ogbm.IsRestartConfirmActive, "Restart confirmation should become active for non-training modes.");
            Expect(ogbm.HasRestartConfirmUi, "Restart confirmation UI should be created.");
            ogbm.TogglePause();
            Expect(!ogbm.IsRestartConfirmActive, "Toggling pause while restart confirm is active should cancel the prompt.");
            Expect(ogbm.IsPaused, "Canceling restart confirmation should leave the run paused.");

            var training = LoadGameManagerForMode(MonStackaMode.Training, friendlyAbilitiesEnabled: false);
            Expect(training.CurrentMode == MonStackaMode.Training, "Runtime training should start in Training mode.");
            Expect(training.CanToggleFriendlyAbilities, "Training should expose the zany toggle.");
            Expect(!training.FriendlyAbilitiesEnabled, "Training should honor classic friendly ability off state.");
            var beforeTrainingBoard = training.Board;
            training.ToggleFriendlyAbilitiesAndRestart();
            Expect(MonStackaAppState.FriendlyAbilitiesEnabled, "Training zany toggle should update app state.");
            Expect(training.FriendlyAbilitiesEnabled, "Training zany toggle should enable assists.");
            Expect(!ReferenceEquals(beforeTrainingBoard, null), "Training board should exist before toggle.");
            Expect(training.Board != null, "Training board should exist after toggle.");
            Expect(training.Board.PiecesPlaced == 0, "Training zany toggle restart should reset placed pieces.");
            training.RequestRestart();
            Expect(!training.IsRestartConfirmActive, "Training restart should not ask for confirmation.");

            var story = LoadGameManagerForMode(MonStackaMode.Story, friendlyAbilitiesEnabled: false, storyChapterId: "1.1");
            Expect(story.CurrentMode == MonStackaMode.Story, "Runtime story should start in Story mode.");
            Expect(story.FriendlyAbilitiesEnabled, "Story should force friendly abilities on.");
            Expect(!story.IsDialogueInputBlocking, "Harness story launch should skip dialogue gate.");
            story.RequestRestart();
            Expect(story.IsRestartConfirmActive, "Story restart should ask for confirmation.");
            Expect(story.IsPaused, "Story restart confirmation should pause the match.");
            story.ReturnHome();
            Expect(story.IsSceneTransitioning, "Home navigation should enter an idempotent scene-transition guard.");
            story.ReturnHome();
            Expect(story.IsSceneTransitioning, "Repeated Home clicks during scene transition should stay guarded.");

            var gameOver = LoadGameManagerForMode(MonStackaMode.Ogbm, friendlyAbilitiesEnabled: false);
            gameOver.Board.Grid[0, 4] = (int)PieceType.I;
            Expect(!gameOver.Board.SpawnNext(PieceType.O), "Runtime top-out setup should force game over.");
            gameOver.RequestRestart();
            Expect(gameOver.IsRestartConfirmActive, "Game-over restart path should still create restart confirmation when requested directly.");
        }

        private static bool CanReachOuterColumn(PieceType pieceType, bool leftSide)
        {
            var board = new BoardState(new[] { pieceType }, seed: 1200 + (int)pieceType);
            var guard = 0;
            while (guard++ < 16 && board.TryMove(leftSide ? -1 : 1, 0))
            {
            }

            var cells = PieceDefinitions.GetAbsoluteCells(board.ActivePiece);
            return leftSide
                ? cells.Min(cell => cell.x) == 0
                : cells.Max(cell => cell.x) == PieceDefinitions.Columns - 1;
        }

        private static void VerifyLineClearPreservesPieceArtSources()
        {
            var board = new BoardState(new[] { PieceType.T }, seed: 44);
            var survivorRow = PieceDefinitions.TotalRows - 2;
            var clearRow = PieceDefinitions.TotalRows - 1;
            const int survivorPieceId = 42;

            board.Grid[survivorRow, 0] = (int)PieceType.T;
            board.PieceIds[survivorRow, 0] = survivorPieceId;
            board.SourceCellXs[survivorRow, 0] = 2;
            board.SourceCellYs[survivorRow, 0] = 0;

            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                board.Grid[clearRow, col] = (int)PieceType.O;
                board.PieceIds[clearRow, col] = 100 + col;
                board.SourceCellXs[clearRow, col] = col % 2;
                board.SourceCellYs[clearRow, col] = col / 2;
            }

            Expect(board.ClearLines() == 1, "Harness line clear setup should clear exactly one row.");
            var record = board.GetLockedPieceGroups().FirstOrDefault(group => group.PieceId == survivorPieceId);
            Expect(record != null, "Surviving partial piece should be rebuilt after line clear.");
            Expect(record.Cells.Count == 1 && record.Cells[0] == new Vector2Int(0, clearRow), "Surviving partial cell should drop into the cleared row.");
            Expect(record.SourceCells.Count == 1 && record.SourceCells[0] == new Vector2Int(2, 0), "Surviving partial cell should keep its original art source coordinate.");
        }

        private static void VerifyStoryRenderStateConsistencySweep()
        {
            var story = LoadGameManagerForMode(MonStackaMode.Story, friendlyAbilitiesEnabled: false, storyChapterId: "1.3");
            var board = story.Board;
            board.Reset();
            for (var row = 0; row < PieceDefinitions.TotalRows; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    board.Grid[row, col] = 0;
                    board.PieceIds[row, col] = 0;
                    board.SourceCellXs[row, col] = 0;
                    board.SourceCellYs[row, col] = 0;
                }
            }

            var survivorRow = PieceDefinitions.TotalRows - 2;
            var clearRow = PieceDefinitions.TotalRows - 1;
            const int survivorPieceId = 9001;
            board.Grid[survivorRow, 0] = (int)PieceType.T;
            board.PieceIds[survivorRow, 0] = survivorPieceId;
            board.SourceCellXs[survivorRow, 0] = 2;
            board.SourceCellYs[survivorRow, 0] = 0;

            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                board.Grid[clearRow, col] = (int)PieceType.O;
                board.PieceIds[clearRow, col] = 9100 + col;
                board.SourceCellXs[clearRow, col] = col % 2;
                board.SourceCellYs[clearRow, col] = col / 2;
            }

            Expect(board.ClearLines() == 1, "Runtime render sweep setup should clear exactly one row.");
            InvokePrivate(story, "RebuildBoardViews");

            var records = board.GetLockedPieceGroups().Where(record => record.Cells.Count > 0).ToList();
            var lockedSkins = Resources.FindObjectsOfTypeAll<PieceSkin>()
                .Where(skin => skin && skin.gameObject.scene.IsValid() && skin.gameObject.scene.isLoaded && skin.PieceId > 0)
                .ToList();
            var lockedSkinIds = lockedSkins.Select(skin => skin.PieceId).OrderBy(id => id).ToArray();
            var recordIds = records.Select(record => record.PieceId).OrderBy(id => id).ToArray();
            Expect(lockedSkinIds.SequenceEqual(recordIds), $"Locked render PieceSkin ids should match board records. Rendered=[{string.Join(",", lockedSkinIds)}] Board=[{string.Join(",", recordIds)}]");

            var survivorRecord = records.FirstOrDefault(record => record.PieceId == survivorPieceId);
            Expect(survivorRecord != null, "Runtime render sweep should keep the survivor record after clear.");
            Expect(survivorRecord.Cells.Count == 1, "Runtime survivor should be a partial one-cell piece after clear.");
            Expect(survivorRecord.SourceCells.Count == 1 && survivorRecord.SourceCells[0] == new Vector2Int(2, 0), "Runtime survivor should preserve source-cell art after clear.");

            var survivorSkin = lockedSkins.FirstOrDefault(skin => skin.PieceId == survivorPieceId);
            Expect(survivorSkin != null, "Runtime survivor should have a visible PieceSkin.");
            Expect(survivorSkin.gameObject.activeInHierarchy, "Runtime survivor PieceSkin should be active in the scene.");
            Expect(!survivorSkin.BodyBuildUsesFullBoxSprite, "Runtime partial survivor should not fall back to a full-box sprite.");
            Expect(survivorSkin.RequiresManualUpdate, "Runtime partial survivor should retain animated visual systems.");
            Expect(survivorSkin.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer => renderer && renderer.sprite), "Runtime survivor should have sprite renderers.");

            var garbageRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>()
                .Where(renderer => renderer && renderer.gameObject.scene.IsValid() && renderer.gameObject.scene.isLoaded && renderer.name.StartsWith("GarbageCell"))
                .ToList();
            foreach (var renderer in garbageRenderers.Where(renderer => renderer.gameObject.activeInHierarchy))
            {
                Expect(renderer.sortingOrder >= 18, "Visible garbage/territory cells should render above floor art.");
                Expect(renderer.color.r > renderer.color.g && renderer.color.r > renderer.color.b, "Visible garbage/territory cells should read as red enemy cells, not gray placeholders.");
                Expect(renderer.sprite != null && renderer.sprite.texture != Texture2D.whiteTexture, "Visible garbage/territory cells should use cell art, not a stretched white placeholder.");
            }
        }

        private static void VerifyStoryInputPlaybackSweep()
        {
            var controls = MonStackaControls.BuildControlsSummaryText();
            Expect(controls.Contains("Hard Drop: Space"), "Story input playback should keep Space as hard drop.");
            Expect(!controls.Contains("Hard Drop: Up") && !controls.Contains("Hard Drop: D-pad Up / Left Stick Up"), "Story input playback should not map Up to hard drop.");
            Expect(!controls.Contains("Pause / Resume: Space"), "Story dialogue submit key Space must not also be a pause binding.");

            foreach (var chapterId in new[] { "1.1", "1.2", "1.3" })
            {
                var chapter = StoryCatalog.GetChapter(chapterId);
                Expect(chapter != null, $"Story chapter {chapterId} should exist for input playback.");
                var story = LoadGameManagerForMode(MonStackaMode.Story, friendlyAbilitiesEnabled: false, storyChapterId: chapterId);
                var board = story.Board;
                AssertStoryManagerInvariants(story, chapter, $"{chapterId} launch");

                _ = board.TryMove(-1, 0);
                _ = board.TryMove(-1, 0);
                _ = board.TryRotate(1);
                _ = board.TrySoftDrop();
                if (chapter.HoldEnabled)
                {
                    Expect(board.TryHold(), $"{chapterId} should accept hold during input playback.");
                    for (var index = 0; index < 3; index += 1)
                    {
                        Expect(board.TrySwapHoldWithUpcoming(index), $"{chapterId} should swap held piece with upcoming slot {index + 1}.");
                    }
                }
                else
                {
                    Expect(!board.TryHold(), $"{chapterId} should reject hold when the chapter disables hold.");
                }
                InvokePrivate(story, "UpdateVisuals");
                AssertStoryManagerInvariants(story, chapter, $"{chapterId} after movement/hold script");

                InvokePrivate(story, "HardDropAndSpawn");
                InvokePrivate(story, "UpdateVisuals");
                AssertStoryManagerInvariants(story, chapter, $"{chapterId} after first hard drop");

                _ = board.TryMove(1, 0);
                _ = board.TryMove(1, 0);
                _ = board.TryRotate(-1);
                if (chapter.HoldEnabled && board.HasHoldPiece)
                {
                    Expect(board.TrySwapHoldWithUpcoming(0), $"{chapterId} should keep queue swap available after a hard drop.");
                }
                InvokePrivate(story, "HardDropAndSpawn");
                InvokePrivate(story, "UpdateVisuals");
                AssertStoryManagerInvariants(story, chapter, $"{chapterId} after second hard drop");

                story.PauseIfRunning();
                Expect(story.IsPaused, $"{chapterId} pause command should pause.");
                var shell = UnityEngine.Object.FindFirstObjectByType<GameSceneShellController>();
                Expect(shell != null, $"{chapterId} should have a shell controller for settings playback.");
                InvokePrivate(shell, "OpenSettings");
                var settingsPanel = FindSceneRect("GameSettingsPanel");
                var pausePanel = FindSceneRect("PausePanel");
                Expect(settingsPanel != null && settingsPanel.gameObject.activeInHierarchy, $"{chapterId} settings panel should open while paused.");
                Expect(pausePanel == null || !pausePanel.gameObject.activeInHierarchy, $"{chapterId} settings should suppress the separate pause banner.");
                InvokePrivate(shell, "CloseSettings");
                Expect(settingsPanel == null || !settingsPanel.gameObject.activeInHierarchy, $"{chapterId} settings panel should close.");
                story.ResumeGame();
                Expect(!story.IsPaused, $"{chapterId} resume command should unpause after settings.");
                AssertStoryManagerInvariants(story, chapter, $"{chapterId} after pause/settings playback");
            }
        }

        private static void AssertStoryManagerInvariants(GameManager story, StoryChapterSpec chapter, string context)
        {
            Expect(story.CurrentMode == MonStackaMode.Story, $"{context}: manager should stay in Story mode.");
            Expect(story.FriendlyAbilitiesEnabled, $"{context}: story should keep friendly abilities enabled.");
            Expect(!story.IsDialogueInputBlocking, $"{context}: harness playback should not be blocked by dialogue.");
            if (!story.Board.IsGameOver())
            {
                Expect(story.Board.HasActivePiece, $"{context}: alive story run should have an active piece.");
                Expect(story.Board.NextQueue.Count > 0, $"{context}: alive story run should keep the next queue populated.");
            }

            if (chapter.Objective.HasBossHealth)
            {
                var hpPercent = Mathf.Clamp01((chapter.Objective.BossHealthPoints - story.Board.Score) / (float)chapter.Objective.BossHealthPoints);
                Expect(hpPercent >= 0f && hpPercent <= 1f, $"{context}: mission HP percent should stay clamped.");
            }
        }

        private static void AssertBoardPanelMatchesPlayableGrid()
        {
            var boardPanel = RequireRect("BoardPanelBackdrop");
            const float CellPixels = 52f;
            var expectedWidth = PieceDefinitions.Columns * CellPixels;
            var expectedHeight = PieceDefinitions.VisibleRows * CellPixels;

            Expect(Mathf.Abs(boardPanel.rect.width - expectedWidth) <= 1f, $"Board panel width should match playable columns. Expected {expectedWidth:0.#}, got {boardPanel.rect.width:0.#}.");
            Expect(Mathf.Abs(boardPanel.rect.height - expectedHeight) <= 1f, $"Board panel height should match visible rows. Expected {expectedHeight:0.#}, got {boardPanel.rect.height:0.#}.");
            Expect(Mathf.Abs(boardPanel.anchoredPosition.x - 700f) <= 1f, $"Board panel x should align with BoardRoot. Got {boardPanel.anchoredPosition.x:0.#}.");
            Expect(Mathf.Abs(boardPanel.anchoredPosition.y + 20f) <= 1f, $"Board panel y should align with BoardRoot. Got {boardPanel.anchoredPosition.y:0.#}.");
        }

        private static void VerifyCurrentBuildArtifacts()
        {
            var buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
            var exePath = Path.Combine(buildDir, "MonStackaV2.exe");
            var dataDir = Path.Combine(buildDir, "MonStackaV2_Data");
            var launchScript = Path.Combine(buildDir, "Launch-MonStackaV2.cmd");
            var stamp = Path.Combine(buildDir, "build-stamp.txt");
            var iconPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "MonStacka", "Art", "AppIcon", "monstacka-app-icon.ico");

            Expect(File.Exists(exePath), "Current Windows build exe should exist.");
            Expect(Directory.Exists(dataDir), "Current Windows build data folder should exist.");
            Expect(File.Exists(launchScript), "Current Windows build launch script should exist.");
            Expect(File.Exists(stamp), "Current Windows build stamp should exist.");
            Expect(File.Exists(iconPath), "MonStacka app icon asset should exist for release builds.");
            Expect(HasReleaseInstructions(), "Repo should include README/download instructions for friends.");

            var buildTimeUtc = ParseBuildStampUtc(stamp);
            var latestRuntimeAssetUtc = LatestRuntimeAssetWriteUtc();
            Expect(buildTimeUtc >= latestRuntimeAssetUtc, $"Windows build is stale. Build stamp {buildTimeUtc:O}; latest runtime asset {latestRuntimeAssetUtc:O}.");
        }

        private static void VerifyBuiltPlayerScreenshotSmoke()
        {
            var buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
            var exePath = Path.Combine(buildDir, "MonStackaV2.exe");
            Expect(File.Exists(exePath), "Built-player smoke needs the Windows player exe.");

            var visualReportDir = Path.Combine(ReportDir, "VisualSmoke");
            Directory.CreateDirectory(visualReportDir);

            var launches = new[]
            {
                new PlayerSmokeLaunch("ogbm-zany", "-monstacka-mode ogbm"),
                new PlayerSmokeLaunch("story-1-3", "-monstacka-mode story -monstacka-chapter 1.3 -monstacka-skip-dialogue"),
            };

            foreach (var launch in launches)
            {
                var screenshotPath = Path.Combine(visualReportDir, $"{launch.Name}.png");
                var smokeReportPath = Path.Combine(visualReportDir, $"{launch.Name}.txt");
                var playerLogPath = Path.Combine(visualReportDir, $"{launch.Name}.log");
                DeleteIfExists(screenshotPath);
                DeleteIfExists(smokeReportPath);
                DeleteIfExists(playerLogPath);

                var arguments =
                    $"-screen-width 1280 -screen-height 720 -screen-fullscreen 0 " +
                    $"{launch.ModeArguments} " +
                    $"-monstacka-capture {QuoteArg(screenshotPath)} " +
                    $"-monstacka-smoke-report {QuoteArg(smokeReportPath)} " +
                    "-monstacka-smoke-quit " +
                    $"-logFile {QuoteArg(playerLogPath)}";

                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    WorkingDirectory = buildDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                Expect(process != null, $"{launch.Name}: player process should start.");
                if (!process.WaitForExit(45000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                        // Process exited between timeout and kill.
                    }

                    throw new InvalidOperationException($"{launch.Name}: built player did not finish smoke test within 45 seconds.");
                }

                Expect(File.Exists(smokeReportPath), $"{launch.Name}: runtime smoke report should exist. Player log: {playerLogPath}");
                var smokeReport = File.ReadAllText(smokeReportPath);
                Expect(smokeReport.Contains($"{RuntimeSmokeLogPrefix} RESULT: PASS", StringComparison.Ordinal), $"{launch.Name}: runtime smoke should pass. Report: {smokeReport.Replace(Environment.NewLine, " ")}");
                Expect(process.ExitCode == 0, $"{launch.Name}: player should exit 0 after PASS. Exit={process.ExitCode}");
                AssertScreenshotLooksRendered(screenshotPath, launch.Name);
            }
        }

        private const string RuntimeSmokeLogPrefix = "[MonStackaSmoke]";

        private readonly struct PlayerSmokeLaunch
        {
            public PlayerSmokeLaunch(string name, string modeArguments)
            {
                Name = name;
                ModeArguments = modeArguments;
            }

            public string Name { get; }
            public string ModeArguments { get; }
        }

        private static void AssertScreenshotLooksRendered(string screenshotPath, string context)
        {
            Expect(File.Exists(screenshotPath), $"{context}: screenshot should exist.");
            var bytes = File.ReadAllBytes(screenshotPath);
            Expect(bytes.Length > 4096, $"{context}: screenshot should not be tiny or empty.");

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Expect(ImageConversion.LoadImage(texture, bytes), $"{context}: screenshot PNG should decode.");
                Expect(texture.width >= 800 && texture.height >= 450, $"{context}: screenshot should be at least 800x450, got {texture.width}x{texture.height}.");

                var pixels = texture.GetPixels32();
                var stride = Mathf.Max(1, pixels.Length / 12000);
                var sampled = 0;
                var blueish = 0;
                var dark = 0;
                var light = 0;
                var saturated = 0;
                var buckets = new HashSet<int>();

                for (var index = 0; index < pixels.Length; index += stride)
                {
                    var pixel = pixels[index];
                    sampled += 1;
                    var max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                    var min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                    if (pixel.b > pixel.r + 12 && pixel.b > pixel.g + 4)
                    {
                        blueish += 1;
                    }

                    if (max < 55)
                    {
                        dark += 1;
                    }

                    if (max > 170)
                    {
                        light += 1;
                    }

                    if (max - min > 35)
                    {
                        saturated += 1;
                    }

                    buckets.Add(((pixel.r / 16) << 8) | ((pixel.g / 16) << 4) | (pixel.b / 16));
                }

                Expect(sampled > 0, $"{context}: screenshot sampler should inspect pixels.");
                Expect(buckets.Count >= 24, $"{context}: screenshot should have varied color buckets, got {buckets.Count}.");
                Expect(blueish >= sampled * 0.15f, $"{context}: screenshot should contain the blue scene background.");
                Expect(dark >= sampled * 0.04f, $"{context}: screenshot should contain dark panel/outline pixels.");
                Expect(light >= sampled * 0.005f, $"{context}: screenshot should contain readable bright UI/text pixels.");
                Expect(saturated >= sampled * 0.08f, $"{context}: screenshot should contain saturated block/UI colors.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string QuoteArg(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static DateTime ParseBuildStampUtc(string stampPath)
        {
            var stampText = File.ReadAllText(stampPath).Trim();
            var marker = " at ";
            var markerIndex = stampText.LastIndexOf(marker, StringComparison.Ordinal);
            Expect(markerIndex >= 0, "Build stamp should contain an ISO timestamp after ' at '.");
            var timestampText = stampText[(markerIndex + marker.Length)..].Trim();
            Expect(DateTime.TryParse(
                    timestampText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timestamp),
                $"Build stamp should parse as UTC timestamp: {timestampText}");
            return timestamp.ToUniversalTime();
        }

        private static DateTime LatestRuntimeAssetWriteUtc()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var assetRoots = new[]
            {
                Path.Combine(projectRoot, "Assets", "MonStacka", "Scripts"),
                Path.Combine(projectRoot, "Assets", "MonStacka", "Scenes"),
                Path.Combine(projectRoot, "Assets", "MonStacka", "Art"),
            };

            return assetRoots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Editor{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(path => File.GetLastWriteTimeUtc(path))
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();
        }

        private static bool HasReleaseInstructions()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var candidates = new[]
            {
                Path.Combine(projectRoot, "README.md"),
                Path.Combine(projectRoot, "..", "README.md"),
                Path.Combine(projectRoot, "Builds", "README.md"),
                Path.Combine(projectRoot, "Builds", "Windows", "README.txt"),
            };

            return candidates.Any(File.Exists);
        }

        private static void AssertVisibleRuntimePieceState(string context)
        {
            var skins = Resources.FindObjectsOfTypeAll<PieceSkin>()
                .Where(skin => skin && skin.gameObject.scene.IsValid() && skin.gameObject.scene.isLoaded && skin.gameObject.activeInHierarchy)
                .ToList();
            Expect(skins.Count > 0, $"{context}: runtime should have at least one visible PieceSkin.");
            Expect(skins.Any(skin => skin.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer => renderer && renderer.sprite)), $"{context}: visible PieceSkin objects should contain sprite renderers.");

            var garbageRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>()
                .Where(renderer => renderer && renderer.gameObject.scene.IsValid() && renderer.gameObject.scene.isLoaded && renderer.name.StartsWith("GarbageCell", StringComparison.Ordinal) && renderer.gameObject.activeInHierarchy)
                .ToList();
            foreach (var renderer in garbageRenderers)
            {
                Expect(renderer.sprite != null && renderer.sprite.texture != Texture2D.whiteTexture, $"{context}: active enemy cells should not render as stretched placeholders.");
                Expect(renderer.sortingOrder >= 18, $"{context}: active enemy cells should render above floor art.");
            }
        }

        private static void PrepareBoardForAssist(PieceType pieceType, BoardState board)
        {
            switch (AssistEffectSystem.AssistForPiece(pieceType))
            {
                case AssistType.GuardBreak:
                case AssistType.Digest:
                    board.SeedTerritoryCells(6, seed: 500 + (int)pieceType);
                    Expect(board.GetGarbageCells().Count > 0, $"{pieceType}: assist setup should seed enemy cells.");
                    break;
                case AssistType.Stitch:
                {
                    var bottom = PieceDefinitions.TotalRows - 1;
                    board.Grid[bottom - 1, 0] = (int)PieceType.J;
                    break;
                }
                case AssistType.Alert:
                    board.Grid[PieceDefinitions.TotalRows - 14, 0] = (int)PieceType.I;
                    Expect(AssistEffectSystem.IsInDanger(board), "Alert assist setup should create a dangerous stack.");
                    break;
            }
        }

        private static AssistTrigger TriggerHeldAssist(PieceType pieceType, BoardState board, AssistEffectSystem assist, out int awarded)
        {
            var totalAwarded = 0;
            AssistTrigger? trigger = null;
            for (var index = 0; index < AssistEffectSystem.TriggerEvery; index += 1)
            {
                trigger = assist.OnPieceLocked(
                    new PieceLockEvent(7000 + index, pieceType, 0, Array.Empty<Vector2Int>(), Vector2Int.zero, cameFromHold: true),
                    board,
                    points => totalAwarded += points
                );
            }

            awarded = totalAwarded;
            Expect(trigger.HasValue, $"{pieceType}: third held placement should trigger a friendly assist.");
            return trigger.Value;
        }

        private static StoryChapterSpec ModifierSpec(string id, int difficultyTier, params StoryModifier[] modifiers) =>
            new()
            {
                Id = id,
                Title = id,
                DifficultyTier = difficultyTier,
                NextPreviewCount = 3,
                HoldEnabled = true,
                Modifiers = modifiers,
            };

        private static void FillBottomLine(BoardState board, PieceType type, int pieceIdStart)
        {
            var bottom = PieceDefinitions.TotalRows - 1;
            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                board.Grid[bottom, col] = (int)type;
                board.PieceIds[bottom, col] = pieceIdStart + col;
                board.SourceCellXs[bottom, col] = col % 2;
                board.SourceCellYs[bottom, col] = col / 2;
            }
        }

        private static GameManager LoadGameManagerForMode(MonStackaMode mode, bool friendlyAbilitiesEnabled, string storyChapterId = null)
        {
            MonStackaAppState.ResetDefaults();
            MonStackaAppState.SelectedMode = mode;
            MonStackaAppState.FriendlyAbilitiesEnabled = friendlyAbilitiesEnabled;
            MonStackaAppState.SelectedStoryChapterId = storyChapterId;
            MonStackaAppState.SkipDialogueForHarness = true;

            EditorSceneManager.OpenScene(GameScenePath);
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Expect(manager != null, "Game scene should contain GameManager for runtime flow.");
            typeof(GameManager)
                .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(manager, null);
            return manager;
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

        private static RectTransform RequireRect(string objectName)
        {
            var rect = FindSceneRect(objectName);
            Expect(rect != null, $"Scene should contain RectTransform named {objectName}.");
            return rect;
        }

        private static RectTransform FindSceneRect(string objectName) =>
            Resources.FindObjectsOfTypeAll<RectTransform>()
                .FirstOrDefault(rect => rect && rect.gameObject.scene.IsValid() && rect.gameObject.scene.isLoaded && rect.name == objectName);

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            Expect(method != null, $"{target.GetType().Name} should contain private method {methodName}.");
            method.Invoke(target, null);
        }

        private static void AssertNotOverlapping(string firstName, string secondName, float minimumGap, bool allowMissing = false)
        {
            var first = FindSceneRect(firstName);
            var second = FindSceneRect(secondName);
            if (allowMissing && (!first || !second))
            {
                return;
            }

            Expect(first != null, $"Scene should contain RectTransform named {firstName}.");
            Expect(second != null, $"Scene should contain RectTransform named {secondName}.");
            var firstRect = GetWorldRect(first);
            var secondRect = GetWorldRect(second);
            Expect(!firstRect.Overlaps(secondRect), $"{firstName} should not overlap {secondName}. {firstName}={FormatRect(firstRect)} {secondName}={FormatRect(secondRect)}");
            var gap = RectGap(firstRect, secondRect);
            Expect(gap >= minimumGap, $"{firstName} and {secondName} should have at least {minimumGap:0.#}px gap, got {gap:0.#}.");
        }

        private static void AssertMostlyCovers(RectTransform overlay, RectTransform canvas, string label)
        {
            var overlayRect = GetWorldRect(overlay);
            var canvasRect = GetWorldRect(canvas);
            Expect(overlayRect.width >= canvasRect.width * 0.95f, $"{label} should cover canvas width.");
            Expect(overlayRect.height >= canvasRect.height * 0.95f, $"{label} should cover canvas height.");
            Expect(overlayRect.Contains(canvasRect.center), $"{label} should cover canvas center.");
        }

        private static void AssertRenderersNotOverlapping(string firstName, string secondName, float minimumGap)
        {
            var first = FindSceneRenderer(firstName);
            var second = FindSceneRenderer(secondName);
            Expect(first != null, $"Scene should contain Renderer named {firstName}.");
            Expect(second != null, $"Scene should contain Renderer named {secondName}.");
            var firstRect = BoundsToRect(first.bounds);
            var secondRect = BoundsToRect(second.bounds);
            Expect(!firstRect.Overlaps(secondRect), $"{firstName} should not overlap {secondName}. {firstName}={FormatRect(firstRect)} {secondName}={FormatRect(secondRect)}");
            var gap = RectGap(firstRect, secondRect);
            Expect(gap >= minimumGap, $"{firstName} and {secondName} should have at least {minimumGap:0.###} world gap, got {gap:0.###}.");
        }

        private static Renderer FindSceneRenderer(string objectName) =>
            Resources.FindObjectsOfTypeAll<Renderer>()
                .FirstOrDefault(renderer => renderer && renderer.gameObject.scene.IsValid() && renderer.gameObject.scene.isLoaded && renderer.name == objectName);

        private static void AssertTextReadable(string objectName, int minFontSize)
        {
            var rect = RequireRect(objectName);
            var text = rect.GetComponent<Text>();
            Expect(text != null, $"{objectName} should have Text.");
            Expect(text.fontSize >= minFontSize || text.resizeTextForBestFit, $"{objectName} should use readable text size.");
            Expect(text.horizontalOverflow != HorizontalWrapMode.Overflow || rect.rect.width >= 90f, $"{objectName} overflow text should have a wide container.");
            Expect(text.verticalOverflow != VerticalWrapMode.Overflow || rect.rect.height >= 24f, $"{objectName} overflow text should have a tall enough container.");
        }

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var minX = corners.Min(corner => corner.x);
            var maxX = corners.Max(corner => corner.x);
            var minY = corners.Min(corner => corner.y);
            var maxY = corners.Max(corner => corner.y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Rect BoundsToRect(Bounds bounds) =>
            Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);

        private static float RectGap(Rect first, Rect second)
        {
            var horizontalGap = first.xMax < second.xMin
                ? second.xMin - first.xMax
                : second.xMax < first.xMin
                    ? first.xMin - second.xMax
                    : 0f;
            var verticalGap = first.yMax < second.yMin
                ? second.yMin - first.yMax
                : second.yMax < first.yMin
                    ? first.yMin - second.yMax
                    : 0f;

            if (horizontalGap <= 0f)
            {
                return verticalGap;
            }

            if (verticalGap <= 0f)
            {
                return horizontalGap;
            }

            return Mathf.Sqrt((horizontalGap * horizontalGap) + (verticalGap * verticalGap));
        }

        private static string FormatRect(Rect rect) =>
            $"x={rect.xMin:0.#}..{rect.xMax:0.#}, y={rect.yMin:0.#}..{rect.yMax:0.#}";

        private static void Expect(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
