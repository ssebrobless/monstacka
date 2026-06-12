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
    /// - CalculatedPlanning's "stricter rotation budget" is intentionally not
    ///   enforced yet (extra preview from spec only) - flagged for tuning review.
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

        private readonly StoryChapterSpec spec;
        private readonly BoardState board;
        private readonly System.Random rng;

        private float hungerTimer;
        private float sedationTimer;
        private float relayTimer;
        private float flickerTimer;
        private bool relayActive;
        private StoryModifier relayedModifier;

        public StoryModifierSystem(StoryChapterSpec chapterSpec, BoardState boardState, int? seed = null)
        {
            spec = chapterSpec;
            board = boardState;
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            board.OnLinesCleared += HandleLinesCleared;
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
    }
}
