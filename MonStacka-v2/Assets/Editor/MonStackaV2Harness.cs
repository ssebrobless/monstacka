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

        private enum ReplayActionKind
        {
            MoveLeft,
            MoveRight,
            RotateCw,
            RotateCcw,
            SoftDrop,
            Hold,
            SwapHold1,
            SwapHold2,
            SwapHold3,
            HardDrop,
            Pause,
            Resume,
            OpenSettings,
            CloseSettings,
            RestartPrompt,
            CancelRestart,
            ToggleTrainingZany,
            TriggerFriendlyAssist,
            ForceGameOver,
        }

        private readonly struct ReplayAction
        {
            public ReplayAction(ReplayActionKind kind, int repeat = 1, bool expectSuccess = true)
            {
                Kind = kind;
                Repeat = Math.Max(1, repeat);
                ExpectSuccess = expectSuccess;
            }

            public ReplayActionKind Kind { get; }
            public int Repeat { get; }
            public bool ExpectSuccess { get; }
        }

        private readonly struct ReplayScenario
        {
            public ReplayScenario(string name, MonStackaMode mode, bool zany, string chapter, params ReplayAction[] actions)
            {
                Name = name;
                Mode = mode;
                Zany = zany;
                Chapter = chapter;
                Actions = actions;
            }

            public string Name { get; }
            public MonStackaMode Mode { get; }
            public bool Zany { get; }
            public string Chapter { get; }
            public IReadOnlyList<ReplayAction> Actions { get; }
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
                new HarnessScenario("ability feedback visual state", VerifyAbilityFeedbackVisualState),
                new HarnessScenario("story deterministic simulation sweep", VerifyStoryDeterministicSimulationSweep),
                new HarnessScenario("story render state consistency sweep", VerifyStoryRenderStateConsistencySweep),
                new HarnessScenario("story input playback sweep", VerifyStoryInputPlaybackSweep),
                new HarnessScenario("runtime replay driver sweep", VerifyRuntimeReplayDriverSweep),
                new HarnessScenario("runtime screenshot checkpoint sweep", VerifyRuntimeScreenshotCheckpointSweep),
                new HarnessScenario("runtime soak replay sweep", VerifyRuntimeSoakReplaySweep),
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
            foreach (var aggrasoChapterId in new[] { "1.1", "1.2", "1.3", "1.4" })
            {
                var aggrasoChapter = StoryCatalog.GetChapter(aggrasoChapterId);
                Expect(aggrasoChapter != null, $"{aggrasoChapterId} should exist.");
                Expect(aggrasoChapter.Modifiers.Contains(StoryModifier.GuardPressure), $"{aggrasoChapterId} should keep Aggraso's Guard Pressure ability.");
                Expect(!aggrasoChapter.Modifiers.Contains(StoryModifier.TerritoryCells), $"{aggrasoChapterId} should not expose Territory Cells as an Aggraso enemy ability.");
            }

            foreach (var muwerdeChapterId in new[] { "2.1", "2.2", "2.3", "2.4" })
            {
                var muwerdeChapter = StoryCatalog.GetChapter(muwerdeChapterId);
                Expect(muwerdeChapter != null, $"{muwerdeChapterId} should exist.");
                Expect(muwerdeChapter.Modifiers.SequenceEqual(new[] { StoryModifier.ResilientCells }), $"{muwerdeChapterId} should use Muwerde's single Resilient Cells ability only.");
                Expect(!muwerdeChapter.Modifiers.Contains(StoryModifier.CalculatedPlanning), $"{muwerdeChapterId} should not keep the retired Muwerde rotation debuff.");
                Expect(!muwerdeChapter.Modifiers.Contains(StoryModifier.PrecisionPressure), $"{muwerdeChapterId} should not keep the retired Muwerde overhang penalty.");
            }

            foreach (var dousemaChapterId in new[] { "3.3", "3.4" })
            {
                var dousemaChapter = StoryCatalog.GetChapter(dousemaChapterId);
                Expect(dousemaChapter != null, $"{dousemaChapterId} should exist.");
                Expect(dousemaChapter.Modifiers.Contains(StoryModifier.ResilientCells), $"{dousemaChapterId} should use Dousema's Resilient Cells ability.");
            }

            var firstMissionBoard = new BoardState(new[] { PieceType.Z }, seed: 111);
            var firstMissionModifiers = new StoryModifierSystem(firstMission, firstMissionBoard, seed: 111);
            firstMissionModifiers.OnMatchStart();
            Expect(!firstMissionModifiers.BuildEnemyAbilityStatus().Contains("No enemy modifiers"), "Story 1.1 enemy tracker should not be empty.");
            Expect(firstMissionModifiers.BuildEnemyAbilityStatus().Contains("[TIMER]"), "Story 1.1 enemy tracker should show when Guard Pressure will add a pressure row.");

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
                "Calculated Planning",
                "Precision Pressure",
                "Blinded",
                "Resilient Cells",
                "Muted Hints",
                "Insatiable Hunger",
                "Sedating Spit",
                "Adrenaline Rush",
                "Reduced Preview",
                "No Hold",
            })
            {
                Expect(status.Contains(label), $"Enemy status should include {label}.");
            }
            Expect(!status.Contains("Signal Relay"), "Signal Relay should not appear in enemy status after retirement, even in legacy modifier data.");

            Expect(Mathf.Approximately(combinedSystem.LockDelayMultiplier, 1f), "Guard Pressure should not tighten lock delay after becoming a row pressure ability.");
            Expect(combinedBoard.GetGarbageCells().Count > 0, "Resilient Cells should seed claimed enemy cells on match start.");
            Expect(status.Contains("Resilient Cells") && status.Contains("next claim"), "Resilient Cells status should track claimed-cell spreading.");

            var planningSpec = new StoryChapterSpec
            {
                Id = "harness-calculated",
                Title = "Harness Calculated Planning",
                DifficultyTier = 3,
                NextPreviewCount = 5,
                Modifiers = new[] { StoryModifier.CalculatedPlanning },
            };
            var planningBoard = new BoardState(new[] { PieceType.T, PieceType.O }, seed: 1002);
            var planningSystem = new StoryModifierSystem(planningSpec, planningBoard, seed: 1002);
            var planningLockedPieces = new List<PieceLockEvent>();
            planningBoard.OnPieceLocked += lockEvent => planningLockedPieces.Add(lockEvent);
            Expect(planningBoard.TryRotate(1), "Planning harness rotation 1 should succeed.");
            Expect(planningBoard.TryRotate(1), "Planning harness rotation 2 should succeed.");
            Expect(planningBoard.TryRotate(1), "Planning harness rotation 3 should succeed.");
            Expect(planningBoard.TryRotate(1), "Planning harness rotation 4 should succeed.");
            var planningQueuedStatus = planningSystem.BuildEnemyAbilityStatus();
            Expect(planningQueuedStatus.Contains("queued"), "Calculated Planning should queue immediately after the fourth rotation, before the piece locks.");
            Expect(planningBoard.TryHold(), "Calculated Planning queued debuff should survive swapping before placement.");
            Expect(planningBoard.HardDrop(), "Planning harness piece should lock.");
            Expect(planningLockedPieces.Count == 1, "Calculated Planning should observe the first locked piece.");
            Expect(planningBoard.IsPieceScoreDebuffed(planningLockedPieces[0].PieceId), "Calculated Planning should apply the queued debuff to the next placed piece after a swap.");
            var planningStatus = planningSystem.BuildEnemyAbilityStatus();
            Expect(planningStatus.Contains("score"), "Calculated Planning status should report the score debuff after it applies.");

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
                Title = "Harness Insatiable Hunger",
                DifficultyTier = 10,
                Modifiers = new[] { StoryModifier.HungerMeter },
            };
            var hungerBoard = new BoardState(new[] { PieceType.O }, seed: 1004);
            var hungerSystem = new StoryModifierSystem(hungerSpec, hungerBoard, seed: 1004);
            const int hungerScenarioPieceId = 5100;
            hungerBoard.Grid[PieceDefinitions.TotalRows - 2, 3] = (int)PieceType.O;
            hungerBoard.PieceIds[PieceDefinitions.TotalRows - 2, 3] = hungerScenarioPieceId;
            FillBottomLine(hungerBoard, PieceType.I, pieceIdStart: 5110);
            hungerSystem.OnMatchStart();
            Expect(hungerBoard.ClearLines() == 1, "Insatiable Hunger harness should clear one row.");
            Expect(!hungerBoard.GetLockedPieceGroups().Any(record => record.PieceId == hungerScenarioPieceId), "Insatiable Hunger should consume the top-layer block instead of adding garbage.");

            var resilientSpec = new StoryChapterSpec
            {
                Id = "harness-resilient",
                Title = "Harness Resilient Cells",
                DifficultyTier = 10,
                Modifiers = new[] { StoryModifier.ResilientCells },
            };
            var resilientBoard = new BoardState(new[] { PieceType.O }, seed: 1005);
            var resilientSystem = new StoryModifierSystem(resilientSpec, resilientBoard, seed: 1005);
            resilientSystem.OnMatchStart();
            Expect(resilientBoard.GetTerritorySourceCells().Count == 1, "Resilient Cells should start with one permanent claimed source.");
            Expect(resilientSystem.BuildEnemyAbilityStatus().Contains("next claim"), "Resilient Cells status should show claim timer progress.");
            SeedClaimableNeighbor(resilientBoard);
            resilientSystem.Tick(20f);
            Expect(resilientBoard.GetTerritoryClaimedCells().Count > 0, "Resilient Cells should expand to adjacent locked blocks over time.");

            var adrenalineSpec = new StoryChapterSpec
            {
                Id = "harness-adrenaline",
                Title = "Harness Adrenaline",
                DifficultyTier = 5,
                Modifiers = new[] { StoryModifier.AdrenalineMonitor, StoryModifier.HungerMeter },
            };
            var adrenalineBoard = new BoardState(new[] { PieceType.I }, seed: 1005);
            var adrenalineSystem = new StoryModifierSystem(adrenalineSpec, adrenalineBoard, seed: 1005);
            Expect(Mathf.Approximately(adrenalineSystem.GravityMultiplier, 1f), "Adrenaline Rush should not speed gravity directly.");
            adrenalineSystem.Tick(19f);
            Expect(adrenalineSystem.BuildEnemyAbilityStatus().Contains("Adrenaline Rush") && adrenalineSystem.BuildEnemyAbilityStatus().Contains("[TIMER]"), "Adrenaline Rush should show cooldown progress before it activates.");
            adrenalineSystem.Tick(1f);
            Expect(adrenalineSystem.BuildEnemyAbilityStatus().Contains("Adrenaline Rush") && adrenalineSystem.BuildEnemyAbilityStatus().Contains("[ACTIVE]"), "Adrenaline Rush should activate every 20 seconds.");
            const int adrenalineHungerPieceId = 5120;
            adrenalineBoard.Grid[PieceDefinitions.TotalRows - 4, 4] = (int)PieceType.S;
            adrenalineBoard.PieceIds[PieceDefinitions.TotalRows - 4, 4] = adrenalineHungerPieceId;
            FillBottomLine(adrenalineBoard, PieceType.O, pieceIdStart: 5130);
            Expect(adrenalineBoard.ClearLines() == 1, "Adrenaline Rush harness should clear one row.");
            Expect(!adrenalineBoard.GetLockedPieceGroups().Any(record => record.PieceId == adrenalineHungerPieceId), "Adrenaline Rush should enhance Insatiable Hunger so one cleared line can trigger it.");
            adrenalineSystem.Tick(11.1f);
            Expect(!adrenalineSystem.BuildEnemyAbilityStatus().Contains("[ACTIVE]"), "Adrenaline Rush should end after 11 seconds.");

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
            var guardEvents = new List<StoryModifierTriggerEvent>();
            guardSystem.OnModifierTriggered += trigger => guardEvents.Add(trigger);
            guardSystem.OnMatchStart();
            Expect(Mathf.Approximately(guardSystem.LockDelayMultiplier, 1f), "Guard Pressure should leave lock delay unchanged.");
            Expect(guardSystem.BuildEnemyAbilityStatus().Contains("[TIMER]"), "Guard Pressure should report timer status before pressure rows trigger.");
            guardSystem.Tick(16f);
            Expect(guardBoard.GetGuardPressureRowCount() == 1, "Guard Pressure should add a full temporary bottom row when its timer fills.");
            Expect(guardSystem.BuildEnemyAbilityStatus().Contains("[ACTIVE]"), "Guard Pressure should report ACTIVE status while pressure rows are on the board.");
            Expect(guardEvents.Any(trigger => trigger.Modifier == StoryModifier.GuardPressure && trigger.State == "ACTIVE"), "Guard Pressure should emit an ACTIVE trigger when it adds a pressure row.");
            Expect(guardBoard.ClearLines() == 0, "A Guard Pressure row should not score as a normal completed line.");
            guardSystem.Tick(5.9f);
            Expect(guardBoard.GetGuardPressureRowCount() == 1, "Guard Pressure row should remain until the six-second timer expires.");
            guardSystem.Tick(0.2f);
            Expect(guardBoard.GetGuardPressureRowCount() == 0, "Guard Pressure row should clear itself after six seconds.");
            Expect(guardEvents.Any(trigger => trigger.Modifier == StoryModifier.GuardPressure && trigger.State == "END"), "Guard Pressure should emit END when a pressure row expires.");

            var guardEarlyBoard = new BoardState(new[] { PieceType.Z }, seed: 4115);
            var guardEarlySystem = new StoryModifierSystem(guardSpec, guardEarlyBoard, seed: 4115);
            var guardEarlyEvents = new List<StoryModifierTriggerEvent>();
            guardEarlySystem.OnModifierTriggered += trigger => guardEarlyEvents.Add(trigger);
            guardEarlySystem.Tick(16f);
            FillLine(guardEarlyBoard, PieceDefinitions.TotalRows - 2, PieceType.O, pieceIdStart: 6100);
            Expect(guardEarlyBoard.ClearLines() == 1, "Clearing a player-built row should still score normally while Guard Pressure is active.");
            Expect(guardEarlyBoard.GetGuardPressureRowCount() == 0, "Clearing a player-built row should remove the active Guard Pressure row early.");
            Expect(guardEarlyEvents.Any(trigger => trigger.Modifier == StoryModifier.GuardPressure && trigger.State == "CLEARED"), "Guard Pressure should emit CLEARED when the player clears it early.");

            var stackedGuardSpec = ModifierSpec("harness-guard-stacked", 10, StoryModifier.GuardPressure);
            var stackedGuardBoard = new BoardState(new[] { PieceType.Z }, seed: 4116);
            var stackedGuardSystem = new StoryModifierSystem(stackedGuardSpec, stackedGuardBoard, seed: 4116);
            var stackedGuardEvents = new List<StoryModifierTriggerEvent>();
            stackedGuardSystem.OnModifierTriggered += trigger => stackedGuardEvents.Add(trigger);
            stackedGuardSystem.Tick(4f);
            stackedGuardSystem.Tick(4f);
            Expect(stackedGuardBoard.GetGuardPressureRowCount() == 2, "High-tier Guard Pressure should be able to stack two active pressure rows.");
            FillLine(stackedGuardBoard, PieceDefinitions.TotalRows - 3, PieceType.O, pieceIdStart: 6200);
            Expect(stackedGuardBoard.ClearLines() == 1, "Clearing a player-built row should still score while multiple Guard Pressure rows are active.");
            Expect(stackedGuardBoard.GetGuardPressureRowCount() == 1, "Clearing a row should remove only the oldest Guard Pressure row, not every active pressure row.");
            stackedGuardSystem.Tick(2.1f);
            Expect(stackedGuardBoard.GetGuardPressureRowCount() == 1, "The remaining newer Guard Pressure row should keep its own timer after an early clear.");
            stackedGuardSystem.Tick(4f);
            Expect(stackedGuardEvents.Any(trigger => trigger.Modifier == StoryModifier.GuardPressure && trigger.State == "END"), "The remaining newer Guard Pressure row should expire on its own six-second timer.");
            Expect(stackedGuardBoard.GetGuardPressureRowCount() == 1, "High-tier Guard Pressure may add a replacement row as the previous row expires.");

            var territorySpec = ModifierSpec("harness-territory", 1, StoryModifier.TerritoryCells);
            var territoryBoard = new BoardState(new[] { PieceType.Z }, seed: 4102);
            var territorySystem = new StoryModifierSystem(territorySpec, territoryBoard, seed: 4102);
            var territoryEvents = new List<StoryModifierTriggerEvent>();
            territorySystem.OnModifierTriggered += trigger => territoryEvents.Add(trigger);
            territorySystem.OnMatchStart();
            Expect(territoryBoard.GetTerritorySourceCells().Count == 1, "Territory Cells should seed one permanent claimed source at match start.");
            Expect(territoryBoard.GetTerritoryClaimedCells().Count == 0, "Territory Cells should not start with temporary claims.");
            var territorySource = territoryBoard.GetTerritorySourceCells()[0];
            var firstClaimCandidate = new Vector2Int(territorySource.x, territorySource.y - 1);
            territoryBoard.Grid[firstClaimCandidate.y, firstClaimCandidate.x] = (int)PieceType.Z;
            territoryBoard.PieceIds[firstClaimCandidate.y, firstClaimCandidate.x] = 7100;
            var pairedClaimCell = new Vector2Int(Mathf.Max(0, firstClaimCandidate.x - 1), firstClaimCandidate.y);
            territoryBoard.Grid[pairedClaimCell.y, pairedClaimCell.x] = (int)PieceType.Z;
            territoryBoard.PieceIds[pairedClaimCell.y, pairedClaimCell.x] = 7100;
            territorySystem.Tick(9.9f);
            Expect(territoryBoard.GetTerritoryClaimedCells().Count == 0, "Territory Cells should wait the full 10 seconds before claiming.");
            territorySystem.Tick(0.2f);
            Expect(territoryBoard.IsTerritoryClaimed(firstClaimCandidate), "Territory Cells should claim a locked block touching the permanent source after its timer fills.");
            Expect(territoryBoard.IsTerritoryClaimed(pairedClaimCell), "Territory Cells should claim every remaining cell belonging to the target block.");
            territorySystem.Tick(30f);
            Expect(territoryBoard.GetTerritoryClaimedCells().Count == 2, "Low-tier Territory Cells should pause claiming while one block is already claimed.");
            FillLine(territoryBoard, firstClaimCandidate.y, PieceType.O, pieceIdStart: 7200);
            territoryBoard.Grid[firstClaimCandidate.y, firstClaimCandidate.x] = (int)PieceType.Z;
            territoryBoard.PieceIds[firstClaimCandidate.y, firstClaimCandidate.x] = 7100;
            territoryBoard.Grid[pairedClaimCell.y, pairedClaimCell.x] = (int)PieceType.Z;
            territoryBoard.PieceIds[pairedClaimCell.y, pairedClaimCell.x] = 7100;
            Expect(territoryBoard.ClearLines() == 0, "A claimed block should not count toward row completion.");
            FillBottomLine(territoryBoard, PieceType.O, pieceIdStart: 7300);
            Expect(territoryBoard.ClearLines() == 1, "Clearing another completed row should still score while territory claims exist.");
            Expect(territoryBoard.GetTerritoryClaimedCells().Count == 0, "Clearing a row should remove exactly one temporary Territory claim.");
            Expect(territoryBoard.GetTerritorySourceCells().Count == 1, "The permanent Territory source should remain after line clears.");
            Expect(territorySystem.BuildEnemyAbilityStatus().Contains("[TIMER]"), "Territory Cells should report timer status after setup.");
            Expect(territoryEvents.Any(trigger => trigger.Modifier == StoryModifier.TerritoryCells && trigger.State == "SETUP"), "Territory Cells should emit a setup trigger event.");
            Expect(territoryEvents.Any(trigger => trigger.Modifier == StoryModifier.TerritoryCells && trigger.State == "CLAIM"), "Territory Cells should emit a claim trigger event.");
            Expect(territoryEvents.Any(trigger => trigger.Modifier == StoryModifier.TerritoryCells && trigger.State == "CLEARED"), "Territory Cells should emit CLEARED when a line clear removes a claim.");

            var stackedTerritorySpec = ModifierSpec("harness-territory-stacked", 10, StoryModifier.TerritoryCells);
            var stackedTerritoryBoard = new BoardState(new[] { PieceType.Z }, seed: 4117);
            var stackedTerritorySystem = new StoryModifierSystem(stackedTerritorySpec, stackedTerritoryBoard, seed: 4117);
            stackedTerritorySystem.OnMatchStart();
            var stackedSource = stackedTerritoryBoard.GetTerritorySourceCells()[0];
            FillTerritoryClaimCandidates(stackedTerritoryBoard, stackedSource, PieceType.Z, pieceIdStart: 7400);
            stackedTerritorySystem.Tick(10f);
            stackedTerritorySystem.Tick(10f);
            stackedTerritorySystem.Tick(10f);
            stackedTerritorySystem.Tick(10f);
            Expect(stackedTerritoryBoard.GetTerritoryClaimedCells().Count == 4, "High-tier Territory Cells should be able to stack four claimed blocks.");
            stackedTerritorySystem.Tick(20f);
            Expect(stackedTerritoryBoard.GetTerritoryClaimedCells().Count == 4, "High-tier Territory Cells should stop claiming once the active claim cap is reached.");
            FillLine(stackedTerritoryBoard, PieceDefinitions.TotalRows - 5, PieceType.O, pieceIdStart: 7500);
            Expect(stackedTerritoryBoard.ClearLines() == 1, "A normal line clear should remove only one stacked Territory claim.");
            Expect(stackedTerritoryBoard.GetTerritoryClaimedCells().Count == 3, "Territory claims should clear oldest-first, one claimed block per player line clear.");

            var cascadeTerritorySpec = ModifierSpec("harness-territory-cascade", 10, StoryModifier.TerritoryCells);
            var cascadeTerritoryBoard = new BoardState(new[] { PieceType.Z }, seed: 4118);
            var cascadeTerritorySystem = new StoryModifierSystem(cascadeTerritorySpec, cascadeTerritoryBoard, seed: 4118);
            cascadeTerritorySystem.OnMatchStart();
            var cascadeSource = cascadeTerritoryBoard.GetTerritorySourceCells()[0];
            var cascadeClaimA = new Vector2Int(cascadeSource.x, cascadeSource.y - 1);
            var cascadeClaimB = new Vector2Int(Mathf.Max(0, cascadeSource.x - 1), cascadeSource.y);
            cascadeTerritoryBoard.Grid[cascadeClaimA.y, cascadeClaimA.x] = (int)PieceType.Z;
            cascadeTerritoryBoard.PieceIds[cascadeClaimA.y, cascadeClaimA.x] = 7600;
            cascadeTerritoryBoard.Grid[cascadeClaimB.y, cascadeClaimB.x] = (int)PieceType.Z;
            cascadeTerritoryBoard.PieceIds[cascadeClaimB.y, cascadeClaimB.x] = 7601;
            cascadeTerritorySystem.Tick(10f);
            cascadeTerritorySystem.Tick(10f);
            Expect(cascadeTerritoryBoard.GetTerritoryClaimedCells().Count == 2, "Cascade setup should have two claimed blocks.");
            FillLine(cascadeTerritoryBoard, cascadeClaimA.y, PieceType.O, pieceIdStart: 7620);
            cascadeTerritoryBoard.Grid[cascadeClaimA.y, cascadeClaimA.x] = (int)PieceType.Z;
            cascadeTerritoryBoard.PieceIds[cascadeClaimA.y, cascadeClaimA.x] = 7600;
            FillLine(cascadeTerritoryBoard, PieceDefinitions.TotalRows - 4, PieceType.O, pieceIdStart: 7630);
            Expect(cascadeTerritoryBoard.ClearLines() == 1, "Cascade setup should clear a player-built row to unclaim the oldest block.");
            Expect(cascadeTerritoryBoard.GetTerritoryClaimedCells().Count == 1, "Rows unlocked by unclaiming should not also clear another claimed block.");

            var planningSpec = ModifierSpec("harness-planning-focused", 3, StoryModifier.CalculatedPlanning);
            planningSpec.NextPreviewCount = 5;
            var planningBoard = new BoardState(new[] { PieceType.T, PieceType.O }, seed: 4103);
            var planningSystem = new StoryModifierSystem(planningSpec, planningBoard, seed: 4103);
            var planningEvents = new List<StoryModifierTriggerEvent>();
            var planningLocks = new List<PieceLockEvent>();
            planningSystem.OnModifierTriggered += trigger => planningEvents.Add(trigger);
            planningBoard.OnPieceLocked += lockEvent => planningLocks.Add(lockEvent);
            planningBoard.TryRotate(1);
            planningBoard.TryRotate(1);
            planningBoard.TryRotate(1);
            planningBoard.TryRotate(1);
            Expect(planningEvents.Any(trigger => trigger.Modifier == StoryModifier.CalculatedPlanning && trigger.State == "QUEUED"), "Calculated Planning should queue a score debuff as soon as rotations exceed the budget.");
            Expect(planningBoard.TryHold(), "Calculated Planning focused matrix should allow swapping after the debuff is queued.");
            planningBoard.TryRotate(1);
            planningBoard.TryRotate(1);
            planningBoard.TryRotate(1);
            planningBoard.TryRotate(1);
            Expect(planningBoard.HardDrop(), "Calculated Planning focused matrix should lock the swapped-in debuffed piece.");
            var debuffedPieceId = planningLocks.Last().PieceId;
            Expect(planningBoard.IsPieceScoreDebuffed(debuffedPieceId), "Calculated Planning should apply the queued debuff to the next locked piece.");
            Expect(planningEvents.Count(trigger => trigger.Modifier == StoryModifier.CalculatedPlanning && trigger.State == "QUEUED") == 1, "Calculated Planning should not retrigger while a debuff is already queued.");
            Expect(planningEvents.Any(trigger => trigger.Modifier == StoryModifier.CalculatedPlanning && trigger.State == "APPLIED"), "Calculated Planning should emit APPLIED when the queued debuff lands on a piece.");
            var beforePenaltyScore = planningBoard.Score;
            FillBottomLine(planningBoard, PieceType.O, pieceIdStart: 7600);
            planningBoard.PieceIds[PieceDefinitions.TotalRows - 1, 0] = debuffedPieceId;
            Expect(planningBoard.ClearLines() == 1, "Calculated Planning focused matrix should clear a row containing the debuffed piece.");
            Expect(planningBoard.Score - beforePenaltyScore == 55, "Calculated Planning should reduce row-clear points for rows touching the debuffed block at difficulty tier 3.");
            Expect(planningSystem.BuildEnemyAbilityStatus().Contains("score"), "Calculated Planning should report score reduction status.");

            var precisionSpec = ModifierSpec("harness-precision-focused", 3, StoryModifier.PrecisionPressure);
            var precisionBoard = new BoardState(new[] { PieceType.T }, seed: 4104);
            var precisionSystem = new StoryModifierSystem(precisionSpec, precisionBoard, seed: 4104);
            var precisionEvents = new List<StoryModifierTriggerEvent>();
            precisionSystem.OnModifierTriggered += trigger => precisionEvents.Add(trigger);
            precisionBoard.TryMove(0, 1);
            precisionBoard.TryMove(0, 1);
            Expect(precisionBoard.LockPiece(), "Precision Pressure focused matrix should lock an unsupported piece.");
            Expect(precisionBoard.GetGarbageCells().Count > 0, "Precision Pressure should seed cells from unsupported overhangs.");
            Expect(precisionSystem.BuildEnemyAbilityStatus().Contains("overhangs"), "Precision Pressure should report overhang trigger progress.");
            Expect(precisionEvents.Any(trigger => trigger.Modifier == StoryModifier.PrecisionPressure && trigger.State == "TRIGGER"), "Precision Pressure should emit a trigger event when unsupported cells seed enemies.");

            var blindedSpec = ModifierSpec("harness-blinded", 10, StoryModifier.GhostFlicker);
            var blindedSystem = new StoryModifierSystem(blindedSpec, new BoardState(new[] { PieceType.L }, seed: 4105), seed: 4105);
            blindedSystem.OnMatchStart();
            Expect(!blindedSystem.BlindedActive, "Blinded should start on cooldown.");
            Expect(blindedSystem.LockedPiecesVisible, "Blinded should leave locked blocks visible while cooling down.");
            var blindedStatus = blindedSystem.BuildEnemyAbilityStatus();
            Expect(blindedStatus.Contains("Blinded"), "Blinded should use its public ability name in enemy status.");
            Expect(!blindedStatus.Contains("Ghost Flicker"), "Blinded status should not expose the old Ghost Flicker internal name.");
            Expect(blindedStatus.Contains("[TIMER]"), "Blinded should expose cooldown timer status before it activates.");
            blindedSystem.Tick(12f);
            Expect(blindedSystem.BlindedActive, "Blinded should activate after its 12-second cooldown fills.");
            Expect(blindedSystem.LockedPiecesVisible, "Blinded should start active while locked blocks are visible.");
            blindedSystem.Tick(0.5f);
            Expect(!blindedSystem.LockedPiecesVisible, "Blinded should hide locked blocks after the first half-second flicker.");
            blindedSystem.Tick(0.5f);
            Expect(blindedSystem.LockedPiecesVisible, "Blinded should show locked blocks on the next half-second flicker.");
            blindedSystem.Tick(6.1f);
            Expect(!blindedSystem.BlindedActive, "Blinded should end after its scaled active duration.");
            Expect(blindedSystem.LockedPiecesVisible, "Blinded should force locked blocks visible after it ends.");

            var resilientSpec = ModifierSpec("harness-resilient", 10, StoryModifier.ResilientCells);
            var resilientBoard = new BoardState(new[] { PieceType.J }, seed: 4107);
            var resilientSystem = new StoryModifierSystem(resilientSpec, resilientBoard, seed: 4107);
            var resilientEvents = new List<StoryModifierTriggerEvent>();
            resilientSystem.OnModifierTriggered += trigger => resilientEvents.Add(trigger);
            resilientSystem.OnMatchStart();
            Expect(resilientBoard.GetTerritorySourceCells().Count == 1, "Resilient Cells should seed one permanent claimed source at match start.");
            var resilientSource = resilientBoard.GetTerritorySourceCells()[0];
            FillTerritoryClaimCandidates(resilientBoard, resilientSource, PieceType.J, pieceIdStart: 5000);
            resilientSystem.Tick(10f);
            Expect(resilientBoard.GetTerritoryClaimedCells().Count == 1, "Resilient Cells should claim a touching locked block after its timer fills.");
            FillLine(resilientBoard, PieceDefinitions.TotalRows - 5, PieceType.O, pieceIdStart: 5100);
            Expect(resilientBoard.ClearLines() == 1, "A normal line clear should still score while Resilient Cells are active.");
            Expect(resilientBoard.GetTerritoryClaimedCells().Count == 0, "Clearing a row should remove one temporary Resilient Cells claim.");
            Expect(resilientBoard.GetTerritorySourceCells().Count == 1, "The permanent Resilient Cells source should remain after line clears.");
            Expect(resilientSystem.BuildEnemyAbilityStatus().Contains("[TIMER]"), "Resilient Cells should report timer status.");
            Expect(resilientEvents.Any(trigger => trigger.Modifier == StoryModifier.ResilientCells && trigger.State == "SETUP"), "Resilient Cells should emit a setup trigger event.");
            Expect(resilientEvents.Any(trigger => trigger.Modifier == StoryModifier.ResilientCells && trigger.State == "CLAIM"), "Resilient Cells should emit a claim trigger event.");
            Expect(resilientEvents.Any(trigger => trigger.Modifier == StoryModifier.ResilientCells && trigger.State == "CLEARED"), "Resilient Cells should emit CLEARED when a line clear removes a claim.");

            var mutedSpec = ModifierSpec("harness-muted", 2, StoryModifier.MutedHints);
            var mutedSystem = new StoryModifierSystem(mutedSpec, new BoardState(new[] { PieceType.J }, seed: 4108), seed: 4108);
            Expect(mutedSystem.HintsMuted, "Muted Hints should hide assist/status hints.");
            Expect(mutedSystem.BuildEnemyAbilityStatus().Contains("[ON]"), "Muted Hints should report ON status.");

            var hungerSpec = ModifierSpec("harness-hunger-focused", 1, StoryModifier.HungerMeter);
            var hungerBoard = new BoardState(new[] { PieceType.S }, seed: 4109);
            var hungerSystem = new StoryModifierSystem(hungerSpec, hungerBoard, seed: 4109);
            var hungerEvents = new List<StoryModifierTriggerEvent>();
            hungerSystem.OnModifierTriggered += trigger => hungerEvents.Add(trigger);
            const int hungerTopPieceId = 8100;
            hungerBoard.Grid[PieceDefinitions.TotalRows - 8, 2] = (int)PieceType.S;
            hungerBoard.PieceIds[PieceDefinitions.TotalRows - 8, 2] = hungerTopPieceId;
            hungerBoard.Grid[PieceDefinitions.TotalRows - 7, 2] = (int)PieceType.S;
            hungerBoard.PieceIds[PieceDefinitions.TotalRows - 7, 2] = hungerTopPieceId;
            FillBottomLine(hungerBoard, PieceType.O, pieceIdStart: 8110);
            Expect(hungerBoard.ClearLines() == 1, "Insatiable Hunger setup should clear one row.");
            Expect(hungerBoard.GetLockedPieceGroups().Any(record => record.PieceId == hungerTopPieceId), "Low-tier Insatiable Hunger should wait for three cleared lines before consuming a block.");
            FillBottomLine(hungerBoard, PieceType.O, pieceIdStart: 8120);
            Expect(hungerBoard.ClearLines() == 1, "Insatiable Hunger setup should clear a second row.");
            Expect(hungerBoard.GetLockedPieceGroups().Any(record => record.PieceId == hungerTopPieceId), "Low-tier Insatiable Hunger should still wait after two cleared lines.");
            FillBottomLine(hungerBoard, PieceType.O, pieceIdStart: 8130);
            Expect(hungerBoard.ClearLines() == 1, "Insatiable Hunger setup should clear a third row.");
            Expect(!hungerBoard.GetLockedPieceGroups().Any(record => record.PieceId == hungerTopPieceId), "Insatiable Hunger should consume the whole top-layer block when its line requirement is met.");
            Expect(hungerEvents.Any(trigger => trigger.Modifier == StoryModifier.HungerMeter && trigger.State == "TRIGGER"), "Insatiable Hunger should emit a trigger event when it consumes a block.");
            Expect(hungerSystem.BuildEnemyAbilityStatus().Contains("Insatiable Hunger"), "Insatiable Hunger should use its public ability name in enemy status.");

            var scaledHungerSpec = ModifierSpec("harness-hunger-scaled", 10, StoryModifier.HungerMeter);
            var scaledHungerBoard = new BoardState(new[] { PieceType.S }, seed: 4119);
            var scaledHungerSystem = new StoryModifierSystem(scaledHungerSpec, scaledHungerBoard, seed: 4119);
            const int scaledHungerPieceId = 8150;
            scaledHungerBoard.Grid[PieceDefinitions.TotalRows - 2, 4] = (int)PieceType.S;
            scaledHungerBoard.PieceIds[PieceDefinitions.TotalRows - 2, 4] = scaledHungerPieceId;
            FillBottomLine(scaledHungerBoard, PieceType.O, pieceIdStart: 8160);
            Expect(scaledHungerBoard.ClearLines() == 1, "Scaled Insatiable Hunger setup should clear one row.");
            Expect(!scaledHungerBoard.GetLockedPieceGroups().Any(record => record.PieceId == scaledHungerPieceId), "High-tier Insatiable Hunger should trigger on every cleared line.");

            var sedationSpec = ModifierSpec("harness-sedation", 10, StoryModifier.SedationWindows);
            var sedationBoard = new BoardState(new[] { PieceType.T }, seed: 4110);
            var sedationSystem = new StoryModifierSystem(sedationSpec, sedationBoard, seed: 4110);
            sedationSystem.Tick(15f);
            Expect(sedationSystem.SedatingSpitActive, "Sedating Spit should activate after its 15-second cooldown fills.");
            Expect(sedationSystem.AssistsSuppressed, "Sedating Spit should suppress friendly assists while active.");
            Expect(sedationSystem.BuildEnemyAbilityStatus().Contains("Sedating Spit"), "Sedating Spit should use its public ability name in enemy status.");
            Expect(sedationSystem.BuildEnemyAbilityStatus().Contains("[ACTIVE]"), "Sedating Spit should report ACTIVE status.");
            FillBottomLine(sedationBoard, PieceType.T, pieceIdStart: 8200);
            Expect(sedationBoard.ClearLines() == 1, "Sedating Spit focused matrix should clear a row while active.");
            Expect(!sedationSystem.SedatingSpitActive, "Clearing a row should end Sedating Spit early.");
            sedationSystem.Tick(15f);
            Expect(sedationSystem.SedatingSpitActive, "Sedating Spit should reactivate after cooldown following an early clear.");
            sedationSystem.Tick(8.1f);
            Expect(!sedationSystem.SedatingSpitActive, "High-tier Sedating Spit should end after its scaled 8-second duration.");

            var assistSuppression = new AssistEffectSystem();
            var assistBoard = new BoardState(new[] { PieceType.T, PieceType.Z }, seed: 41101);
            assistSuppression.OnPieceLocked(new PieceLockEvent(1, PieceType.Z, 0, Array.Empty<Vector2Int>(), Vector2Int.zero, cameFromHold: true), assistBoard, _ => { });
            assistSuppression.OnPieceLocked(new PieceLockEvent(2, PieceType.Z, 0, Array.Empty<Vector2Int>(), Vector2Int.zero, cameFromHold: true), assistBoard, _ => { });
            Expect(assistSuppression.NextHeldPlacementWillTrigger, "Assist suppression setup should arm the next held placement.");
            assistSuppression.SuppressAndReset();
            Expect(!assistSuppression.NextHeldPlacementWillTrigger && assistSuppression.HeldPlacementsUntilTrigger == AssistEffectSystem.TriggerEvery, "Sedating Spit should clear charged and partial assist progress.");
            Expect(assistSuppression.OnPieceLocked(new PieceLockEvent(3, PieceType.Z, 0, Array.Empty<Vector2Int>(), Vector2Int.zero, cameFromHold: true), assistBoard, _ => { }, assistsSuppressed: true) == null, "Sedating Spit should prevent assist activation while active.");
            Expect(assistSuppression.HeldPlacementsUntilTrigger == AssistEffectSystem.TriggerEvery, "Sedating Spit should prevent assist charge progress while active.");

            var adrenalineSpec = ModifierSpec("harness-adrenaline-focused", 4, StoryModifier.AdrenalineMonitor, StoryModifier.HungerMeter);
            var adrenalineBoard = new BoardState(new[] { PieceType.I }, seed: 4111);
            var adrenalineSystem = new StoryModifierSystem(adrenalineSpec, adrenalineBoard, seed: 4111);
            var adrenalineEvents = new List<StoryModifierTriggerEvent>();
            adrenalineSystem.OnModifierTriggered += trigger => adrenalineEvents.Add(trigger);
            Expect(Mathf.Approximately(adrenalineSystem.GravityMultiplier, 1f), "Adrenaline Rush should not directly accelerate gravity.");
            adrenalineSystem.Tick(20f);
            Expect(adrenalineEvents.Any(trigger => trigger.Modifier == StoryModifier.AdrenalineMonitor && trigger.State == "ACTIVE"), "Adrenaline Rush should emit ACTIVE after its cooldown fills.");
            Expect(adrenalineSystem.BuildEnemyAbilityStatus().Contains("Adrenaline Rush") && adrenalineSystem.BuildEnemyAbilityStatus().Contains("[ACTIVE]"), "Adrenaline Rush should report ACTIVE status while boosting enemy abilities.");
            const int adrenalineFocusedPieceId = 8250;
            adrenalineBoard.Grid[PieceDefinitions.TotalRows - 5, 5] = (int)PieceType.S;
            adrenalineBoard.PieceIds[PieceDefinitions.TotalRows - 5, 5] = adrenalineFocusedPieceId;
            FillBottomLine(adrenalineBoard, PieceType.O, pieceIdStart: 8260);
            Expect(adrenalineBoard.ClearLines() == 1, "Focused Adrenaline Rush setup should clear one row.");
            Expect(!adrenalineBoard.GetLockedPieceGroups().Any(record => record.PieceId == adrenalineFocusedPieceId), "Adrenaline Rush should temporarily enhance Insatiable Hunger line requirements.");
            adrenalineSystem.Tick(11.1f);
            Expect(adrenalineEvents.Any(trigger => trigger.Modifier == StoryModifier.AdrenalineMonitor && trigger.State == "END"), "Adrenaline Rush should emit END after its fixed duration.");

            var relaySpec = ModifierSpec("harness-retired-relay-focused", 5, StoryModifier.SignalRelay);
            var relayBoard = new BoardState(new[] { PieceType.I }, seed: 4112);
            var relaySystem = new StoryModifierSystem(relaySpec, relayBoard, seed: 4112);
            var relayEvents = new List<StoryModifierTriggerEvent>();
            relaySystem.OnModifierTriggered += trigger => relayEvents.Add(trigger);
            relaySystem.OnMatchStart();
            relaySystem.Tick(40f);
            Expect(!relaySystem.BuildEnemyAbilityStatus().Contains("Signal Relay"), "Retired Signal Relay should not show status for legacy data.");
            Expect(!relayEvents.Any(trigger => trigger.Modifier == StoryModifier.SignalRelay), "Retired Signal Relay should not emit trigger events for legacy data.");

            foreach (var chapter in StoryCatalog.Chapters)
            {
                Expect(!chapter.Modifiers.Contains(StoryModifier.SignalRelay), $"Story chapter {chapter.Id} should not declare retired Signal Relay.");
            }

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
                StoryModifier.GhostFlicker => "Blinded",
                StoryModifier.EcholocationDim => "Echolocation Dim",
                StoryModifier.ResilientCells => "Resilient Cells",
                StoryModifier.MutedHints => "Muted Hints",
                StoryModifier.HungerMeter => "Insatiable Hunger",
                StoryModifier.SedationWindows => "Sedating Spit",
                StoryModifier.AdrenalineMonitor => "Adrenaline Rush",
                StoryModifier.SignalRelay => string.Empty,
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
            var story = LoadGameManagerForMode(MonStackaMode.Story, friendlyAbilitiesEnabled: false, storyChapterId: "3.3");
            var chapter = StoryCatalog.GetChapter("3.3");
            Expect(chapter != null, "Story 3.3 should exist for runtime HUD sweep.");
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
            hud.RenderStoryEnemyStatus("<color=#ffcf74>Guard Pressure</color> [ON] pressure row in 6s\n<color=#ff7cc8>Resilient Cells</color> [SETUP] source seeded");
            Expect(enemyText.text.Contains("[ON]") && enemyText.text.Contains("[SETUP]"), "Story enemy HUD should render explicit state tags.");
            hud.ShowEnemyModifierTrigger("Insatiable Hunger", "TRIGGER", "ate S block #7 (3 cells)");
            var triggerText = RequireRect("StoryEnemyTriggerCue").GetComponent<Text>();
            Expect(triggerText.gameObject.activeInHierarchy, "Story enemy HUD should show a trigger cue when an enemy ability fires.");
            Expect(triggerText.text.Contains("Insatiable Hunger") && triggerText.text.Contains("TRIGGER"), "Story enemy trigger cue should name the modifier and trigger state.");

            var territoryRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>()
                .Where(renderer => renderer && renderer.gameObject.scene.IsValid() && renderer.name.StartsWith("GarbageCell", StringComparison.Ordinal))
                .ToArray();
            Expect(territoryRenderers.Length > 0, "Story 3.3 should render seeded Resilient Cells claims.");
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
                "one permanent claimed source cell",
                "tries to claim one random locked block touching the claimed area",
                "The whole target block becomes claimed",
                "removes one temporary claimed block, oldest first",
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
            Expect(!story.IsPaused, "Home navigation should immediately leave gameplay pause state before the scene unloads.");
            Expect(!story.IsRestartConfirmActive, "Home navigation should clear restart confirmation before the scene unloads.");
            Expect(!story.IsEndRunPanelActive, "Home navigation should clear end-run state before the scene unloads.");
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
            board.SourceCellXs[survivorRow, 0] = 1;
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
            Expect(record.SourceCells.Count == 1 && record.SourceCells[0] == new Vector2Int(1, 0), "Surviving partial cell should keep its original art source coordinate.");
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
            board.SourceCellXs[survivorRow, 0] = 1;
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
            Expect(survivorRecord.SourceCells.Count == 1 && survivorRecord.SourceCells[0] == new Vector2Int(1, 0), "Runtime survivor should preserve source-cell art after clear.");

            var survivorSkin = lockedSkins.FirstOrDefault(skin => skin.PieceId == survivorPieceId);
            Expect(survivorSkin != null, "Runtime survivor should have a visible PieceSkin.");
            Expect(survivorSkin.gameObject.activeInHierarchy, "Runtime survivor PieceSkin should be active in the scene.");
            Expect(!survivorSkin.BodyBuildUsesFullBoxSprite, "Runtime partial survivor should not fall back to a full-box sprite.");
            Expect(survivorSkin.RequiresManualUpdate, "Runtime partial survivor should retain animated visual systems.");
            Expect(survivorSkin.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer => renderer && renderer.sprite), "Runtime survivor should have sprite renderers.");
            Expect(survivorSkin.GetComponentsInChildren<FacialPartAnimator>(true).Any(animator => animator && animator.Animates), "Runtime partial survivor should keep animated monster feature layers after line clear.");

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

        private static void VerifyAbilityFeedbackVisualState()
        {
            var manager = LoadGameManagerForMode(MonStackaMode.Ogbm, friendlyAbilitiesEnabled: true);
            var board = manager.Board;

            ArmNextHeldAssistWithoutCommit(manager, "Ability feedback visual state");
            InvokePrivate(manager, "UpdatePreviewViews");
            Expect(manager.AssistSystem.NextHeldPlacementWillTrigger, "Ability feedback should arm the next held placement before visual checks.");
            Expect(IsSceneObjectActive("AbilityReadyInnerGlow"), "Hold box should glow while an armed assist is still waiting in hold.");
            Expect(!IsSceneObjectActive("AssistBoardActivationCue"), "Board ability cue should stay hidden until the armed held piece is deployed.");

            Expect(board.TrySwapHoldWithUpcoming(0), "Ability feedback should allow swapping an armed hold with the next queue slot.");
            InvokePrivate(manager, "UpdatePreviewViews");
            Expect(manager.AssistSystem.NextHeldPlacementWillTrigger, "Hold queue swap should not consume the armed assist charge.");
            Expect(IsSceneObjectActive("AbilityReadyInnerGlow"), "Hold glow should survive queue swapping while the ability is still held.");

            Expect(board.TryHold(), "Ability feedback should deploy the armed held piece.");
            InvokePrivate(manager, "UpdatePreviewViews");
            InvokePrivate(manager, "UpdateVisuals");
            Expect(manager.AssistSystem.NextHeldPlacementWillTrigger, "Deploying the armed held piece should preserve the assist charge until lock.");
            Expect(!IsSceneObjectActive("AbilityReadyInnerGlow"), "Hold glow should clear from the replacement hold piece once the armed piece is on the board.");
            Expect(IsSceneObjectActive("AssistBoardActivationCue"), "Board ability cue should appear when the armed held piece is deployed.");

            var scoreBeforeLock = board.Score;
            InvokePrivate(manager, "HardDropAndSpawn");
            InvokePrivate(manager, "UpdateVisuals");
            Expect(!manager.AssistSystem.NextHeldPlacementWillTrigger, "Assist glow/counter should clear after the armed held piece locks.");
            Expect(manager.AssistSystem.HeldPlacementsUntilTrigger == AssistEffectSystem.TriggerEvery, "Assist counter should reset after the board commit.");
            Expect(board.Score > scoreBeforeLock, "Board commit should award friendly assist points.");
            Expect(!IsSceneObjectActive("AbilityReadyInnerGlow"), "Hold glow should remain clear after assist commit.");
            Expect(IsSceneObjectActive("AssistBoardActivationCue"), "Board ability cue should linger briefly after assist commit.");
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

        private static void VerifyRuntimeReplayDriverSweep()
        {
            var scenarios = new[]
            {
                new ReplayScenario(
                    "O.G.B.M. classic replay",
                    MonStackaMode.Ogbm,
                    zany: false,
                    chapter: null,
                    new ReplayAction(ReplayActionKind.MoveLeft, repeat: 8),
                    new ReplayAction(ReplayActionKind.MoveRight, repeat: 16),
                    new ReplayAction(ReplayActionKind.RotateCw),
                    new ReplayAction(ReplayActionKind.SoftDrop, repeat: 2),
                    new ReplayAction(ReplayActionKind.HardDrop),
                    new ReplayAction(ReplayActionKind.Pause),
                    new ReplayAction(ReplayActionKind.Resume),
                    new ReplayAction(ReplayActionKind.RestartPrompt),
                    new ReplayAction(ReplayActionKind.CancelRestart),
                    new ReplayAction(ReplayActionKind.ForceGameOver)
                ),
                new ReplayScenario(
                    "O.G.B.M. zany assist replay",
                    MonStackaMode.Ogbm,
                    zany: true,
                    chapter: null,
                    new ReplayAction(ReplayActionKind.MoveRight, repeat: 4),
                    new ReplayAction(ReplayActionKind.TriggerFriendlyAssist),
                    new ReplayAction(ReplayActionKind.HardDrop)
                ),
                new ReplayScenario(
                    "X(4)-LINES classic replay",
                    MonStackaMode.Sprint40,
                    zany: false,
                    chapter: null,
                    new ReplayAction(ReplayActionKind.MoveLeft, repeat: 5),
                    new ReplayAction(ReplayActionKind.RotateCcw),
                    new ReplayAction(ReplayActionKind.HardDrop),
                    new ReplayAction(ReplayActionKind.MoveRight, repeat: 5),
                    new ReplayAction(ReplayActionKind.HardDrop)
                ),
                new ReplayScenario(
                    "X(4)-LINES zany assist replay",
                    MonStackaMode.Sprint40,
                    zany: true,
                    chapter: null,
                    new ReplayAction(ReplayActionKind.TriggerFriendlyAssist),
                    new ReplayAction(ReplayActionKind.OpenSettings),
                    new ReplayAction(ReplayActionKind.CloseSettings)
                ),
                new ReplayScenario(
                    "Training zany toggle replay",
                    MonStackaMode.Training,
                    zany: false,
                    chapter: null,
                    new ReplayAction(ReplayActionKind.ToggleTrainingZany),
                    new ReplayAction(ReplayActionKind.MoveLeft, repeat: 2),
                    new ReplayAction(ReplayActionKind.RotateCw),
                    new ReplayAction(ReplayActionKind.HardDrop),
                    new ReplayAction(ReplayActionKind.RestartPrompt)
                ),
                new ReplayScenario(
                    "Story 1.1 replay",
                    MonStackaMode.Story,
                    zany: false,
                    chapter: "1.1",
                    new ReplayAction(ReplayActionKind.MoveLeft, repeat: 3),
                    new ReplayAction(ReplayActionKind.Hold),
                    new ReplayAction(ReplayActionKind.HardDrop),
                    new ReplayAction(ReplayActionKind.RestartPrompt),
                    new ReplayAction(ReplayActionKind.CancelRestart)
                ),
                new ReplayScenario(
                    "Story 1.2 replay",
                    MonStackaMode.Story,
                    zany: false,
                    chapter: "1.2",
                    new ReplayAction(ReplayActionKind.Hold),
                    new ReplayAction(ReplayActionKind.HardDrop),
                    new ReplayAction(ReplayActionKind.Hold),
                    new ReplayAction(ReplayActionKind.SwapHold1),
                    new ReplayAction(ReplayActionKind.HardDrop)
                ),
                new ReplayScenario(
                    "Story 1.3 modifier replay",
                    MonStackaMode.Story,
                    zany: false,
                    chapter: "1.3",
                    new ReplayAction(ReplayActionKind.MoveRight, repeat: 3),
                    new ReplayAction(ReplayActionKind.Hold),
                    new ReplayAction(ReplayActionKind.HardDrop),
                    new ReplayAction(ReplayActionKind.OpenSettings),
                    new ReplayAction(ReplayActionKind.CloseSettings),
                    new ReplayAction(ReplayActionKind.HardDrop)
                ),
            };

            foreach (var scenario in scenarios)
            {
                RunReplayScenario(scenario);
            }

            RunLineClearReplayScenario();
            RunFocusedEnemyModifierReplayScenario();
        }

        private static void VerifyRuntimeScreenshotCheckpointSweep()
        {
            var checkpointDir = Path.Combine(ReportDir, "ReplayCheckpoints");
            Directory.CreateDirectory(checkpointDir);
            foreach (var oldFile in Directory.EnumerateFiles(checkpointDir, "*.png"))
            {
                File.Delete(oldFile);
            }

            var startManager = LoadGameManagerForMode(MonStackaMode.Story, friendlyAbilitiesEnabled: false, storyChapterId: "1.3");
            InvokePrivate(startManager, "UpdateVisuals");
            var startPath = CaptureRuntimeCheckpoint(checkpointDir, "story-start", "Story checkpoint start");
            AssertCheckpointScreenshotReadable(startPath, "Story checkpoint start");

            var lineClearManager = LoadGameManagerForMode(MonStackaMode.Story, friendlyAbilitiesEnabled: false, storyChapterId: "1.3");
            var lineClearBoard = lineClearManager.Board;
            lineClearBoard.Reset();
            var clearRow = PieceDefinitions.TotalRows - 8;
            var survivorRow = clearRow - 1;
            lineClearBoard.Grid[survivorRow, 2] = (int)PieceType.T;
            lineClearBoard.PieceIds[survivorRow, 2] = 61099;
            lineClearBoard.SourceCellXs[survivorRow, 2] = 1;
            lineClearBoard.SourceCellYs[survivorRow, 2] = 0;
            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                lineClearBoard.Grid[clearRow, col] = (int)PieceType.O;
                lineClearBoard.PieceIds[clearRow, col] = 61100 + col;
                lineClearBoard.SourceCellXs[clearRow, col] = col % 2;
                lineClearBoard.SourceCellYs[clearRow, col] = 0;
            }
            InvokePrivate(lineClearBoard, "RebuildLockedPiecesFromGrid");
            InvokePrivate(lineClearManager, "RebuildBoardViews");
            InvokePrivate(lineClearManager, "UpdateVisuals");
            var beforeLineClearPath = CaptureRuntimeCheckpoint(checkpointDir, "before-line-clear", "Story checkpoint before line clear");
            AssertCheckpointScreenshotReadable(beforeLineClearPath, "Story checkpoint before line clear");
            Expect(lineClearBoard.ClearLines() == 1, "Screenshot checkpoint should clear a prepared line.");
            InvokePrivate(lineClearManager, "RebuildBoardViews");
            InvokePrivate(lineClearManager, "UpdateVisuals");
            var lineClearPath = CaptureRuntimeCheckpoint(checkpointDir, "after-line-clear", "Story checkpoint after line clear");
            AssertCheckpointScreenshotReadable(lineClearPath, "Story checkpoint after line clear");
            Expect(lineClearBoard.Lines == 1, "Story checkpoint after line clear should update board line count.");
            AssertScreenshotsDiffer(startPath, lineClearPath, "start vs after-line-clear checkpoint");

            var assistManager = LoadGameManagerForMode(MonStackaMode.Ogbm, friendlyAbilitiesEnabled: true);
            TriggerFriendlyAssistThroughRuntime(assistManager, "Screenshot checkpoint assist trigger");
            InvokePrivate(assistManager, "UpdateVisuals");
            var assistPath = CaptureRuntimeCheckpoint(checkpointDir, "after-assist", "Zany checkpoint after assist trigger");
            AssertCheckpointScreenshotReadable(assistPath, "Zany checkpoint after assist trigger");
            AssertScreenshotsDiffer(startPath, assistPath, "start vs assist checkpoint");

            var settingsManager = LoadGameManagerForMode(MonStackaMode.Story, friendlyAbilitiesEnabled: false, storyChapterId: "1.3");
            settingsManager.PauseIfRunning();
            OpenReplaySettings("Screenshot checkpoint settings");
            InvokePrivate(settingsManager, "UpdateVisuals");
            var settingsPath = CaptureRuntimeCheckpoint(checkpointDir, "pause-settings", "Story checkpoint pause settings");
            AssertCheckpointScreenshotReadable(settingsPath, "Story checkpoint pause settings");
            AssertScreenshotsDiffer(startPath, settingsPath, "start vs settings checkpoint");
            CloseReplaySettings("Screenshot checkpoint settings");

            var gameOverManager = LoadGameManagerForMode(MonStackaMode.Ogbm, friendlyAbilitiesEnabled: true);
            ForceRuntimeGameOver(gameOverManager, "Screenshot checkpoint game over");
            var gameOverPath = CaptureRuntimeCheckpoint(checkpointDir, "game-over", "O.G.B.M. checkpoint game over");
            AssertCheckpointScreenshotReadable(gameOverPath, "O.G.B.M. checkpoint game over");
            Expect(gameOverManager.IsGameOver && gameOverManager.HasEndRunUi && gameOverManager.IsEndRunPanelActive, "O.G.B.M. checkpoint game over should activate the end-run UI state.");
        }

        private static void VerifyRuntimeSoakReplaySweep()
        {
            RunRuntimeSoakReplay(
                "O.G.B.M. zany soak",
                LoadGameManagerForMode(MonStackaMode.Ogbm, friendlyAbilitiesEnabled: true),
                new ReplayScenario("O.G.B.M. zany soak", MonStackaMode.Ogbm, true, null),
                maxSteps: 64);
            RunRuntimeSoakReplay(
                "Story 1.3 soak",
                LoadGameManagerForMode(MonStackaMode.Story, friendlyAbilitiesEnabled: false, storyChapterId: "1.3"),
                new ReplayScenario("Story 1.3 soak", MonStackaMode.Story, false, "1.3"),
                maxSteps: 64);
        }

        private static void RunRuntimeSoakReplay(string name, GameManager manager, ReplayScenario scenario, int maxSteps)
        {
            var board = manager.Board;
            var lastScore = board.Score;
            var startingPieces = board.PiecesPlaced;

            for (var step = 0; step < maxSteps && !board.IsGameOver(); step += 1)
            {
                var direction = step % 2 == 0 ? -1 : 1;
                for (var move = 0; move < 8; move += 1)
                {
                    board.TryMove(direction, 0);
                }

                if (step % 3 == 0)
                {
                    board.TryRotate(1);
                }

                if (step % 7 == 0 && manager.CurrentMode != MonStackaMode.Training)
                {
                    board.TryHold();
                }

                InvokePrivate(manager, "HardDropAndSpawn");
                InvokePrivate(manager, "UpdateVisuals");
                AssertReplayInvariants(manager, scenario, $"soak step {step}", ref lastScore);
            }

            Expect(board.PiecesPlaced > startingPieces + 8 || board.IsGameOver(), $"{name}: soak should place many pieces or end in explicit game over.");
            if (board.IsGameOver())
            {
                InvokePrivate(manager, "HandleRunCompletion");
                Expect(manager.HasEndRunUi && manager.IsEndRunPanelActive, $"{name}: soak game over should show the end-run panel.");
            }
        }

        private static void RunReplayScenario(ReplayScenario scenario)
        {
            var manager = LoadGameManagerForMode(scenario.Mode, scenario.Zany, scenario.Chapter);
            var board = manager.Board;
            Expect(board != null, $"{scenario.Name}: board should exist.");
            Expect(manager.FriendlyAbilitiesEnabled == AssistEffectSystem.IsEnabledFor(scenario.Mode, scenario.Zany), $"{scenario.Name}: friendly ability state should match mode rules.");
            AssertCanReachOuterLanesRuntime(manager, scenario.Name);
            var lastScore = board.Score;
            AssertReplayInvariants(manager, scenario, "launch", ref lastScore);
            foreach (var action in scenario.Actions)
            {
                ApplyReplayAction(manager, scenario, action);
                InvokePrivate(manager, "UpdateVisuals");
                AssertReplayInvariants(manager, scenario, action.Kind.ToString(), ref lastScore);
            }
        }

        private static void ApplyReplayAction(GameManager manager, ReplayScenario scenario, ReplayAction action)
        {
            for (var iteration = 0; iteration < action.Repeat; iteration += 1)
            {
                var board = manager.Board;
                var succeeded = true;
                switch (action.Kind)
                {
                    case ReplayActionKind.MoveLeft:
                        succeeded = board.TryMove(-1, 0);
                        break;
                    case ReplayActionKind.MoveRight:
                        succeeded = board.TryMove(1, 0);
                        break;
                    case ReplayActionKind.RotateCw:
                        succeeded = board.TryRotate(1);
                        break;
                    case ReplayActionKind.RotateCcw:
                        succeeded = board.TryRotate(-1);
                        break;
                    case ReplayActionKind.SoftDrop:
                        succeeded = board.TrySoftDrop();
                        break;
                    case ReplayActionKind.Hold:
                        succeeded = board.TryHold();
                        break;
                    case ReplayActionKind.SwapHold1:
                        succeeded = board.TrySwapHoldWithUpcoming(0);
                        break;
                    case ReplayActionKind.SwapHold2:
                        succeeded = board.TrySwapHoldWithUpcoming(1);
                        break;
                    case ReplayActionKind.SwapHold3:
                        succeeded = board.TrySwapHoldWithUpcoming(2);
                        break;
                    case ReplayActionKind.HardDrop:
                        InvokePrivate(manager, "HardDropAndSpawn");
                        succeeded = board.IsGameOver() || board.HasActivePiece;
                        break;
                    case ReplayActionKind.Pause:
                        manager.PauseIfRunning();
                        succeeded = manager.IsPaused;
                        break;
                    case ReplayActionKind.Resume:
                        manager.ResumeGame();
                        succeeded = !manager.IsPaused;
                        break;
                    case ReplayActionKind.OpenSettings:
                        manager.PauseIfRunning();
                        OpenReplaySettings(scenario.Name);
                        var openPanel = FindSceneRect("GameSettingsPanel");
                        succeeded = openPanel != null && openPanel.gameObject.activeInHierarchy;
                        break;
                    case ReplayActionKind.CloseSettings:
                        CloseReplaySettings(scenario.Name);
                        var closedPanel = FindSceneRect("GameSettingsPanel");
                        succeeded = closedPanel == null || !closedPanel.gameObject.activeInHierarchy;
                        break;
                    case ReplayActionKind.RestartPrompt:
                        manager.RequestRestart();
                        succeeded = scenario.Mode == MonStackaMode.Training
                            ? !manager.IsRestartConfirmActive && manager.Board.PiecesPlaced == 0
                            : manager.IsPaused && manager.IsRestartConfirmActive;
                        break;
                    case ReplayActionKind.CancelRestart:
                        InvokePrivate(manager, "CancelRestartConfirmation");
                        succeeded = manager.IsPaused && !manager.IsRestartConfirmActive;
                        break;
                    case ReplayActionKind.ToggleTrainingZany:
                        manager.ToggleFriendlyAbilitiesAndRestart();
                        succeeded = manager.CanToggleFriendlyAbilities &&
                            manager.FriendlyAbilitiesEnabled &&
                            manager.Board.PiecesPlaced == 0 &&
                            manager.Board.HasActivePiece;
                        break;
                    case ReplayActionKind.TriggerFriendlyAssist:
                        TriggerFriendlyAssistThroughRuntime(manager, scenario.Name);
                        succeeded = manager.AssistSystem == null || manager.AssistSystem.HeldPlacementsUntilTrigger == AssistEffectSystem.TriggerEvery;
                        break;
                    case ReplayActionKind.ForceGameOver:
                        ForceRuntimeGameOver(manager, scenario.Name);
                        succeeded = manager.IsGameOver && manager.HasEndRunUi && manager.IsEndRunPanelActive;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action.Kind), action.Kind, "Unknown replay action.");
                }

                if (action.ExpectSuccess)
                {
                    if (!succeeded && IsBoundaryTolerantReplayAction(action.Kind))
                    {
                        succeeded = true;
                    }

                    Expect(succeeded, $"{scenario.Name}: replay action {action.Kind} should succeed on iteration {iteration + 1}/{action.Repeat}.");
                }
                else
                {
                    Expect(!succeeded, $"{scenario.Name}: replay action {action.Kind} should fail on iteration {iteration + 1}/{action.Repeat}.");
                }
            }
        }

        private static bool IsBoundaryTolerantReplayAction(ReplayActionKind kind) =>
            kind == ReplayActionKind.MoveLeft || kind == ReplayActionKind.MoveRight;

        private static void AssertReplayInvariants(GameManager manager, ReplayScenario scenario, string checkpoint, ref int lastScore)
        {
            var board = manager.Board;
            Expect(board.Score >= lastScore, $"{scenario.Name} {checkpoint}: score should never decrease.");
            lastScore = board.Score;

            if (!board.IsGameOver())
            {
                Expect(board.HasActivePiece, $"{scenario.Name} {checkpoint}: alive replay should keep an active piece.");
                Expect(board.NextQueue.Count > 0, $"{scenario.Name} {checkpoint}: next queue should stay populated.");
            }

            AssertVisibleRuntimePieceState($"{scenario.Name} {checkpoint}");
            AssertLockedViewsMatchBoard($"{scenario.Name} {checkpoint}");
            AssertTextReadable("ScoreLabel", minFontSize: 14);
            AssertTextReadable("HoldLabel", minFontSize: 14);
            AssertTextReadable("NextLabel", minFontSize: 14);

            if (scenario.Mode == MonStackaMode.Story)
            {
                AssertStoryReplayStatus(manager, scenario, checkpoint);
            }
        }

        private static void AssertCanReachOuterLanesRuntime(GameManager manager, string context)
        {
            var pieceType = manager.Board.ActivePiece.Type;
            Expect(CanReachOuterColumn(pieceType, leftSide: true), $"{context}: {pieceType} should be able to reach the left outer lane.");
            Expect(CanReachOuterColumn(pieceType, leftSide: false), $"{context}: {pieceType} should be able to reach the right outer lane.");
        }

        private static void AssertStoryReplayStatus(GameManager manager, ReplayScenario scenario, string checkpoint)
        {
            var chapter = StoryCatalog.GetChapter(scenario.Chapter);
            Expect(chapter != null, $"{scenario.Name} {checkpoint}: story chapter should exist.");
            AssertStoryManagerInvariants(manager, chapter, $"{scenario.Name} {checkpoint}");

            var storyModifiers = GetField(manager.GetType(), manager, "storyModifiers") as StoryModifierSystem;
            Expect(storyModifiers != null, $"{scenario.Name} {checkpoint}: story modifier system should exist.");
            var status = storyModifiers.BuildEnemyAbilityStatus();
            Expect(!status.Contains("No enemy modifiers", StringComparison.OrdinalIgnoreCase), $"{scenario.Name} {checkpoint}: enemy status should not be empty.");
            Expect(status.Contains("[") && status.Contains("]"), $"{scenario.Name} {checkpoint}: enemy status should include trigger/state tags.");
            foreach (var modifier in chapter.Modifiers)
            {
                Expect(status.Contains(StoryModifierLabelForHarness(modifier)), $"{scenario.Name} {checkpoint}: status should include {modifier}.");
            }
        }

        private static void AssertLockedViewsMatchBoard(string context)
        {
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (!manager)
            {
                return;
            }

            var recordIds = manager.Board.GetLockedPieceGroups()
                .Where(record => record.Cells.Count > 0 && record.PieceId > 0)
                .Select(record => record.PieceId)
                .OrderBy(id => id)
                .ToArray();
            var skinIds = Resources.FindObjectsOfTypeAll<PieceSkin>()
                .Where(skin => skin && skin.gameObject.scene.IsValid() && skin.gameObject.scene.isLoaded && skin.PieceId > 0 && skin.gameObject.activeInHierarchy)
                .Select(skin => skin.PieceId)
                .OrderBy(id => id)
                .ToArray();
            Expect(skinIds.SequenceEqual(recordIds), $"{context}: locked visual PieceSkin ids should match board records. Rendered=[{string.Join(",", skinIds)}] Board=[{string.Join(",", recordIds)}]");
        }

        private static void TriggerFriendlyAssistThroughRuntime(GameManager manager, string context)
        {
            Expect(manager.AssistSystem != null, $"{context}: friendly assist replay requires assists enabled.");
            var board = manager.Board;
            var startingScore = board.Score;

            if (!board.HasHoldPiece)
            {
                Expect(board.TryHold(), $"{context}: assist replay should fill the hold box.");
                InvokePrivate(manager, "HardDropAndSpawn");
            }

            for (var heldPlacement = 1; heldPlacement <= AssistEffectSystem.TriggerEvery; heldPlacement += 1)
            {
                Expect(board.HasHoldPiece, $"{context}: assist replay should keep a hold piece before held placement {heldPlacement}.");
                Expect(board.TryHold(), $"{context}: assist replay should swap held piece for placement {heldPlacement}.");
                if (heldPlacement == AssistEffectSystem.TriggerEvery)
                {
                    Expect(manager.AssistSystem.NextHeldPlacementWillTrigger, $"{context}: assist glow/counter should be armed before the third held placement.");
                }

                InvokePrivate(manager, "HardDropAndSpawn");
                InvokePrivate(manager, "UpdateVisuals");
            }

            Expect(manager.AssistSystem.HeldPlacementsUntilTrigger == AssistEffectSystem.TriggerEvery, $"{context}: assist counter should reset after trigger.");
            Expect(!manager.AssistSystem.NextHeldPlacementWillTrigger, $"{context}: assist glow/counter should clear after trigger.");
            Expect(board.Score > startingScore, $"{context}: friendly assist trigger should award points through the runtime event path.");
        }

        private static void ArmNextHeldAssistWithoutCommit(GameManager manager, string context)
        {
            Expect(manager.AssistSystem != null, $"{context}: arming assist requires friendly abilities.");
            var board = manager.Board;

            if (!board.HasHoldPiece)
            {
                Expect(board.TryHold(), $"{context}: should fill hold before arming assist.");
                InvokePrivate(manager, "HardDropAndSpawn");
            }

            for (var heldPlacement = 1; heldPlacement < AssistEffectSystem.TriggerEvery; heldPlacement += 1)
            {
                Expect(board.HasHoldPiece, $"{context}: should have a hold piece before arming step {heldPlacement}.");
                Expect(board.TryHold(), $"{context}: should deploy held piece for arming step {heldPlacement}.");
                InvokePrivate(manager, "HardDropAndSpawn");
                InvokePrivate(manager, "UpdateVisuals");
            }

            Expect(manager.AssistSystem.NextHeldPlacementWillTrigger, $"{context}: next held placement should now be armed.");
            Expect(board.HasHoldPiece, $"{context}: armed assist should still have a held piece available.");
        }

        private static void ForceRuntimeGameOver(GameManager manager, string context)
        {
            var board = manager.Board;
            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                board.Grid[0, col] = (int)PieceType.I;
                board.PieceIds[0, col] = 30000 + col;
                board.SourceCellXs[0, col] = col % 4;
                board.SourceCellYs[0, col] = 0;
            }

            board.SpawnNext(PieceType.O);
            InvokePrivate(manager, "HandleRunCompletion");
            InvokePrivate(manager, "UpdateVisuals");
            Expect(board.IsGameOver(), $"{context}: forced top-out should put board in game-over state.");
            Expect(manager.HasEndRunUi && manager.IsEndRunPanelActive, $"{context}: game over should show the end-run panel.");
        }

        private static void OpenReplaySettings(string context)
        {
            var shell = UnityEngine.Object.FindFirstObjectByType<GameSceneShellController>();
            Expect(shell != null, $"{context}: replay should find game scene shell.");
            InvokePrivate(shell, "OpenSettings");
        }

        private static void CloseReplaySettings(string context)
        {
            var shell = UnityEngine.Object.FindFirstObjectByType<GameSceneShellController>();
            Expect(shell != null, $"{context}: replay should find game scene shell.");
            InvokePrivate(shell, "CloseSettings");
        }

        private static void RunLineClearReplayScenario()
        {
            var story = LoadGameManagerForMode(MonStackaMode.Story, friendlyAbilitiesEnabled: false, storyChapterId: "1.3");
            var board = story.Board;
            board.Reset();
            var survivorRow = PieceDefinitions.TotalRows - 2;
            const int survivorPieceId = 44100;
            board.Grid[survivorRow, 1] = (int)PieceType.T;
            board.PieceIds[survivorRow, 1] = survivorPieceId;
            board.SourceCellXs[survivorRow, 1] = 2;
            board.SourceCellYs[survivorRow, 1] = 0;
            FillBottomLine(board, PieceType.O, 44200);

            Expect(board.ClearLines() == 1, "Runtime replay line clear should clear exactly one prepared row.");
            InvokePrivate(story, "RebuildBoardViews");
            var lastScore = board.Score;
            AssertReplayInvariants(
                story,
                new ReplayScenario("Story line-clear replay", MonStackaMode.Story, false, "1.3"),
                "after line clear",
                ref lastScore
            );
            var survivor = board.GetLockedPieceGroups().FirstOrDefault(record => record.PieceId == survivorPieceId);
            Expect(survivor != null && survivor.Cells.Count == 1, "Runtime replay line clear should keep the surviving partial piece.");
            var ids = board.GetLockedPieceGroups().Select(record => record.PieceId).ToArray();
            Expect(ids.Distinct().Count() == ids.Length, "Runtime replay line clear should not duplicate locked piece record ids.");
        }

        private static void RunFocusedEnemyModifierReplayScenario()
        {
            var spec = new StoryChapterSpec
            {
                Id = "replay-focused-modifiers",
                Title = "Replay Focused Modifiers",
                DifficultyTier = 6,
                NextPreviewCount = 2,
                Modifiers = new[]
                {
                    StoryModifier.GuardPressure,
                    StoryModifier.CalculatedPlanning,
                    StoryModifier.PrecisionPressure,
                    StoryModifier.HungerMeter,
                    StoryModifier.ResilientCells,
                },
            };
            var board = new BoardState(new[] { PieceType.T, PieceType.O }, seed: 9401);
            var modifiers = new StoryModifierSystem(spec, board, seed: 9401);
            modifiers.OnMatchStart();
            Expect(board.GetGarbageCells().Count > 0, "Focused enemy replay should seed territory cells at match start.");
            Expect(board.TryRotate(1), "Focused enemy replay should rotate once.");
            Expect(board.TryRotate(1), "Focused enemy replay should rotate twice.");
            Expect(board.TryRotate(1), "Focused enemy replay should rotate three times.");
            Expect(board.HardDrop(), "Focused enemy replay should lock the rotated piece.");
            modifiers.Tick(20f);
            FillBottomLine(board, PieceType.O, 94500);
            board.ClearLines();

            var status = modifiers.BuildEnemyAbilityStatus();
            foreach (var modifier in spec.Modifiers)
            {
                Expect(status.Contains(StoryModifierLabelForHarness(modifier)), $"Focused enemy replay status should include {modifier}.");
            }
            Expect(status.Contains("[") && status.Contains("]"), "Focused enemy replay status should include trigger/state tags.");
            Expect(board.GetGarbageCells().Count > 0, "Focused enemy replay should leave visible enemy cells after triggers.");
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
                Expect(light >= 0, $"{context}: screenshot light-pixel counter should remain valid.");
                Expect(saturated >= sampled * 0.08f, $"{context}: screenshot should contain saturated block/UI colors.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string CaptureRuntimeCheckpoint(string directory, string name, string context)
        {
            var camera = Camera.main;
            Expect(camera != null, $"{context}: checkpoint capture should find a main camera.");
            var path = Path.Combine(directory, $"{name}.png");
            DeleteIfExists(path);

            var renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGBA32, mipChain: false);
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);
            }

            return path;
        }

        private static void AssertCheckpointScreenshotReadable(string screenshotPath, string context)
        {
            Expect(File.Exists(screenshotPath), $"{context}: checkpoint screenshot should exist.");
            var bytes = File.ReadAllBytes(screenshotPath);
            Expect(bytes.Length > 4096, $"{context}: checkpoint screenshot should not be tiny or empty.");

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Expect(ImageConversion.LoadImage(texture, bytes), $"{context}: checkpoint screenshot PNG should decode.");
                Expect(texture.width >= 800 && texture.height >= 450, $"{context}: checkpoint screenshot should be at least 800x450, got {texture.width}x{texture.height}.");

                var pixels = texture.GetPixels32();
                var stride = Mathf.Max(1, pixels.Length / 12000);
                var sampled = 0;
                var blueish = 0;
                var dark = 0;
                var buckets = new HashSet<int>();

                for (var index = 0; index < pixels.Length; index += stride)
                {
                    var pixel = pixels[index];
                    sampled += 1;
                    if (pixel.b > pixel.r + 12 && pixel.b > pixel.g + 4)
                    {
                        blueish += 1;
                    }

                    if (Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) < 55)
                    {
                        dark += 1;
                    }

                    buckets.Add(((pixel.r / 16) << 8) | ((pixel.g / 16) << 4) | (pixel.b / 16));
                }

                Expect(sampled > 0, $"{context}: checkpoint sampler should inspect pixels.");
                Expect(buckets.Count >= 12, $"{context}: checkpoint should have varied color buckets, got {buckets.Count}.");
                Expect(blueish >= sampled * 0.10f, $"{context}: checkpoint should include the blue scene background.");
                Expect(dark >= sampled * 0.02f, $"{context}: checkpoint should include dark panel/outline/depth pixels.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void AssertScreenshotsDiffer(string firstPath, string secondPath, string context)
        {
            Expect(File.Exists(firstPath), $"{context}: first screenshot should exist.");
            Expect(File.Exists(secondPath), $"{context}: second screenshot should exist.");
            var firstBytes = File.ReadAllBytes(firstPath);
            var secondBytes = File.ReadAllBytes(secondPath);
            Expect(!firstBytes.SequenceEqual(secondBytes), $"{context}: checkpoint screenshots should not be byte-identical.");

            var firstTexture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            var secondTexture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Expect(ImageConversion.LoadImage(firstTexture, firstBytes), $"{context}: first screenshot should decode.");
                Expect(ImageConversion.LoadImage(secondTexture, secondBytes), $"{context}: second screenshot should decode.");
                Expect(firstTexture.width == secondTexture.width && firstTexture.height == secondTexture.height, $"{context}: screenshots should share dimensions.");

                var firstPixels = firstTexture.GetPixels32();
                var secondPixels = secondTexture.GetPixels32();
                var stride = Mathf.Max(1, firstPixels.Length / 12000);
                var changed = 0;
                var sampled = 0;
                for (var index = 0; index < firstPixels.Length; index += stride)
                {
                    sampled += 1;
                    var a = firstPixels[index];
                    var b = secondPixels[index];
                    if (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 24)
                    {
                        changed += 1;
                    }
                }

                Expect(sampled > 0, $"{context}: screenshot diff should sample pixels.");
                Expect(changed >= sampled * 0.004f, $"{context}: checkpoint screenshots should differ meaningfully. Changed {changed}/{sampled}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstTexture);
                UnityEngine.Object.DestroyImmediate(secondTexture);
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
            FillLine(board, PieceDefinitions.TotalRows - 1, type, pieceIdStart);
        }

        private static void SeedClaimableNeighbor(BoardState board)
        {
            var source = board.GetTerritorySourceCells().First();
            var candidate = new Vector2Int(source.x, Mathf.Max(0, source.y - 1));
            board.Grid[candidate.y, candidate.x] = (int)PieceType.J;
            board.PieceIds[candidate.y, candidate.x] = 9700;
            board.SourceCellXs[candidate.y, candidate.x] = 0;
            board.SourceCellYs[candidate.y, candidate.x] = 0;
        }

        private static void FillLine(BoardState board, int row, PieceType type, int pieceIdStart)
        {
            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                board.Grid[row, col] = (int)type;
                board.PieceIds[row, col] = pieceIdStart + col;
                board.SourceCellXs[row, col] = col % 2;
                board.SourceCellYs[row, col] = col / 2;
            }
        }

        private static void FillTerritoryClaimCandidates(BoardState board, Vector2Int source, PieceType type, int pieceIdStart)
        {
            var candidates = new[]
            {
                new Vector2Int(source.x, source.y - 1),
                new Vector2Int(source.x, Mathf.Min(PieceDefinitions.TotalRows - 1, source.y + 1)),
                new Vector2Int(Mathf.Max(0, source.x - 1), source.y),
                new Vector2Int(Mathf.Min(PieceDefinitions.Columns - 1, source.x + 1), source.y),
                new Vector2Int(source.x, Mathf.Max(0, source.y - 2)),
            };

            var pieceId = pieceIdStart;
            foreach (var candidate in candidates.Distinct())
            {
                if (candidate.x < 0 || candidate.x >= PieceDefinitions.Columns || candidate.y < 0 || candidate.y >= PieceDefinitions.TotalRows)
                {
                    continue;
                }

                board.Grid[candidate.y, candidate.x] = (int)type;
                board.PieceIds[candidate.y, candidate.x] = pieceId++;
                board.SourceCellXs[candidate.y, candidate.x] = 0;
                board.SourceCellYs[candidate.y, candidate.x] = 0;
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

        private static bool IsSceneObjectActive(string objectName) =>
            Resources.FindObjectsOfTypeAll<Transform>()
                .Any(transform => transform &&
                    transform.gameObject.scene.IsValid() &&
                    transform.gameObject.scene.isLoaded &&
                    transform.name == objectName &&
                    transform.gameObject.activeInHierarchy);

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            Expect(method != null, $"{target.GetType().Name} should contain private method {methodName}.");
            method.Invoke(target, null);
        }

        private static object GetField(Type type, object target, string fieldName)
        {
            var field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            Expect(field != null, $"{type.Name} should contain private field {fieldName}.");
            return field.GetValue(target);
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
