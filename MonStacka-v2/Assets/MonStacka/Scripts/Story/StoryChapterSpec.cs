using System;
using System.Collections.Generic;
using MonStacka.Core;

namespace MonStacka.Story
{
    public enum DialogueSpeaker
    {
        Player = 0,
        Narrator = 1,
        PaSystem = 2,
    }

    [Serializable]
    public struct DialogueLine
    {
        public DialogueSpeaker Speaker;
        public string Text;
        /// <summary>True when the line is an internal thought (rendered in italics, *like this* in the script).</summary>
        public bool IsThought;

        public DialogueLine(DialogueSpeaker speaker, string text, bool isThought = false)
        {
            Speaker = speaker;
            Text = text;
            IsThought = isThought;
        }
    }

    public enum StoryObjectiveKind
    {
        /// <summary>Clear N lines to finish the mission.</summary>
        ClearLines = 0,
        /// <summary>Reach a score target before topping out.</summary>
        ReachScore = 1,
        /// <summary>Survive for a duration (seconds) without topping out.</summary>
        SurviveSeconds = 2,
        /// <summary>Clear N lines within a time limit.</summary>
        ClearLinesTimed = 3,
    }

    [Serializable]
    public struct StoryObjective
    {
        public StoryObjectiveKind Kind;
        public int TargetLines;
        public int TargetScore;
        public float TimeLimitSeconds;
        public int BossHealthPoints;

        public bool HasBossHealth => BossHealthPoints > 0;
        public bool HasTimeLimit => TimeLimitSeconds > 0f;

        public static StoryObjective Lines(int lines) =>
            new() { Kind = StoryObjectiveKind.ClearLines, TargetLines = lines };

        public static StoryObjective Score(int score) =>
            new() { Kind = StoryObjectiveKind.ReachScore, TargetScore = score };

        public static StoryObjective Survive(float seconds) =>
            new() { Kind = StoryObjectiveKind.SurviveSeconds, TimeLimitSeconds = seconds };

        public static StoryObjective TimedLines(int lines, float seconds) =>
            new() { Kind = StoryObjectiveKind.ClearLinesTimed, TargetLines = lines, TimeLimitSeconds = seconds };

        public static StoryObjective Boss(int healthPoints, float seconds = 0f) =>
            new() { Kind = StoryObjectiveKind.ReachScore, TargetScore = healthPoints, BossHealthPoints = healthPoints, TimeLimitSeconds = seconds };

        public StoryObjective WithBossHealth(int healthPoints, float seconds = 0f)
        {
            BossHealthPoints = healthPoints;
            if (seconds > 0f)
            {
                TimeLimitSeconds = seconds;
            }

            return this;
        }
    }

    /// <summary>
    /// Chapter gameplay modifiers. Each maps to a monster theme from the handoff.
    /// Implementations live in StoryModifierSystem; specs only declare which are active.
    /// </summary>
    public enum StoryModifier
    {
        /// <summary>Aggraso/Z: faster lock delay once the piece touches the ground.</summary>
        GuardPressure,
        /// <summary>Deprecated/internal: claimed-cell spread helper, now surfaced as Dousema/J ResilientCells.</summary>
        TerritoryCells,
        /// <summary>Muwerde/O: Calculated Prediction queues a score debuff after more than four rotations.</summary>
        CalculatedPlanning,
        /// <summary>Muwerde/O: precision scoring - bonus for flush placements, penalty cells for overhangs.</summary>
        PrecisionPressure,
        /// <summary>Galiffambos/L: Vision Loss flickers placed board blocks on a cooldown.</summary>
        GhostFlicker,
        /// <summary>Deprecated Galiffambos/L dimming modifier kept for old data compatibility.</summary>
        EcholocationDim,
        /// <summary>Dousema/J: one permanent enemy claim spreads to adjacent locked cells; claimed cells do not count for row clears.</summary>
        ResilientCells,
        /// <summary>Dousema/J: hold box and hints disabled (mouth sewn shut).</summary>
        MutedHints,
        /// <summary>Sorrisol/S: Insatiable Hunger consumes a whole top-layer block after enough player-cleared lines.</summary>
        HungerMeter,
        /// <summary>Lysergicada/T: Sedating Spit wipes and blocks friendly assist charge.</summary>
        SedationWindows,
        /// <summary>Blyndoolie/I: Adrenaline Rush periodically boosts enemy abilities for a fixed window.</summary>
        AdrenalineMonitor,
        /// <summary>Deprecated legacy value. Signal Relay is retired and ignored by runtime story modifiers.</summary>
        SignalRelay,
        /// <summary>Chapter 5: reduced next-queue preview.</summary>
        ReducedPreview,
        /// <summary>Chapter 5: hold disabled.</summary>
        NoHold,
    }

    /// <summary>
    /// Explicit chapter metadata, per handoff: story mode is data driven, never
    /// inferred from prose. Dialogue authored in StoryDialogue.* files.
    /// </summary>
    public sealed class StoryChapterSpec
    {
        public string Id;
        public string Title;
        public int Act;
        public int Sequence;
        /// <summary>Monster/piece focus of the chapter (empty for chapter 5: no bias).</summary>
        public PieceType[] FocusedPieces = Array.Empty<PieceType>();
        /// <summary>Spawn weight per piece. 1 = normal. Missing entries default to 1. Empty dict = unbiased.</summary>
        public Dictionary<PieceType, float> SpawnBias = new();
        /// <summary>1 (intro) .. 10 (chapter 5 remix).</summary>
        public int DifficultyTier = 1;
        public float GravitySeconds = 0.65f;
        public float LockDelaySeconds = 0.25f;
        public int NextPreviewCount = 3;
        public bool HoldEnabled = true;
        public DialogueLine[] IntroDialogue = Array.Empty<DialogueLine>();
        public DialogueLine[] PreMatchDialogue = Array.Empty<DialogueLine>();
        public DialogueLine[] PostMatchDialogue = Array.Empty<DialogueLine>();
        public StoryObjective Objective = StoryObjective.Lines(10);
        public StoryModifier[] Modifiers = Array.Empty<StoryModifier>();
        public string UnlocksNext;
        /// <summary>True when the chapter-piece mapping or prose was not confirmed by the user.</summary>
        public bool NeedsUserMapping;
        /// <summary>True when dialogue is a generated draft (written by agent in the script's voice) awaiting user review.</summary>
        public bool DialogueIsGeneratedDraft;
    }
}
