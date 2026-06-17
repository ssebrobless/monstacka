using System;
using UnityEngine;

namespace MonStacka.Core
{
    public enum AssistType
    {
        /// <summary>Z / Aggraso: removes garbage cells near the bottom/edges.</summary>
        GuardBreak,
        /// <summary>O / Muwerde: planning window - extra preview + clean placement bonus.</summary>
        Calculation,
        /// <summary>L / Galiffambos: enhanced ghost/safe-placement guidance window.</summary>
        EchoGuide,
        /// <summary>J / Dousema: repairs the deepest covered hole.</summary>
        Stitch,
        /// <summary>S / Sorrisol: eats garbage cells and awards points per cell.</summary>
        Digest,
        /// <summary>T / Lysergicada: slows gravity and extends lock delay for a window.</summary>
        Sedate,
        /// <summary>I / Blyndoolie: danger-window score boost while the stack is high.</summary>
        Alert,
    }

    public readonly struct AssistTrigger
    {
        public readonly AssistType Type;
        public readonly PieceType Piece;
        public readonly int ScoreAwarded;
        public readonly int ComboCount;
        public readonly string Label;

        public AssistTrigger(AssistType type, PieceType piece, int scoreAwarded, int comboCount, string label)
        {
            Type = type;
            Piece = piece;
            ScoreAwarded = scoreAwarded;
            ComboCount = comboCount;
            Label = label;
        }
    }

    /// <summary>
    /// Every-third-held-piece assist system (handoff spec). When a piece that came
    /// from hold is placed, a counter increments; every third one triggers that
    /// piece's monster assist immediately. Instant assists mutate the board through
    /// BoardState helpers; windowed assists expose multipliers the GameManager and
    /// HUD read while active. Scoring rewards planning, not raw power.
    /// </summary>
    public sealed class AssistEffectSystem
    {
        public const int TriggerEvery = 3;

        private const float WindowSeconds = 8f;
        private const int BaseTriggerBonus = 150;
        private const int ComboBonusPerStep = 100;
        private const int DigestPointsPerCell = 40;
        private const int GuardBreakCells = 6;
        private const int DigestCells = 8;
        private const int LineClearDuringWindowBonus = 200;
        private const int DangerSaveBonus = 300;
        /// <summary>Stack height (rows from bottom, visible) considered dangerous.</summary>
        private const int DangerHeightRows = 14;

        /// <summary>A new trigger within this many total placements of the previous one keeps the combo.</summary>
        private const int ComboWindowPlacements = TriggerEvery * 2;

        private int piecesUntilTrigger = TriggerEvery;
        private float windowRemaining;
        private AssistType? activeWindow;
        private PieceType? activeWindowPiece;
        private int comboCount;
        private bool hasTriggeredBefore;
        private int piecesSinceTrigger;

        /// <summary>0..TriggerEvery-1 progress shown in the HUD ("Held Assist: 2/3").</summary>
        public int HeldProgress => TriggerEvery - piecesUntilTrigger;
        public int HeldPlacementsUntilTrigger => piecesUntilTrigger;
        public bool NextHeldPlacementWillTrigger => piecesUntilTrigger <= 1;
        public AssistType? ActiveWindow => windowRemaining > 0f ? activeWindow : null;
        public PieceType? ActiveWindowPiece => windowRemaining > 0f ? activeWindowPiece : null;
        public float WindowRemaining => Mathf.Max(0f, windowRemaining);
        public int ComboCount => comboCount;

        /// <summary>Gravity multiplier while Sedate is active (>1 = slower fall).</summary>
        public float GravityMultiplier => ActiveWindow == AssistType.Sedate ? 1.6f : 1f;
        /// <summary>Extra lock delay seconds while Sedate is active.</summary>
        public float LockDelayBonusSeconds => ActiveWindow == AssistType.Sedate ? 0.15f : 0f;
        /// <summary>Extra next-queue previews while Calculation is active.</summary>
        public int ExtraPreviewCount => ActiveWindow == AssistType.Calculation ? 2 : 0;
        /// <summary>View layer renders an enhanced ghost while EchoGuide is active.</summary>
        public bool EchoGuideActive => ActiveWindow == AssistType.EchoGuide;
        /// <summary>Score multiplier for clears made while Alert is active in danger.</summary>
        public float AlertScoreMultiplier(BoardState board) =>
            ActiveWindow == AssistType.Alert && IsInDanger(board) ? 1.5f : 1f;

        public static bool IsEnabledFor(MonStackaMode mode, bool friendlyAbilitiesEnabled = false) => mode switch
        {
            MonStackaMode.Ogbm => friendlyAbilitiesEnabled,
            MonStackaMode.Sprint40 => friendlyAbilitiesEnabled,
            MonStackaMode.Training => friendlyAbilitiesEnabled,
            MonStackaMode.Story => true,
            _ => true,
        };

