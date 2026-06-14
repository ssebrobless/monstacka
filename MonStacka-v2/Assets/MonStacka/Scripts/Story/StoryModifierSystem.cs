using System.Collections.Generic;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Story
{
    /// <summary>
    /// Runtime behavior for chapter modifiers declared in StoryChapterSpec.
    /// Plain C# (no MonoBehaviour); GameManager ticks it and reads its outputs.
    /// Data-driven pieces (preview count, hold, gravity, lock delay, spawn bias)
    /// are applied directly from the spec by GameManager; this class implements
    /// the time/board behaviors.
    ///
    /// Notes against the handoff:
    /// - CalculatedPlanning is enforced as a per-piece rotation budget.
    /// - PrecisionPressure is enforced as an unsupported-overhang check on lock.
    /// - ResilientCells is implemented as regrowth: each line clear has a chance
    ///   to reseed one territory cell ("the flesh regrows"), rather than a second
    ///   grid cell type, to keep clear rules deterministic and readable.
    /// </summary>
    public sealed class StoryModifierSystem
    {
        private const float HungerBaseWindowSeconds = 22f;
        private const float SedationCycleSeconds = 18f;
        private const float SedationWarningSeconds = 3f;
        private const float SedationActiveSeconds = 4f;
        private const float SignalRelayCycleSeconds = 25f;
        private const float SignalRelayActiveSeconds = 6f;
        private const float FlickerCycleSeconds = 2.6f;
        private const float FlickerOffSeconds = 0.35f;
        private const int AdrenalineHeightRows = 13;
        private const int CalculatedPlanningRotationBudget = 2;
        private const int CalculatedPlanningMaxPenaltyCells = 3;
        private const int PrecisionPressureMaxPenaltyCells = 3;

        private readonly StoryChapterSpec spec;
        private readonly BoardState board;
        private readonly System.Random rng;

        private float hungerTimer;
        private float sedationTimer;
        private float relayTimer;
        private float flickerTimer;
        private bool relayActive;
        private StoryModifier relayedModifier;
        private string lastCalculatedPlanningStatus = "safe";
        private string lastPrecisionPressureStatus = "safe";

        public StoryModifierSystem(StoryChapterSpec chapterSpec, BoardState boardState, int? seed = null)
        {
            spec = chapterSpec;
            board = boardState;
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            board.OnLinesCleared += HandleLinesCleared;
            board.OnPieceLocked += HandlePieceLocked;
        }

        public bool Has(StoryModifier modifier)
        {
            foreach (var entry in spec.Modifiers)
            {
                if (entry == modifier)
                {
                    return true;
                }
            }

            return relayActive && relayedModifier == modifier;
        }

        /// <summary>Lock delay multiplier (&lt;1 = faster lock under Aggraso guard pressure).</summary>
        public float LockDelayMultiplier => Has(StoryModifier.GuardPressure) ? 0.6f : 1f;

        /// <summary>Gravity multiplier (&lt;1 = faster fall under adrenaline).</summary>
        public float GravityMultiplier =>
            Has(StoryModifier.AdrenalineMonitor) && IsStackHigh() ? 0.7f : 1f;

        /// <summary>DAS/ARR multiplier while sedated (&gt;1 = sluggish controls).</summary>
        public float InputSluggishMultiplier => SedationActive ? 2.4f : 1f;

        public bool SedationWarning =>
            Has(StoryModifier.SedationWindows) &&
            sedationTimer >= SedationCycleSeconds - SedationWarningSeconds - SedationActiveSeconds &&
            sedationTimer < SedationCycleSeconds - SedationActiveSeconds;

        public bool SedationActive =>
            Has(StoryModifier.SedationWindows) &&
            sedationTimer >= SedationCycleSeconds - SedationActiveSeconds;

        /// <summary>Board dim alpha for Galiffambos echolocation chapters (0 = fully visible).</summary>
        public float BoardDimAlpha
        {
            get
            {
                if (!Has(StoryModifier.EcholocationDim))
                {
                    return 0f;
                }

                // Periodic "echo flash": mostly dim, briefly clear every cycle.
                var phase = Time.time % 3.5f;
                return phase < 0.5f ? 0f : 0.42f;
            }
        }

        /// <summary>Active piece visibility for Galiffambos ghost-flicker chapters.</summary>
        public bool ActivePieceVisible
        {
            get
            {
                if (!Has(StoryModifier.GhostFlicker))
                {
                    return true;
                }

                return flickerTimer % FlickerCycleSeconds >= FlickerOffSeconds;
            }
        }

        /// <summary>Hide assist/status hints for Dousema muted chapters.</summary>
        public bool HintsMuted => Has(StoryModifier.MutedHints);

        /// <summary>Compact status chips shown in the HUD ("HUNGER 12s | SEDATION...").</summary>
        public string BuildStatusChips()
        {
            var chips = new System.Text.StringBuilder();
            if (Has(StoryModifier.HungerMeter))
            {
                var remaining = Mathf.Max(0f, HungerWindowSeconds - hungerTimer);
                Append(chips, $"HUNGER {Mathf.CeilToInt(remaining)}s");
            }

            if (SedationActive)
            {
                Append(chips, "SEDATED");
            }
            else if (SedationWarning)
            {
                Append(chips, "SEDATION INCOMING");
            }

            if (Has(StoryModifier.AdrenalineMonitor) && IsStackHigh())
            {
                Append(chips, "ADRENALINE SPIKE");
            }

            if (relayActive)
            {
                Append(chips, "SIGNAL RELAY");
            }

            if (Has(StoryModifier.GuardPressure))
            {
                Append(chips, "GUARD");
            }

            return chips.ToString();
        }

        /// <summary>Detailed story-mode enemy readout for the right HUD panel.</summary>
        public string BuildEnemyAbilityStatus()
        {
            var status = new System.Text.StringBuilder();

            if (Has(StoryModifier.GuardPressure))
            {
                AppendStatus(status, "Guard Pressure", "ON", $"{RelayTag(StoryModifier.GuardPressure)}every piece locks faster: delay x0.6");
            }

            if (Has(StoryModifier.TerritoryCells))
            {
                var seedCount = HasDeclared(StoryModifier.TerritoryCells)
                    ? 4 + spec.DifficultyTier
                    : 3;
                AppendStatus(status, "Territory Cells", "SETUP", $"{RelayTag(StoryModifier.TerritoryCells)}{seedCount} enemy cells seeded at match start");
            }

            if (Has(StoryModifier.CalculatedPlanning))
            {
                var previewDelta = Mathf.Max(0, spec.NextPreviewCount - 3);
                var rotationPressure = board.CurrentPieceRotations <= CalculatedPlanningRotationBudget
                    ? $"rotations {board.CurrentPieceRotations}/{CalculatedPlanningRotationBudget}"
                    : $"rotations {board.CurrentPieceRotations}/{CalculatedPlanningRotationBudget}: penalty armed";
                var previewStatus = previewDelta > 0
                    ? $"+{previewDelta} next; {rotationPressure}; last {lastCalculatedPlanningStatus}"
                    : $"{rotationPressure}; last {lastCalculatedPlanningStatus}";
                AppendStatus(status, "Calculated Planning", "LOCK", previewStatus);
            }

            if (Has(StoryModifier.PrecisionPressure))
            {
                AppendStatus(status, "Precision Pressure", "LOCK", $"unsupported overhangs seed cells; last {lastPrecisionPressureStatus}");
            }

            if (Has(StoryModifier.GhostFlicker))
            {
                var phase = flickerTimer % FlickerCycleSeconds;
                var flickerStatus = phase < FlickerOffSeconds
                    ? $"hidden {Seconds(FlickerOffSeconds - phase)}"
                    : $"next blink {Seconds(FlickerCycleSeconds - phase)}";
                AppendStatus(status, "Ghost Flicker", "TIMER", $"{RelayTag(StoryModifier.GhostFlicker)}{flickerStatus}");
            }

            if (Has(StoryModifier.EcholocationDim))
            {
                var phase = Time.time % 3.5f;
                var echoStatus = phase < 0.5f
                    ? $"clear {Seconds(0.5f - phase)}"
                    : $"next flash {Seconds(3.5f - phase)}";
                AppendStatus(status, "Echolocation Dim", "TIMER", echoStatus);
            }

            if (Has(StoryModifier.ResilientCells))
            {
                var chance = Mathf.RoundToInt((0.3f + (spec.DifficultyTier * 0.03f)) * 100f);
                AppendStatus(status, "Resilient Cells", "CLEAR", $"line clears have {chance}% regrow chance");
            }

            if (Has(StoryModifier.MutedHints))
            {
                AppendStatus(status, "Muted Hints", "ON", "assist hints hidden");
            }

            if (Has(StoryModifier.HungerMeter))
            {
                var remaining = HungerWindowSeconds - hungerTimer;
                AppendStatus(status, "Hunger Meter", "TIMER", $"garbage row in {Seconds(remaining)} unless you clear a line");
            }

            if (Has(StoryModifier.SedationWindows))
            {
                var activeStart = SedationCycleSeconds - SedationActiveSeconds;
                var warningStart = activeStart - SedationWarningSeconds;
                if (SedationActive)
                {
                    AppendStatus(status, "Sedation", "ACTIVE", $"sluggish controls for {Seconds(SedationCycleSeconds - sedationTimer)}");
                }
                else if (SedationWarning)
                {
                    AppendStatus(status, "Sedation", "WARNING", $"sluggish controls start in {Seconds(activeStart - sedationTimer)}");
                }
                else
                {
                    AppendStatus(status, "Sedation", "TIMER", $"warning in {Seconds(warningStart - sedationTimer)}");
                }
            }

            if (Has(StoryModifier.AdrenalineMonitor))
            {
                var adrenalineStatus = IsStackHigh()
                    ? $"{RelayTag(StoryModifier.AdrenalineMonitor)}active: gravity x0.7"
                    : $"{RelayTag(StoryModifier.AdrenalineMonitor)}armed at high stack";
                AppendStatus(status, "Adrenaline Monitor", IsStackHigh() ? "ACTIVE" : "ARMED", adrenalineStatus);
            }

            if (HasDeclared(StoryModifier.SignalRelay))
            {
                var relayStatus = relayActive
                    ? $"{ModifierLabel(relayedModifier)} for {Seconds(SignalRelayActiveSeconds - relayTimer)}"
                    : $"next relay in {Seconds(SignalRelayCycleSeconds - relayTimer)}";
                AppendStatus(status, "Signal Relay", relayActive ? "ACTIVE" : "TIMER", relayStatus);
            }

            if (Has(StoryModifier.ReducedPreview))
            {
                AppendStatus(status, "Reduced Preview", "ON", $"{spec.NextPreviewCount} next shown");
            }

            if (Has(StoryModifier.NoHold))
            {
                AppendStatus(status, "No Hold", "ON", "hold disabled");
            }

            return status.Length > 0 ? status.ToString() : "No enemy modifiers";
        }

        public void OnMatchStart()
        {
            if (Has(StoryModifier.TerritoryCells))
            {
                board.SeedTerritoryCells(4 + spec.DifficultyTier);
            }

            hungerTimer = 0f;
            sedationTimer = 0f;
            relayTimer = 0f;
            flickerTimer = 0f;
            relayActive = false;
            lastCalculatedPlanningStatus = "safe";
            lastPrecisionPressureStatus = "safe";
        }

        public void Tick(float deltaTime)
        {
            flickerTimer += deltaTime;

            if (Has(StoryModifier.HungerMeter))
            {
                hungerTimer += deltaTime;
                if (hungerTimer >= HungerWindowSeconds)
                {
                    hungerTimer = 0f;
                    board.AddGarbageRow(rng.Next(0, PieceDefinitions.Columns));
                }
            }

            if (Has(StoryModifier.SedationWindows))
            {
                sedationTimer += deltaTime;
                if (sedationTimer >= SedationCycleSeconds)
                {
                    sedationTimer = 0f;
                }
            }

            if (HasDeclared(StoryModifier.SignalRelay))
            {
                relayTimer += deltaTime;
                if (!relayActive && relayTimer >= SignalRelayCycleSeconds)
                {
                    relayActive = true;
                    relayTimer = 0f;
                    relayedModifier = PickRelayModifier();
                    if (relayedModifier == StoryModifier.TerritoryCells)
                    {
                        board.SeedTerritoryCells(3);
                    }
                }
                else if (relayActive && relayTimer >= SignalRelayActiveSeconds)
                {
                    relayActive = false;
                    relayTimer = 0f;
                }
            }
        }

        private float HungerWindowSeconds =>
            Mathf.Max(10f, HungerBaseWindowSeconds - spec.DifficultyTier);

        /// <summary>Declared on the spec itself (relay never relays itself).</summary>
        private bool HasDeclared(StoryModifier modifier)
        {
            foreach (var entry in spec.Modifiers)
            {
                if (entry == modifier)
                {
                    return true;
                }
            }

            return false;
        }

        private StoryModifier PickRelayModifier()
        {
            var options = new[]
            {
                StoryModifier.GuardPressure,
                StoryModifier.TerritoryCells,
                StoryModifier.GhostFlicker,
                StoryModifier.AdrenalineMonitor,
            };
            return options[rng.Next(options.Length)];
        }

        private void HandleLinesCleared(int lines)
        {
            hungerTimer = 0f;

            if (HasDeclared(StoryModifier.ResilientCells) && lines > 0)
            {
                // Regrowth: clearing flesh leaves scar tissue behind sometimes.
                var chance = 0.3f + (spec.DifficultyTier * 0.03f);
                if (rng.NextDouble() < chance)
                {
                    board.SeedTerritoryCells(1);
                }
            }
        }

        private void HandlePieceLocked(PieceLockEvent lockEvent)
        {
            if (Has(StoryModifier.CalculatedPlanning))
            {
                var extraRotations = Mathf.Max(0, lockEvent.RotationInputs - CalculatedPlanningRotationBudget);
                if (extraRotations > 0)
                {
                    var penaltyCells = Mathf.Min(CalculatedPlanningMaxPenaltyCells, extraRotations);
                    board.SeedTerritoryCells(penaltyCells);
                    lastCalculatedPlanningStatus = $"+{penaltyCells} cells after {lockEvent.RotationInputs} rotations";
                }
                else
                {
                    lastCalculatedPlanningStatus = $"{lockEvent.RotationInputs}/{CalculatedPlanningRotationBudget} rotations";
                }
            }

            if (Has(StoryModifier.PrecisionPressure))
            {
                var unsupportedCells = CountUnsupportedCells(lockEvent.Cells);
                if (unsupportedCells > 0)
                {
                    var penaltyCells = Mathf.Min(PrecisionPressureMaxPenaltyCells, unsupportedCells);
                    board.SeedTerritoryCells(penaltyCells);
                    lastPrecisionPressureStatus = $"+{penaltyCells} cells from {unsupportedCells} overhangs";
                }
                else
                {
                    lastPrecisionPressureStatus = "clean lock";
                }
            }
        }

        private int CountUnsupportedCells(IReadOnlyList<Vector2Int> cells)
        {
            var lockedCells = new HashSet<Vector2Int>(cells);
            var unsupported = 0;
            foreach (var cell in cells)
            {
                if (cell.x < 0 || cell.x >= PieceDefinitions.Columns || cell.y < 0 || cell.y >= PieceDefinitions.TotalRows - 1)
                {
                    continue;
                }

                var below = new Vector2Int(cell.x, cell.y + 1);
                if (lockedCells.Contains(below))
                {
                    continue;
                }

                if (board.Grid[below.y, below.x] == 0)
                {
                    unsupported += 1;
                }
            }

            return unsupported;
        }

        private bool IsStackHigh()
        {
            var dangerRow = PieceDefinitions.TotalRows - AdrenalineHeightRows;
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

        private static void Append(System.Text.StringBuilder builder, string chip)
        {
            if (builder.Length > 0)
            {
                builder.Append("  |  ");
            }

            builder.Append(chip);
        }

        private string RelayTag(StoryModifier modifier) =>
            relayActive && relayedModifier == modifier && !HasDeclared(modifier) ? "relay: " : string.Empty;

        private static void AppendStatus(System.Text.StringBuilder builder, string name, string state, string detail)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("<color=#ffcf74>");
            builder.Append(name);
            builder.Append("</color> [");
            builder.Append(state);
            builder.Append("] ");
            builder.Append(detail);
        }

        private static string Seconds(float seconds) =>
            $"{Mathf.CeilToInt(Mathf.Max(0f, seconds))}s";

        private static string ModifierLabel(StoryModifier modifier) =>
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
    }
}
