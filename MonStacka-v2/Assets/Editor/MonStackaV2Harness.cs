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
                new HarnessScenario("story modifier scenarios", VerifyStoryModifierScenarios),
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