        public static AssistType AssistForPiece(PieceType piece) => piece switch
        {
            PieceType.Z => AssistType.GuardBreak,
            PieceType.O => AssistType.Calculation,
            PieceType.L => AssistType.EchoGuide,
            PieceType.J => AssistType.Stitch,
            PieceType.S => AssistType.Digest,
            PieceType.T => AssistType.Sedate,
            PieceType.I => AssistType.Alert,
            _ => AssistType.Alert,
        };

        public static string LabelFor(AssistType type) => type switch
        {
            AssistType.GuardBreak => "GUARD BREAK",
            AssistType.Calculation => "CALCULATION",
            AssistType.EchoGuide => "ECHO GUIDE",
            AssistType.Stitch => "STITCH",
            AssistType.Digest => "DIGEST",
            AssistType.Sedate => "SEDATE",
            AssistType.Alert => "ALERT",
            _ => type.ToString().ToUpperInvariant(),
        };

        public void Reset()
        {
            piecesUntilTrigger = TriggerEvery;
            windowRemaining = 0f;
            activeWindow = null;
            activeWindowPiece = null;
            comboCount = 0;
            hasTriggeredBefore = false;
            piecesSinceTrigger = 0;
        }

        public void SuppressAndReset()
        {
            Reset();
        }

        public void Tick(float deltaTime)
        {
            if (windowRemaining > 0f)
            {
                windowRemaining -= deltaTime;
                if (windowRemaining <= 0f)
                {
                    activeWindow = null;
                    activeWindowPiece = null;
                }
            }
        }

        /// <summary>
        /// Call for every locked piece (non-training). Returns a trigger description
        /// when this placement fired an assist; the returned score is already applied
        /// to scoreSink via the callback.
        /// </summary>
        public AssistTrigger? OnPieceLocked(PieceLockEvent lockEvent, BoardState board, Action<int> awardScore, bool assistsSuppressed = false)
        {
            if (assistsSuppressed)
            {
                SuppressAndReset();
                return null;
            }

            piecesSinceTrigger += 1;

            if (!lockEvent.CameFromHold)
            {
                return null;
            }

            piecesUntilTrigger -= 1;
            if (piecesUntilTrigger > 0)
            {
                return null;
            }

            piecesUntilTrigger = TriggerEvery;

            // Combo: planned back-to-back trigger cycles. A trigger needs 3 held
            // placements; staying within 6 total placements means the player wove
            // hold usage tightly enough to chain.
            comboCount = hasTriggeredBefore && piecesSinceTrigger <= ComboWindowPlacements ? comboCount + 1 : 1;
            hasTriggeredBefore = true;
            piecesSinceTrigger = 0;

            var wasInDanger = IsInDanger(board);
            var type = AssistForPiece(lockEvent.PieceType);
            var score = BaseTriggerBonus + ((comboCount - 1) * ComboBonusPerStep);

            switch (type)
            {
                case AssistType.GuardBreak:
                {
                    var cleared = board.ClearGarbageCells(GuardBreakCells);
                    score += cleared * 20;
                    if (wasInDanger && cleared > 0)
                    {
                        score += DangerSaveBonus;
                    }
                    break;
                }
                case AssistType.Digest:
                {
                    var eaten = board.ClearGarbageCells(DigestCells);
                    score += eaten * DigestPointsPerCell;
                    if (wasInDanger && eaten > 0)
                    {
                        score += DangerSaveBonus;
                    }
                    break;
                }
                case AssistType.Stitch:
                {
                    if (board.TryRepairDeepestHole())
                    {
                        score += 120;
                    }
                    break;
                }
                case AssistType.Calculation:
                case AssistType.EchoGuide:
                case AssistType.Sedate:
                case AssistType.Alert:
                    activeWindow = type;
                    activeWindowPiece = lockEvent.PieceType;
                    windowRemaining = WindowSeconds;
                    if (type == AssistType.Alert && wasInDanger)
                    {
                        score += DangerSaveBonus;
                    }
                    break;
            }

            awardScore?.Invoke(score);
            return new AssistTrigger(type, lockEvent.PieceType, score, comboCount, LabelFor(type));
        }

        /// <summary>Call when lines clear; awards the during-window bonus.</summary>
        public int OnLinesCleared(int lines, Action<int> awardScore)
        {
            if (lines <= 0 || ActiveWindow == null)
            {
                return 0;
            }

            var bonus = lines * LineClearDuringWindowBonus;
            awardScore?.Invoke(bonus);
            return bonus;
        }

        public static bool IsInDanger(BoardState board)
        {
            if (board == null)
            {
                return false;
            }

            var dangerRow = PieceDefinitions.TotalRows - DangerHeightRows;
            for (var row = 0; row <= dangerRow; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    if (board.Grid[row, col] != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
