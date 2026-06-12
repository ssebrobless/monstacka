using System.Collections.Generic;
using System.Linq;
using MonStacka.Core;

namespace MonStacka.Story
{
    /// <summary>
    /// Explicit chapter metadata for the whole campaign, per the handoff spec.
    /// Chapter/piece mapping comes from the handoff (authoritative):
    /// Z=Aggraso, O=Muwerde, L=Galiffambos, J=Dousema, S=Sorrisol, T=Lysergicada, I=Blyndoolie.
    /// Chapter 5 has no spawn bias and combines earlier mechanics at high difficulty.
    /// </summary>
    public static class StoryCatalog
    {
        /// <summary>Spawn weight for the focused piece in biased chapters (others stay 1.0).</summary>
        public const float FocusBias = 2.5f;

        public static readonly IReadOnlyList<StoryChapterSpec> Chapters = BuildChapters();

        public static StoryChapterSpec GetChapter(string id) =>
            Chapters.FirstOrDefault(chapter => chapter.Id == id);

        public static StoryChapterSpec FirstChapter => Chapters[0];

        private static Dictionary<PieceType, float> Bias(params PieceType[] focused)
        {
            var bias = new Dictionary<PieceType, float>();
            foreach (var piece in focused)
            {
                bias[piece] = FocusBias;
            }
            return bias;
        }

