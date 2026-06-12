using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonStacka.Core;
using MonStacka.UI;
using MonStacka.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonStacka.Editor
{
    public static class MonStackaV2Verification
    {
        private static readonly string Root = "Assets/MonStacka";
        private static readonly string HomeScenePath = Root + "/Scenes/Home.unity";
        private static readonly string GameScenePath = Root + "/Scenes/Game.unity";
        private static readonly string PieceSkinDir = Root + "/Data/PieceSkins";
        private static readonly string BuildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
        private static readonly string ExePath = Path.Combine(BuildDir, "MonStackaV2.exe");
        private static readonly string DataDir = Path.Combine(BuildDir, "MonStackaV2_Data");

        public static void RunBatchMode()
        {
            try
            {
                VerifyBoardStateCore();
                VerifyHoldTrackingAndGarbage();
                VerifyAssistSystem();
                VerifyStoryCatalog();
                VerifyControlDefaults();
                VerifyGeneratedPieceSkins();
                VerifyVisualExtrasSafetyGate();
                VerifyRippleStageDefaults();
                VerifySceneWiring();
                VerifyBuildFolderIntegrity();
                Debug.Log("MonStacka v2 verification complete.");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"MonStacka v2 verification failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("MonStacka/Verify Vertical Slice")]
        public static void Run()
        {
            VerifyBoardStateCore();
            VerifyHoldTrackingAndGarbage();
            VerifyAssistSystem();
            VerifyStoryCatalog();
            VerifyControlDefaults();
            VerifyGeneratedPieceSkins();
            VerifyVisualExtrasSafetyGate();
            VerifyRippleStageDefaults();
            VerifySceneWiring();
            VerifyBuildFolderIntegrity();
            Debug.Log("MonStacka v2 verification complete.");
        }

        private static void VerifyBoardStateCore()
        {
            var state = new BoardState(new[] { PieceType.T }, seed: 7);
            Expect(state.HasActivePiece, "BoardState should spawn an initial piece.");
            Expect(state.ActivePiece.Type == PieceType.T, "Single-piece pool should spawn that piece.");

            var spawned = state.ActivePiece;
            Expect(state.TryMove(-1, 0), "Active piece should be able to move left from spawn.");
            Expect(state.ActivePiece.X == spawned.X - 1, "Left move should update X.");
            Expect(state.TryMove(1, 0), "Active piece should move back right.");

            var rotateState = new BoardState(new[] { PieceType.T }, seed: 13);
            var beforeRotation = rotateState.ActivePiece.Rotation;
            Expect(rotateState.TryRotate(1), "T piece should rotate in an empty board.");
            Expect(rotateState.ActivePiece.Rotation != beforeRotation, "Rotation should change orientation.");

            var holdState = new BoardState(new[] { PieceType.I, PieceType.T }, seed: 5);
            Expect(holdState.TryHold(), "First hold should succeed.");
            Expect(holdState.HoldUsed, "Hold should be marked used after the first hold.");
            Expect(!holdState.TryHold(), "Second hold in the same life should fail.");

            var topOutState = new BoardState(new[] { PieceType.O }, seed: 11);
            topOutState.Grid[0, 4] = (int)PieceType.I;
            Expect(!topOutState.SpawnNext(PieceType.O), "Spawning into an occupied spawn cell should top out.");
            Expect(topOutState.IsGameOver(), "Top-out should mark the board game over.");

            var softDropState = new BoardState(new[] { PieceType.O }, seed: 12);
            Expect(softDropState.TrySoftDrop(), "Soft drop should move a falling piece.");
            Expect(softDropState.Score == 1, $"Soft drop should award 1 point outside training, got {softDropState.Score}.");

            var hardDropState = new BoardState(new[] { PieceType.O }, seed: 14);
            Expect(hardDropState.HardDrop(), "Hard drop should lock the active piece.");
            Expect(hardDropState.Score == 44, $"Expected hard drop score of 44 for spawn O, got {hardDropState.Score}.");
            Expect(hardDropState.PiecesPlaced == 1, $"Expected hard drop to place 1 piece, got {hardDropState.PiecesPlaced}.");
            Expect(hardDropState.Grid[22, 4] == (int)PieceType.O, "Hard-dropped O should occupy the expected bottom row cell.");

            var clearState = new BoardState(new[] { PieceType.O }, seed: 1);
            foreach (var x in new[] { -1, 1, 3, 5, 7 })
            {
                HardDropToX(clearState, x);
            }
            Expect(clearState.Lines == 2, $"Expected two cleared lines, got {clearState.Lines}.");
            Expect(clearState.Score == 300, $"Expected 300 score for a double, got {clearState.Score}.");
            Expect(clearState.PiecesPlaced == 5, $"Expected 5 pieces placed, got {clearState.PiecesPlaced}.");

            var sprintState = new BoardState(new[] { PieceType.O }, seed: 2, targetLines: PieceDefinitions.TargetLines);
            var safety = 0;
            while (!sprintState.SprintComplete && safety < 40)
            {
                foreach (var x in new[] { -1, 1, 3, 5, 7 })
                {
                    HardDropToX(sprintState, x);
                }
                safety += 1;
            }
            Expect(sprintState.SprintComplete, "Sprint should complete after clearing the target line count.");
            Expect(sprintState.GameOver, "Sprint completion should end the run.");

            var neighborState = new BoardState(new[] { PieceType.O }, seed: 3);
            HardDropToX(neighborState, 0);
            var map = neighborState.GetNeighborMap(includeActive: false);
            Expect(map.Cells.Count > 0, "Neighbor map should contain locked cells.");

            var trainingRedoState = new BoardState(
                new[] { PieceType.I },
                seed: 4,
                selectedMode: MonStackaMode.Training,
                trainingFeedback: "redo"
            );
            trainingRedoState.RegisterTrainingInput();
            trainingRedoState.RegisterTrainingInput();
            Expect(trainingRedoState.LockPiece(), "Training redo lock should succeed.");
            Expect(trainingRedoState.TrainingFaults == 1, $"Expected one training fault, got {trainingRedoState.TrainingFaults}.");
            Expect(trainingRedoState.PiecesPlaced == 1, $"Expected one training piece attempt, got {trainingRedoState.PiecesPlaced}.");
            Expect(trainingRedoState.HasActivePiece && trainingRedoState.ActivePiece.Type == PieceType.I, "Training redo should restore the same active piece.");
            Expect(trainingRedoState.Grid.Cast<int>().All(cell => cell == 0), "Training redo should leave the board empty.");

            var trainingShowState = new BoardState(
                new[] { PieceType.T },
                seed: 6,
                selectedMode: MonStackaMode.Training,
                trainingFeedback: "show"
            );
            Expect(trainingShowState.LockPiece(), "Training show lock should advance after a clean placement.");
            Expect(trainingShowState.TrainingFaults == 0, $"Expected no training faults, got {trainingShowState.TrainingFaults}.");
            Expect(trainingShowState.TrainingPerfectStreak == 1, $"Expected perfect streak of 1, got {trainingShowState.TrainingPerfectStreak}.");
            Expect(trainingShowState.Grid.Cast<int>().All(cell => cell == 0), "Training show should reset the board after each placement.");
        }

        private static void VerifyHoldTrackingAndGarbage()
        {
            var state = new BoardState(new[] { PieceType.I, PieceType.T }, seed: 21);
            Expect(!state.ActivePieceCameFromHold, "Fresh spawn should not be marked as from hold.");
            Expect(state.TryHold(), "First hold should succeed.");
            Expect(!state.ActivePieceCameFromHold, "First hold spawns from queue, not from hold.");
            Expect(state.HardDrop(), "Hard drop after first hold should lock.");
            Expect(state.SpawnNext(), "Spawn after lock should succeed.");

            var sawFromHoldLock = false;
            state.OnPieceLocked += lockEvent => sawFromHoldLock |= lockEvent.CameFromHold;
            Expect(state.TryHold(), "Swap hold should succeed.");
            Expect(state.ActivePieceCameFromHold, "Swapped-in piece should be marked as from hold.");
            Expect(state.HardDrop(), "Hard drop of held piece should lock.");
            Expect(sawFromHoldLock, "Lock event should carry CameFromHold for held pieces.");

            var garbageState = new BoardState(new[] { PieceType.O }, seed: 22);
            var garbageEvents = 0;
            garbageState.OnGarbageChanged += () => garbageEvents += 1;
            garbageState.AddGarbageRow(3);
            var bottom = PieceDefinitions.TotalRows - 1;
            Expect(garbageState.Grid[bottom, 3] == 0, "Garbage row should leave the hole column empty.");
            Expect(garbageState.Grid[bottom, 0] == BoardState.GarbageCellValue, "Garbage row should fill non-hole columns.");
            Expect(garbageState.GetGarbageCells().Count == PieceDefinitions.Columns - 1, "Garbage cell query should match inserted row.");
            Expect(garbageEvents == 1, "Garbage change event should fire on insert.");

            var removed = garbageState.ClearGarbageCells(4);
            Expect(removed == 4, $"Expected 4 garbage cells cleared, got {removed}.");
            Expect(garbageState.GetGarbageCells().Count == PieceDefinitions.Columns - 5, "Garbage cells should shrink after clearing.");

            garbageState.Grid[bottom - 1, 3] = (int)PieceType.I;
            Expect(garbageState.TryRepairDeepestHole(), "Repair should fill the covered hole.");
            Expect(garbageState.Grid[bottom, 3] == BoardState.GarbageCellValue, "Repaired hole should hold a stitch cell.");
            Expect(!new BoardState(new[] { PieceType.O }, seed: 23).TryRepairDeepestHole(), "Repair should report false on a clean board.");
        }

        private static void VerifyAssistSystem()
        {
            Expect(AssistEffectSystem.AssistForPiece(PieceType.Z) == AssistType.GuardBreak, "Z should map to Guard Break.");
            Expect(AssistEffectSystem.AssistForPiece(PieceType.O) == AssistType.Calculation, "O should map to Calculation.");
            Expect(AssistEffectSystem.AssistForPiece(PieceType.L) == AssistType.EchoGuide, "L should map to Echo Guide.");
            Expect(AssistEffectSystem.AssistForPiece(PieceType.J) == AssistType.Stitch, "J should map to Stitch.");
            Expect(AssistEffectSystem.AssistForPiece(PieceType.S) == AssistType.Digest, "S should map to Digest.");
            Expect(AssistEffectSystem.AssistForPiece(PieceType.T) == AssistType.Sedate, "T should map to Sedate.");
            Expect(AssistEffectSystem.AssistForPiece(PieceType.I) == AssistType.Alert, "I should map to Alert.");

            Expect(AssistEffectSystem.IsEnabledFor(MonStackaMode.Ogbm), "Assists should be enabled in O.G.B.M.");
            Expect(AssistEffectSystem.IsEnabledFor(MonStackaMode.Sprint40), "Assists should be enabled in X(4)-LINES.");
            Expect(!AssistEffectSystem.IsEnabledFor(MonStackaMode.Training), "Assists should be disabled in Training by default.");
            Expect(AssistEffectSystem.IsEnabledFor(MonStackaMode.Training, trainingAssistToggle: true), "Assist practice toggle should enable Training assists.");

            var board = new BoardState(new[] { PieceType.T }, seed: 31);
            var assist = new AssistEffectSystem();
            var awarded = 0;
            void Award(int points) => awarded += points;
            PieceLockEvent HeldLock(PieceType type) =>
                new(1, type, 0, new System.Collections.Generic.List<UnityEngine.Vector2Int>(), UnityEngine.Vector2Int.zero, cameFromHold: true);

            Expect(assist.OnPieceLocked(HeldLock(PieceType.T), board, Award) == null, "First held placement should not trigger.");
            Expect(assist.HeldProgress == 1, $"Held progress should be 1, got {assist.HeldProgress}.");
            Expect(assist.OnPieceLocked(HeldLock(PieceType.T), board, Award) == null, "Second held placement should not trigger.");
            var trigger = assist.OnPieceLocked(HeldLock(PieceType.T), board, Award);
            Expect(trigger.HasValue, "Third held placement should trigger the assist.");
            Expect(trigger.Value.Type == AssistType.Sedate, "T trigger should fire Sedate.");
            Expect(awarded > 0, "Assist trigger should award score.");
            Expect(assist.GravityMultiplier > 1f, "Sedate should slow gravity while active.");
            Expect(assist.LockDelayBonusSeconds > 0f, "Sedate should extend lock delay while active.");
            assist.Tick(30f);
            Expect(Mathf.Approximately(assist.GravityMultiplier, 1f), "Sedate should expire after its window.");

            var nonHeld = assist.OnPieceLocked(
                new PieceLockEvent(2, PieceType.T, 0, new System.Collections.Generic.List<UnityEngine.Vector2Int>(), UnityEngine.Vector2Int.zero, cameFromHold: false),
                board,
                Award
            );
            Expect(nonHeld == null, "Non-held placements should never trigger assists.");

            var stitchBoard = new BoardState(new[] { PieceType.J }, seed: 32);
            var stitchBottom = PieceDefinitions.TotalRows - 1;
            stitchBoard.Grid[stitchBottom - 1, 0] = (int)PieceType.J;
            var stitchAssist = new AssistEffectSystem();
            stitchAssist.OnPieceLocked(HeldLock(PieceType.J), stitchBoard, Award);
            stitchAssist.OnPieceLocked(HeldLock(PieceType.J), stitchBoard, Award);
            var stitchTrigger = stitchAssist.OnPieceLocked(HeldLock(PieceType.J), stitchBoard, Award);
            Expect(stitchTrigger.HasValue && stitchTrigger.Value.Type == AssistType.Stitch, "J trigger should fire Stitch.");
            Expect(stitchBoard.Grid[stitchBottom, 0] == BoardState.GarbageCellValue, "Stitch should repair the covered hole.");
        }

        private static void VerifyStoryCatalog()
        {
            var chapters = MonStacka.Story.StoryCatalog.Chapters;
            Expect(chapters.Count == 20, $"Expected 20 story chapters, got {chapters.Count}.");
            Expect(chapters.Select(chapter => chapter.Id).Distinct().Count() == chapters.Count, "Chapter ids should be unique.");

            for (var index = 0; index < chapters.Count; index += 1)
            {
                var chapter = chapters[index];
                Expect(!string.IsNullOrWhiteSpace(chapter.Title), $"Chapter {chapter.Id} should have a title.");
                Expect(chapter.IntroDialogue != null && chapter.PreMatchDialogue != null && chapter.PostMatchDialogue != null, $"Chapter {chapter.Id} dialogue arrays should be non-null.");
                Expect(chapter.GravitySeconds > 0f && chapter.LockDelaySeconds > 0f, $"Chapter {chapter.Id} should have valid timing.");

                var expectedNext = index + 1 < chapters.Count ? chapters[index + 1].Id : null;
                Expect(chapter.UnlocksNext == expectedNext, $"Chapter {chapter.Id} unlock chain should point to {expectedNext ?? "null"}.");

                if (index > 0)
                {
                    Expect(chapter.DifficultyTier >= chapters[index - 1].DifficultyTier, $"Difficulty should not decrease at {chapter.Id}.");
                    Expect(chapter.GravitySeconds <= chapters[index - 1].GravitySeconds, $"Gravity should not slow down at {chapter.Id}.");
                }

                if (chapter.Act == 5)
                {
                    Expect(chapter.SpawnBias.Count == 0, $"Chapter 5 mission {chapter.Id} must have no spawn bias.");
                    Expect(chapter.Modifiers.Length >= 3, $"Chapter 5 mission {chapter.Id} should combine multiple mechanics.");
                }
                else if (chapter.Id != "1.1")
                {
                    Expect(chapter.SpawnBias.Count > 0, $"Chapter {chapter.Id} should bias its focused piece spawns.");
                    foreach (var piece in chapter.FocusedPieces)
                    {
                        Expect(chapter.SpawnBias.TryGetValue(piece, out var weight) && weight > 1f, $"Chapter {chapter.Id} should bias {piece} above normal weight.");
                    }
                }
            }

            Expect(chapters[0].IntroDialogue.Length > 0, "Chapter 1.1 should carry the game intro dialogue.");
            Expect(MonStacka.Story.StoryCatalog.GetChapter("5.3").HoldEnabled == false, "Final mission should disable hold.");

            // Weighted bag: biased chapters spawn the focused piece more often,
            // while every piece still appears in every bag.
            var bias = MonStacka.Story.StoryCatalog.GetChapter("1.2").SpawnBias;
            var allPieces = Enum.GetValues(typeof(PieceType)).Cast<PieceType>().ToList();
            var weightedBag = new PieceBag(allPieces, seed: 41, weights: bias);
            var focusedCount = 0;
            var totalCount = 0;
            for (var bagIndex = 0; bagIndex < 200; bagIndex += 1)
            {
                var bagPieces = weightedBag.MakeBag();
                Expect(bagPieces.Distinct().Count() == allPieces.Count, "Biased bags should still contain every piece type.");
                focusedCount += bagPieces.Count(piece => piece == PieceType.Z);
                totalCount += bagPieces.Count;
            }
            var focusedShare = (float)focusedCount / totalCount;
            Expect(focusedShare > 1.6f / 7f, $"Z spawn share should exceed uniform under bias, got {focusedShare:0.###}.");
        }

        private static void VerifyGeneratedPieceSkins()
        {
            var expectedPieces = new[]
            {
                PieceType.I,
                PieceType.O,
                PieceType.T,
                PieceType.S,
                PieceType.Z,
                PieceType.J,
                PieceType.L,
            };

            var skinAssets = expectedPieces
                .Select(piece => AssetDatabase.LoadAssetAtPath<PieceSkinData>($"{PieceSkinDir}/Skin_{piece}.asset"))
                .ToArray();

            Expect(skinAssets.All(asset => asset != null), "All 7 piece skin assets should exist.");
            foreach (var skin in skinAssets)
            {
                Expect(skin.bodyFrames != null && skin.bodyFrames.Length == 3, $"{skin.pieceType} should have 3 body frames.");
                Expect(skin.bodyFrames.All(frame => frame != null), $"{skin.pieceType} should have non-null body frame sprites.");
                Expect(skin.featuresSheet != null, $"{skin.pieceType} should reference the features sheet.");
                Expect(skin.features != null && skin.features.Length > 0, $"{skin.pieceType} should have feature seeds from the PSD manifest.");
                VerifyGeneratedFrameOccupancy(skin);
                VerifyRotatedFrameOccupancy(skin);
            }

            var outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Art/Materials/OutlineMeshMat.mat");
            var tuning = AssetDatabase.LoadAssetAtPath<BorderDeformTuningProfile>($"{Root}/Data/Tuning/DefaultBorderDeformTuning.asset");
            Expect(outlineMaterial != null, "Outline material should exist.");
            Expect(tuning != null, "Default border tuning asset should exist.");

            foreach (var skin in skinAssets)
            {
                var go = new GameObject($"Verify_{skin.pieceType}");
                try
                {
                    var pieceSkin = go.AddComponent<PieceSkin>();
                    pieceSkin.PieceId = 99;
                    pieceSkin.Initialize(
                        skin,
                        skin.pieceType,
                        0,
                        PieceDefinitions.GetCells(skin.pieceType, 0),
                        1f,
                        outlineMaterial,
                        tuning,
                        false,
                        0.05f,
                        true,
                        skin.pieceType is PieceType.I or PieceType.T
                    );
                    pieceSkin.ManualUpdate(0.5f);
                    Expect(!pieceSkin.BodyBuildUsesFullBoxSprite, $"{skin.pieceType} should use the shared connected-body render path.");

                    var facialParts = go.transform.Find("FacialParts");
                    Expect(facialParts != null && facialParts.childCount > 0, $"{skin.pieceType} should create independent feature overlays.");
                    Expect(facialParts != null && facialParts.childCount == skin.features.Length, $"{skin.pieceType} should create one overlay per feature seed.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            var generatedDir = Path.Combine(Directory.GetCurrentDirectory(), Root, "Art/Generated/BodyFrames");
            Expect(Directory.Exists(generatedDir), "Generated body frame directory should exist.");
            var pngCount = Directory.GetFiles(generatedDir, "*_frame*.png").Length;
            Expect(pngCount == 21, $"Expected 21 generated body frame PNGs, found {pngCount}.");
        }

        private static void VerifyControlDefaults()
        {
            var summary = MonStackaControls.BuildControlsSummaryText();
            Expect(summary.Contains("Move Left: Left Arrow"), "Keyboard left default should be visible in controls summary.");
            Expect(summary.Contains("Move Right: Right Arrow"), "Keyboard right default should be visible in controls summary.");
            Expect(summary.Contains("Soft Drop: Down Arrow"), "Keyboard soft drop default should be visible in controls summary.");
            Expect(summary.Contains("Hard Drop: Space"), "Keyboard hard drop default should be visible in controls summary.");
            Expect(summary.Contains("Rotate CCW: Z"), "Keyboard CCW default should be visible in controls summary.");
            Expect(summary.Contains("Rotate CW: X"), "Keyboard CW default should be visible in controls summary.");
            Expect(summary.Contains("Rotate 180: A"), "Keyboard 180 default should be visible in controls summary.");
            Expect(summary.Contains("Hold: C"), "Keyboard hold default should be visible in controls summary.");
            Expect(summary.Contains("Pause / Resume: P / Esc"), "Keyboard pause default should be visible in controls summary.");
            Expect(summary.Contains("Retry: R"), "Keyboard retry default should be visible in controls summary.");
            Expect(summary.Contains("Restart Paused: O"), "Keyboard paused restart default should be visible in controls summary.");
            Expect(summary.Contains("Move Left: D-pad Left / Left Stick Left"), "Xbox left default should be visible in controls summary.");
            Expect(summary.Contains("Move Right: D-pad Right / Left Stick Right"), "Xbox right default should be visible in controls summary.");
            Expect(summary.Contains("Soft Drop: D-pad Down / Left Stick Down"), "Xbox soft drop default should be visible in controls summary.");
            Expect(summary.Contains("Hard Drop: D-pad Up / Left Stick Up"), "Xbox hard drop default should be visible in controls summary.");
            Expect(summary.Contains("Rotate CCW: A"), "Xbox CCW default should be visible in controls summary.");
            Expect(summary.Contains("Rotate CW: B"), "Xbox CW default should be visible in controls summary.");
            Expect(summary.Contains("Rotate 180: Y"), "Xbox 180 default should be visible in controls summary.");
            Expect(summary.Contains("Hold: LB"), "Xbox hold default should be visible in controls summary.");
            Expect(summary.Contains("Pause / Resume: Back"), "Xbox pause default should be visible in controls summary.");
            Expect(summary.Contains("Retry: Start"), "Xbox retry default should be visible in controls summary.");
            Expect(summary.Contains("Restart Paused: L3"), "Xbox paused restart default should be visible in controls summary.");
            Expect(summary.Contains("DAS 110ms"), "Controls summary should report v1 default DAS timing.");
        }

        private static void VerifyVisualExtrasSafetyGate()
        {
            var previousValue = MonStackaAppState.VisualExtrasEnabled;
            MonStackaAppState.VisualExtrasEnabled = false;
            try
            {
                var skin = AssetDatabase.LoadAssetAtPath<PieceSkinData>($"{PieceSkinDir}/Skin_I.asset");
                var outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Art/Materials/OutlineMeshMat.mat");
                var tuning = AssetDatabase.LoadAssetAtPath<BorderDeformTuningProfile>($"{Root}/Data/Tuning/DefaultBorderDeformTuning.asset");
                Expect(skin != null, "Safety gate test needs I piece skin.");

                var go = new GameObject("Verify_VisualExtrasOff");
                try
                {
                    var pieceSkin = go.AddComponent<PieceSkin>();
                    pieceSkin.Initialize(
                        skin,
                        PieceType.I,
                        0,
                        PieceDefinitions.GetCells(PieceType.I, 0),
                        1f,
                        outlineMaterial,
                        tuning,
                        false,
                        0.25f,
                        true,
                        true
                    );

                    Expect(!pieceSkin.UsesBorderPulse, "VisualExtras off should disable border pulse.");
                    // Bodies are featureless since the PSD layer split: the face overlay
                    // must stay visible with extras off, but it must not animate.
                    var facialParts = go.transform.Find("FacialParts");
                    Expect(facialParts != null && facialParts.childCount > 0, "VisualExtras off should keep static facial overlays visible.");
                    var animator = facialParts.GetComponent<FacialPartAnimator>();
                    Expect(animator != null && !animator.Animates, "VisualExtras off should stop facial animation.");
                    Expect(go.transform.Find("Body") != null, "VisualExtras off should keep the body sprite visible.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
            finally
            {
                MonStackaAppState.VisualExtrasEnabled = previousValue;
            }
        }

        private static void VerifyRippleStageDefaults()
        {
            var previousMode = MonStackaAppState.SelectedMode;
            var previousGravity = MonStackaAppState.GravitySeconds;
            var previousLockDelay = MonStackaAppState.LockDelaySeconds;
            var previousDas = MonStackaAppState.DasSeconds;
            var previousArr = MonStackaAppState.ArrSeconds;
            var previousMusicEnabled = MonStackaAppState.MusicEnabled;
            var previousSfxEnabled = MonStackaAppState.SfxEnabled;
            var previousMusicVolume = MonStackaAppState.MusicVolume;
            var previousSfxVolume = MonStackaAppState.SfxVolume;
            var previousTrainingFeedback = MonStackaAppState.TrainingFeedbackMode;
            var previousVisualExtras = MonStackaAppState.VisualExtrasEnabled;
            var previousStage = MonStackaAppState.RippleStage;
            try
            {
                MonStackaAppState.ResetDefaults();
                Expect(MonStackaAppState.RippleStage == MonStackaRippleStage.HomePreview, "Default ripple stage should be home-preview only.");
                MonStackaAppState.RippleStage = MonStackaRippleStage.Off;
                Expect(MonStackaAppState.RippleStage < MonStackaRippleStage.HomePreview, "Off ripple stage should disable home preview pulse.");
                MonStackaAppState.RippleStage = MonStackaRippleStage.ActiveGameplay;
                Expect(MonStackaAppState.RippleStage >= MonStackaRippleStage.ActiveGameplay, "Active ripple stage should enable active gameplay pulse.");
                Expect(MonStackaAppState.RippleStage < MonStackaRippleStage.LandedGameplay, "Active ripple stage should not enable landed stack pulse.");
                MonStackaAppState.RippleStage = MonStackaRippleStage.LandedGameplay;
                Expect(MonStackaAppState.RippleStage >= MonStackaRippleStage.LandedGameplay, "Landed ripple stage should enable landed stack pulse.");
            }
            finally
            {
                MonStackaAppState.SelectedMode = previousMode;
                MonStackaAppState.GravitySeconds = previousGravity;
                MonStackaAppState.LockDelaySeconds = previousLockDelay;
                MonStackaAppState.DasSeconds = previousDas;
                MonStackaAppState.ArrSeconds = previousArr;
                MonStackaAppState.MusicEnabled = previousMusicEnabled;
                MonStackaAppState.SfxEnabled = previousSfxEnabled;
                MonStackaAppState.MusicVolume = previousMusicVolume;
                MonStackaAppState.SfxVolume = previousSfxVolume;
                MonStackaAppState.TrainingFeedbackMode = previousTrainingFeedback;
                MonStackaAppState.VisualExtrasEnabled = previousVisualExtras;
                MonStackaAppState.RippleStage = previousStage;
            }
        }

        private static void VerifySceneWiring()
        {
            var homeScene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
            Expect(homeScene.IsValid(), "Home scene should open.");
            var homeMenu = UnityEngine.Object.FindFirstObjectByType<HomeMenuController>();
            Expect(homeMenu != null, "Home scene should contain a HomeMenuController.");

            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Expect(scene.IsValid(), "Game scene should open.");
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Expect(manager != null, "Game scene should contain a GameManager.");
            var artboard = UnityEngine.Object.FindFirstObjectByType<ArtboardViewportController>();
            Expect(artboard != null, "Game scene should contain an ArtboardViewportController.");
            var hud = UnityEngine.Object.FindFirstObjectByType<HUDController>();
            Expect(hud != null, "Game scene should contain a HUDController.");
            var hold = UnityEngine.Object.FindFirstObjectByType<HoldBoxView>();
            Expect(hold != null, "Game scene should contain a HoldBoxView.");
            var next = UnityEngine.Object.FindFirstObjectByType<NextQueueView>();
            Expect(next != null, "Game scene should contain a NextQueueView.");

            var managerType = typeof(GameManager);
            var useThreePiece = (bool)GetPrivateField(managerType, manager, "useThreePieceVerticalSlice");
            Expect(!useThreePiece, "Game scene should be configured for all 7 pieces.");

            var deformTuning = GetPrivateField(managerType, manager, "deformTuning") as BorderDeformTuningProfile;
            Expect(deformTuning != null, "Game scene should wire the border deform tuning asset.");

            var pieceSkins = GetPrivateField(managerType, manager, "pieceSkins") as PieceSkinData[];
            Expect(pieceSkins != null && pieceSkins.Length == 7, "Game scene should wire all 7 piece skins.");
            Expect(pieceSkins.All(skin => skin != null), "Game scene should not contain null piece skin references.");
        }

        private static void VerifyBuildFolderIntegrity()
        {
            if (!File.Exists(ExePath))
            {
                return;
            }

            Expect(Directory.Exists(BuildDir), "Windows build output directory should exist.");
            Expect(Directory.Exists(DataDir), "Windows player data folder should exist next to the executable.");
            Expect(File.Exists(Path.Combine(BuildDir, "Launch-MonStackaV2.cmd")), "Windows launch helper should exist.");
        }

        private static void VerifyGeneratedFrameOccupancy(PieceSkinData skin)
        {
            var allowedCells = new HashSet<Vector2Int>(PieceDefinitions.GetCells(skin.pieceType, skin.baseRotation));
            var boxSize = skin.boxSize;
            var pixelsPerCell = skin.pixelsPerCell;

            foreach (var frame in skin.bodyFrames)
            {
                var texture = frame.texture;
                var rect = frame.rect;
                var pixels = texture.GetPixels(
                    Mathf.RoundToInt(rect.x),
                    Mathf.RoundToInt(rect.y),
                    Mathf.RoundToInt(rect.width),
                    Mathf.RoundToInt(rect.height)
                );

                for (var localY = 0; localY < frame.texture.height; localY += 1)
                {
                    for (var localX = 0; localX < frame.texture.width; localX += 1)
                    {
                        var color = pixels[(localY * frame.texture.width) + localX];
                        if (color.a <= 0.05f)
                        {
                            continue;
                        }

                        var cellX = localX / pixelsPerCell;
                        var cellY = boxSize - 1 - (localY / pixelsPerCell);
                        Expect(
                            allowedCells.Contains(new Vector2Int(cellX, cellY)),
                            $"{skin.pieceType} frame '{frame.name}' contains visible pixels outside occupied cells at ({cellX},{cellY})."
                        );
                    }
                }
            }
        }

        private static void VerifyRotatedFrameOccupancy(PieceSkinData skin)
        {
            // Per-cell occupancy: every cell of the rotated definition must hold body
            // pixels and every empty cell must stay (nearly) empty. This catches
            // rotation-direction mistakes that leave the art on the wrong cells.
            var ppc = skin.pixelsPerCell;
            for (var rotation = 0; rotation < 4; rotation += 1)
            {
                var occupied = new HashSet<Vector2Int>(PieceDefinitions.GetCells(skin.pieceType, rotation));
                var rotatedFrames = ConnectedBodyBuilder.GetRotatedFrameTextures(skin, skin.pieceType, rotation);
                foreach (var frame in rotatedFrames)
                {
                    var pixels = frame.GetPixels();
                    var boxCells = frame.width / ppc;
                    for (var cellY = 0; cellY < boxCells; cellY += 1)
                    {
                        for (var cellX = 0; cellX < boxCells; cellX += 1)
                        {
                            var opaque = 0;
                            for (var localY = 0; localY < ppc; localY += 1)
                            {
                                var pixelY = frame.height - ((cellY + 1) * ppc) + localY;
                                for (var localX = 0; localX < ppc; localX += 1)
                                {
                                    if (pixels[(pixelY * frame.width) + (cellX * ppc) + localX].a > 0.05f)
                                    {
                                        opaque += 1;
                                    }
                                }
                            }

                            var fraction = opaque / (float)(ppc * ppc);
                            if (occupied.Contains(new Vector2Int(cellX, cellY)))
                            {
                                Expect(fraction > 0.3f, $"{skin.pieceType} rotation {rotation} cell ({cellX},{cellY}) should hold body pixels (got {fraction:P0}).");
                            }
                            else
                            {
                                Expect(fraction < 0.08f, $"{skin.pieceType} rotation {rotation} cell ({cellX},{cellY}) should be empty (got {fraction:P0}).");
                            }
                        }
                    }
                }
            }
        }

        private static void HardDropToX(BoardState state, int targetX)
        {
            Expect(state.HasActivePiece, "Expected an active piece before hard drop.");
            var currentX = state.ActivePiece.X;
            while (currentX > targetX)
            {
                Expect(state.TryMove(-1, 0), $"Could not move piece left to target x={targetX}.");
                currentX = state.ActivePiece.X;
            }
            while (currentX < targetX)
            {
                Expect(state.TryMove(1, 0), $"Could not move piece right to target x={targetX}.");
                currentX = state.ActivePiece.X;
            }

            while (state.TryMove(0, 1))
            {
            }

            Expect(state.LockPiece(), "Expected hard-dropped piece to lock.");
            if (!state.SprintComplete)
            {
                Expect(state.SpawnNext(), "Expected next piece to spawn after lock.");
            }
        }

        private static object GetPrivateField(Type type, object instance, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Expect(field != null, $"Missing private field '{fieldName}' on {type.Name}.");
            return field.GetValue(instance);
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