        private static List<StoryChapterSpec> BuildChapters()
        {
            var chapters = new List<StoryChapterSpec>
            {
                // ----- Act 1: Aggraso / Z -----
                new()
                {
                    Id = "1.1", Title = "A Yucky Building", Act = 1, Sequence = 1,
                    DifficultyTier = 1, GravitySeconds = 0.85f, LockDelaySeconds = 0.5f,
                    Objective = StoryObjective.Lines(5),
                    IntroDialogue = StoryDialogueAct1.GameIntro,
                    PreMatchDialogue = StoryDialogueAct1.PreMatch_1_1,
                    PostMatchDialogue = StoryDialogueAct1.PostMatch_1_1,
                },
                new()
                {
                    Id = "1.2", Title = "Guard Dog", Act = 1, Sequence = 2,
                    FocusedPieces = new[] { PieceType.Z }, SpawnBias = Bias(PieceType.Z),
                    DifficultyTier = 1, GravitySeconds = 0.8f, LockDelaySeconds = 0.45f,
                    Objective = StoryObjective.Lines(8),
                    Modifiers = new[] { StoryModifier.GuardPressure },
                    IntroDialogue = StoryDialogueAct1.Intro_1_2,
                    PreMatchDialogue = StoryDialogueAct1.PreMatch_1_2,
                    PostMatchDialogue = StoryDialogueAct1.PostMatch_1_2,
                },
                new()
                {
                    Id = "1.3", Title = "Lock the Door Behind You", Act = 1, Sequence = 3,
                    FocusedPieces = new[] { PieceType.Z }, SpawnBias = Bias(PieceType.Z),
                    DifficultyTier = 2, GravitySeconds = 0.75f, LockDelaySeconds = 0.42f,
                    Objective = StoryObjective.Lines(10),
                    Modifiers = new[] { StoryModifier.GuardPressure, StoryModifier.TerritoryCells },
                    IntroDialogue = StoryDialogueAct1.Intro_1_3,
                    PreMatchDialogue = StoryDialogueAct1.PreMatch_1_3,
                    PostMatchDialogue = StoryDialogueAct1.PostMatch_1_3,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "1.4", Title = "A Shared Dream", Act = 1, Sequence = 4,
                    FocusedPieces = new[] { PieceType.Z }, SpawnBias = Bias(PieceType.Z),
                    DifficultyTier = 2, GravitySeconds = 0.7f, LockDelaySeconds = 0.4f,
                    Objective = StoryObjective.Lines(12),
                    Modifiers = new[] { StoryModifier.GuardPressure, StoryModifier.TerritoryCells },
                    IntroDialogue = StoryDialogueAct1.Intro_1_4,
                    PreMatchDialogue = StoryDialogueAct1.PreMatch_1_4,
                    PostMatchDialogue = StoryDialogueAct1.PostMatch_1_4,
                    DialogueIsGeneratedDraft = true,
                },

                // ----- Act 2: Muwerde / O -----
                new()
                {
                    Id = "2.1", Title = "Unlocking Intelligence", Act = 2, Sequence = 1,
                    FocusedPieces = new[] { PieceType.O }, SpawnBias = Bias(PieceType.O),
                    DifficultyTier = 3, GravitySeconds = 0.62f, LockDelaySeconds = 0.38f,
                    NextPreviewCount = 5,
                    Objective = StoryObjective.Lines(12),
                    Modifiers = new[] { StoryModifier.CalculatedPlanning },
                    IntroDialogue = StoryDialogueAct2.Intro_2_1,
                    PreMatchDialogue = StoryDialogueAct2.PreMatch_2_1,
                    PostMatchDialogue = StoryDialogueAct2.PostMatch_2_1,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "2.2", Title = "Trial and Error", Act = 2, Sequence = 2,
                    FocusedPieces = new[] { PieceType.O }, SpawnBias = Bias(PieceType.O),
                    DifficultyTier = 3, GravitySeconds = 0.58f, LockDelaySeconds = 0.36f,
                    NextPreviewCount = 5,
                    Objective = StoryObjective.Lines(14),
                    Modifiers = new[] { StoryModifier.CalculatedPlanning, StoryModifier.PrecisionPressure },
                    IntroDialogue = StoryDialogueAct2.Intro_2_2,
                    PreMatchDialogue = StoryDialogueAct2.PreMatch_2_2,
                    PostMatchDialogue = StoryDialogueAct2.PostMatch_2_2,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "2.3", Title = "Thinking Outside the Box", Act = 2, Sequence = 3,
                    FocusedPieces = new[] { PieceType.O }, SpawnBias = Bias(PieceType.O),
                    DifficultyTier = 4, GravitySeconds = 0.54f, LockDelaySeconds = 0.34f,
                    NextPreviewCount = 5,
                    Objective = StoryObjective.Score(6000),
                    Modifiers = new[] { StoryModifier.CalculatedPlanning, StoryModifier.PrecisionPressure },
                    IntroDialogue = StoryDialogueAct2.Intro_2_3,
                    PreMatchDialogue = StoryDialogueAct2.PreMatch_2_3,
                    PostMatchDialogue = StoryDialogueAct2.PostMatch_2_3,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "2.4", Title = "Weathering the Storm", Act = 2, Sequence = 4,
                    FocusedPieces = new[] { PieceType.O }, SpawnBias = Bias(PieceType.O),
                    DifficultyTier = 4, GravitySeconds = 0.5f, LockDelaySeconds = 0.32f,
                    Objective = StoryObjective.Lines(16),
                    Modifiers = new[] { StoryModifier.PrecisionPressure, StoryModifier.TerritoryCells },
                    IntroDialogue = StoryDialogueAct2.Intro_2_4,
                    PreMatchDialogue = StoryDialogueAct2.PreMatch_2_4,
                    PostMatchDialogue = StoryDialogueAct2.PostMatch_2_4,
                    DialogueIsGeneratedDraft = true,
                },

                // ----- Act 3: Galiffambos / L (3.1-3.2), Dousema / J (3.3-3.4) -----
                new()
                {
                    Id = "3.1", Title = "Development of Senses", Act = 3, Sequence = 1,
                    FocusedPieces = new[] { PieceType.L }, SpawnBias = Bias(PieceType.L),
                    DifficultyTier = 5, GravitySeconds = 0.48f, LockDelaySeconds = 0.32f,
                    Objective = StoryObjective.Lines(16),
                    Modifiers = new[] { StoryModifier.GhostFlicker },
                    IntroDialogue = StoryDialogueAct3.Intro_3_1,
                    PreMatchDialogue = StoryDialogueAct3.PreMatch_3_1,
                    PostMatchDialogue = StoryDialogueAct3.PostMatch_3_1,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "3.2", Title = "The Cost of Success", Act = 3, Sequence = 2,
                    FocusedPieces = new[] { PieceType.L }, SpawnBias = Bias(PieceType.L),
                    DifficultyTier = 5, GravitySeconds = 0.46f, LockDelaySeconds = 0.3f,
                    Objective = StoryObjective.Lines(18),
                    Modifiers = new[] { StoryModifier.GhostFlicker, StoryModifier.EcholocationDim },
                    IntroDialogue = StoryDialogueAct3.Intro_3_2,
                    PreMatchDialogue = StoryDialogueAct3.PreMatch_3_2,
                    PostMatchDialogue = StoryDialogueAct3.PostMatch_3_2,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "3.3", Title = "Preparing for the Worst", Act = 3, Sequence = 3,
                    FocusedPieces = new[] { PieceType.J }, SpawnBias = Bias(PieceType.J),
                    DifficultyTier = 6, GravitySeconds = 0.44f, LockDelaySeconds = 0.3f,
                    Objective = StoryObjective.Lines(18),
                    Modifiers = new[] { StoryModifier.ResilientCells },
                    IntroDialogue = StoryDialogueAct3.Intro_3_3,
                    PreMatchDialogue = StoryDialogueAct3.PreMatch_3_3,
                    PostMatchDialogue = StoryDialogueAct3.PostMatch_3_3,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "3.4", Title = "Nose to the Grindstone", Act = 3, Sequence = 4,
                    FocusedPieces = new[] { PieceType.J }, SpawnBias = Bias(PieceType.J),
                    DifficultyTier = 6, GravitySeconds = 0.42f, LockDelaySeconds = 0.28f,
                    Objective = StoryObjective.Lines(20),
                    Modifiers = new[] { StoryModifier.ResilientCells, StoryModifier.MutedHints },
                    IntroDialogue = StoryDialogueAct3.Intro_3_4,
                    PreMatchDialogue = StoryDialogueAct3.PreMatch_3_4,
                    PostMatchDialogue = StoryDialogueAct3.PostMatch_3_4,
                    DialogueIsGeneratedDraft = true,
                },

                // ----- Act 4: Sorrisol / S + Lysergicada / T (4.1-4.3), Blyndoolie / I (4.4-4.5) -----
                new()
                {
                    Id = "4.1", Title = "Teamwork", Act = 4, Sequence = 1,
                    FocusedPieces = new[] { PieceType.S, PieceType.T },
                    SpawnBias = Bias(PieceType.S, PieceType.T),
                    DifficultyTier = 7, GravitySeconds = 0.4f, LockDelaySeconds = 0.28f,
                    Objective = StoryObjective.Lines(20),
                    Modifiers = new[] { StoryModifier.HungerMeter, StoryModifier.SedationWindows },
                    IntroDialogue = StoryDialogueAct4.Intro_4_1,
                    PreMatchDialogue = StoryDialogueAct4.PreMatch_4_1,
                    PostMatchDialogue = StoryDialogueAct4.PostMatch_4_1,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "4.2", Title = "Waste Management", Act = 4, Sequence = 2,
                    FocusedPieces = new[] { PieceType.S }, SpawnBias = Bias(PieceType.S),
                    DifficultyTier = 7, GravitySeconds = 0.38f, LockDelaySeconds = 0.26f,
                    Objective = StoryObjective.Lines(22),
                    Modifiers = new[] { StoryModifier.HungerMeter },
                    IntroDialogue = StoryDialogueAct4.Intro_4_2,
                    PreMatchDialogue = StoryDialogueAct4.PreMatch_4_2,
                    PostMatchDialogue = StoryDialogueAct4.PostMatch_4_2,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "4.3", Title = "Identifying a Problem", Act = 4, Sequence = 3,
                    FocusedPieces = new[] { PieceType.T }, SpawnBias = Bias(PieceType.T),
                    DifficultyTier = 8, GravitySeconds = 0.36f, LockDelaySeconds = 0.26f,
                    Objective = StoryObjective.Lines(22),
                    Modifiers = new[] { StoryModifier.SedationWindows },
                    IntroDialogue = StoryDialogueAct4.Intro_4_3,
                    PreMatchDialogue = StoryDialogueAct4.PreMatch_4_3,
                    PostMatchDialogue = StoryDialogueAct4.PostMatch_4_3,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "4.4", Title = "Vigilance is Key", Act = 4, Sequence = 4,
                    FocusedPieces = new[] { PieceType.I }, SpawnBias = Bias(PieceType.I),
                    DifficultyTier = 8, GravitySeconds = 0.34f, LockDelaySeconds = 0.24f,
                    Objective = StoryObjective.Lines(24),
                    Modifiers = new[] { StoryModifier.AdrenalineMonitor },
                    IntroDialogue = StoryDialogueAct4.Intro_4_4,
                    PreMatchDialogue = StoryDialogueAct4.PreMatch_4_4,
                    PostMatchDialogue = StoryDialogueAct4.PostMatch_4_4,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "4.5", Title = "Knowing When to Let Go", Act = 4, Sequence = 5,
                    FocusedPieces = new[] { PieceType.I }, SpawnBias = Bias(PieceType.I),
                    DifficultyTier = 9, GravitySeconds = 0.32f, LockDelaySeconds = 0.24f,
                    Objective = StoryObjective.Lines(26),
                    Modifiers = new[] { StoryModifier.AdrenalineMonitor, StoryModifier.SignalRelay },
                    IntroDialogue = StoryDialogueAct4.Intro_4_5,
                    PreMatchDialogue = StoryDialogueAct4.PreMatch_4_5,
                    PostMatchDialogue = StoryDialogueAct4.PostMatch_4_5,
                    DialogueIsGeneratedDraft = true,
                },

                // ----- Act 5: finale. NO spawn bias. Hard remix missions. -----
                new()
                {
                    Id = "5.1", Title = "Tethered Minds", Act = 5, Sequence = 1,
                    DifficultyTier = 9, GravitySeconds = 0.3f, LockDelaySeconds = 0.22f,
                    NextPreviewCount = 2,
                    Objective = StoryObjective.Lines(28),
                    Modifiers = new[]
                    {
                        StoryModifier.ReducedPreview,
                        StoryModifier.GuardPressure,
                        StoryModifier.GhostFlicker,
                    },
                    IntroDialogue = StoryDialogueAct5.Intro_5_1,
                    PreMatchDialogue = StoryDialogueAct5.PreMatch_5_1,
                    PostMatchDialogue = StoryDialogueAct5.PostMatch_5_1,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "5.2", Title = "Destruction", Act = 5, Sequence = 2,
                    DifficultyTier = 10, GravitySeconds = 0.26f, LockDelaySeconds = 0.2f,
                    NextPreviewCount = 2,
                    Objective = StoryObjective.TimedLines(30, 300f),
                    Modifiers = new[]
                    {
                        StoryModifier.ReducedPreview,
                        StoryModifier.HungerMeter,
                        StoryModifier.ResilientCells,
                        StoryModifier.SedationWindows,
                    },
                    IntroDialogue = StoryDialogueAct5.Intro_5_2,
                    PreMatchDialogue = StoryDialogueAct5.PreMatch_5_2,
                    PostMatchDialogue = StoryDialogueAct5.PostMatch_5_2,
                    DialogueIsGeneratedDraft = true,
                },
                new()
                {
                    Id = "5.3", Title = "Creation", Act = 5, Sequence = 3,
                    DifficultyTier = 10, GravitySeconds = 0.22f, LockDelaySeconds = 0.18f,
                    NextPreviewCount = 1, HoldEnabled = false,
                    Objective = StoryObjective.Lines(32),
                    Modifiers = new[]
                    {
                        StoryModifier.ReducedPreview,
                        StoryModifier.NoHold,
                        StoryModifier.GuardPressure,
                        StoryModifier.TerritoryCells,
                        StoryModifier.HungerMeter,
                        StoryModifier.AdrenalineMonitor,
                        StoryModifier.SignalRelay,
                    },
                    IntroDialogue = StoryDialogueAct5.Intro_5_3,
                    PreMatchDialogue = StoryDialogueAct5.PreMatch_5_3,
                    PostMatchDialogue = StoryDialogueAct5.PostMatch_5_3,
                    DialogueIsGeneratedDraft = true,
                },
            };

            for (var index = 0; index < chapters.Count; index += 1)
            {
                chapters[index].UnlocksNext = index + 1 < chapters.Count ? chapters[index + 1].Id : null;
            }

            return chapters;
        }
    }
}
