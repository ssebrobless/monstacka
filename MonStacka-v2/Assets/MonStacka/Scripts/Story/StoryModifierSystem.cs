using System.Collections.Generic;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Story
{
    public readonly struct StoryModifierTriggerEvent
    {
        public readonly StoryModifier Modifier;
        public readonly string Name;
        public readonly string State;
        public readonly string Detail;

        public StoryModifierTriggerEvent(StoryModifier modifier, string name, string state, string detail)
        {
            Modifier = modifier;
            Name = name;
            State = state;
            Detail = detail;
        }
    }

    /// <summary>
    /// Runtime behavior for chapter modifiers declared in StoryChapterSpec.
    /// Plain C# (no MonoBehaviour); GameManager ticks it and reads its outputs.
    /// Data-driven pieces (preview count, hold, gravity, lock delay, spawn bias)
    /// are applied directly from the spec by GameManager; this class implements
    /// the time/board behaviors.
    ///
    /// Notes against the handoff:
    /// - CalculatedPlanning queues a score debuff after excess rotations.
    /// - PrecisionPressure is enforced as an unsupported-overhang check on lock.
    /// - ResilientCells owns the claimed-cell spread behavior: one permanent
    ///   source expands to adjacent locked cells, and claims clear oldest-first.
    /// </summary>
    public sealed class StoryModifierSystem
    {
        private const float GuardPressureBaseWindowSeconds = 18f;
        private const float GuardPressureActiveSeconds = 6f;
        private const float TerritoryClaimWindowSeconds = 10f;
        private const float SedatingSpitCooldownSeconds = 15f;
        private const float SedatingSpitBaseActiveSeconds = 4f;
        private const float SedatingSpitMaxActiveSeconds = 8f;
        private const float SignalRelayCycleSeconds = 25f;
        private const float SignalRelayActiveSeconds = 6f;
        private const float AdrenalineRushCooldownSeconds = 20f;
        private const float AdrenalineRushActiveSeconds = 11f;
        private const int AdrenalineRushDifficultyBoost = 6;
        private const float BlindedCooldownSeconds = 12f;
        private const float BlindedFlickerIntervalSeconds = 0.5f;
        private const float BlindedBaseActiveSeconds = 4f;
        private const float BlindedMaxActiveSeconds = 7f;
        private const int CalculatedPlanningRotationBudget = 3;
        private const int PrecisionPressureMaxPenaltyCells = 3;

        private readonly StoryChapterSpec spec;
        private readonly BoardState board;
        private readonly System.Random rng;

        private int hungerClearedLineProgress;
        private float guardPressureTimer;
        private float territoryTimer;
        private readonly List<float> guardPressureRowTimers = new();
        private float sedatingSpitCooldownTimer;
        private float sedatingSpitActiveTimer;
        private bool sedatingSpitActive;
        private float relayTimer;
        private float adrenalineRushCooldownTimer;
        private float adrenalineRushActiveTimer;
        private float blindedCooldownTimer;
        private float blindedActiveTimer;
        private float blindedCurrentActiveSeconds;
        private float sedatingSpitCurrentActiveSeconds;
        private bool blindedActive;
        private bool adrenalineRushActive;
        private bool relayActive;
        private StoryModifier relayedModifier;
        private bool calculatedPlanningDebuffQueued;
        private string lastCalculatedPlanningStatus = "safe";
        private string lastPrecisionPressureStatus = "safe";

        public event System.Action<StoryModifierTriggerEvent> OnModifierTriggered;

        public StoryModifierSystem(StoryChapterSpec chapterSpec, BoardState boardState, int? seed = null)
        {
            spec = chapterSpec;
            board = boardState;
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            board.OnLinesCleared += HandleLinesCleared;
            board.OnPieceLocked += HandlePieceLocked;
            board.OnPieceRotated += HandlePieceRotated;
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

        /// <summary>Lock delay multiplier (&lt;1 = faster lock under special story modifiers).</summary>
        public float LockDelayMultiplier => 1f;

        /// <summary>Gravity multiplier (&lt;1 = faster fall); Adrenaline Rush boosts enemy abilities instead.</summary>
        public float GravityMultiplier => 1f;

        /// <summary>DAS/ARR multiplier. Sedating Spit no longer slows movement.</summary>
        public float InputSluggishMultiplier => 1f;

        public bool SedatingSpitActive => Has(StoryModifier.SedationWindows) && sedatingSpitActive;

        public bool AssistsSuppressed => SedatingSpitActive;

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

        /// <summary>Active pieces stay visible; Blinded flickers only locked/placed blocks.</summary>
        public bool ActivePieceVisible => true;

        public bool BlindedActive => Has(StoryModifier.GhostFlicker) && blindedActive;

        public bool LockedPiecesVisible
        {
            get
            {
                if (!BlindedActive)
                {
                    return true;
                }

                var flickerStep = Mathf.FloorToInt(blindedActiveTimer / BlindedFlickerIntervalSeconds);
                return flickerStep % 2 == 0;
            }
        }

        /// <summary>Hide assist/status hints for Dousema muted chapters.</summary>
        public bool HintsMuted => Has(StoryModifier.MutedHints);

        /// <summary>Compact status chips shown in the HUD ("HUNGER 1/3 | SEDATION...").</summary>
        public string BuildStatusChips()
        {
            var chips = new System.Text.StringBuilder();
            if (Has(StoryModifier.HungerMeter))
            {
                Append(chips, $"HUNGER {hungerClearedLineProgress}/{InsatiableHungerLineRequirement}");
            }

            if (SedatingSpitActive)
            {
                Append(chips, "SEDATING SPIT");
            }

            if (adrenalineRushActive)
            {
                Append(chips, "ADRENALINE RUSH");
            }

            if (relayActive)
            {
                Append(chips, "SIGNAL RELAY");
            }

            if (Has(StoryModifier.GuardPressure))
            {
                Append(chips, guardPressureRowTimers.Count > 0 ? $"GUARD ROW x{guardPressureRowTimers.Count}" : "GUARD TIMER");
            }

            return chips.ToString();
        }

        /// <summary>Detailed story-mode enemy readout for the right HUD panel.</summary>
        public string BuildEnemyAbilityStatus()
        {
            var status = new System.Text.StringBuilder();

            if (Has(StoryModifier.GuardPressure))
            {
                if (guardPressureRowTimers.Count > 0)
                {
                    var oldestRemaining = GuardPressureActiveSeconds - guardPressureRowTimers[0];
                    AppendStatus(
                        status,
                        "Guard Pressure",
                        "ACTIVE",
                        $"{RelayTag(StoryModifier.GuardPressure)}{guardPressureRowTimers.Count} pressure row{Plural(guardPressureRowTimers.Count)} active; oldest clears in {Seconds(oldestRemaining)}; clear a line to remove one early");
                }
                else
                {
                    var remaining = GuardPressureWindowSeconds - guardPressureTimer;
                    AppendStatus(status, "Guard Pressure", "TIMER", $"{RelayTag(StoryModifier.GuardPressure)}pressure row in {Seconds(remaining)}");
                }
            }

            if (HasClaimingCells)
            {
                var claimCount = board.GetTerritoryClaimedBlockCount();
                var sourceStatus = board.GetTerritorySourceCells().Count > 0
                    ? $"source locked; {claimCount} claim{Plural(claimCount)} active"
                    : "source pending";
                var capped = claimCount >= TerritoryClaimLimit
                    ? $"claim cap {claimCount}/{TerritoryClaimLimit}; clear rows to unclaim oldest"
                    : $"next claim in {Seconds(TerritoryClaimWindowSeconds - territoryTimer)}; cap {claimCount}/{TerritoryClaimLimit}";
                AppendStatus(status, "Resilient Cells", "TIMER", $"{RelayTag(StoryModifier.ResilientCells)}{sourceStatus}; {capped}");
            }

            if (Has(StoryModifier.CalculatedPlanning))
            {
                var previewDelta = Mathf.Max(0, spec.NextPreviewCount - 3);
                var reductionPercent = Mathf.RoundToInt((1f - CalculatedPlanningScoreMultiplier) * 100f);
                var rotationPressure = calculatedPlanningDebuffQueued
                    ? $"score -{reductionPercent}% queued for next block"
                    : board.CurrentPieceRotations <= CalculatedPlanningRotationBudget
                    ? $"rotations {board.CurrentPieceRotations}/{CalculatedPlanningRotationBudget}"
                    : $"rotations {board.CurrentPieceRotations}/{CalculatedPlanningRotationBudget}: debuff armed";
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
                if (BlindedActive)
                {
                    var phase = blindedActiveTimer % BlindedFlickerIntervalSeconds;
                    var visibleStatus = LockedPiecesVisible ? "visible" : "invisible";
                    AppendStatus(
                        status,
                        "Blinded",
                        "ACTIVE",
                        $"{RelayTag(StoryModifier.GhostFlicker)}placed blocks {visibleStatus}; next flicker {Seconds(BlindedFlickerIntervalSeconds - phase)}; ends in {Seconds(blindedCurrentActiveSeconds - blindedActiveTimer)}");
                }
                else
                {
                    AppendStatus(status, "Blinded", "TIMER", $"{RelayTag(StoryModifier.GhostFlicker)}flicker starts in {Seconds(BlindedCooldownSeconds - blindedCooldownTimer)}");
                }
            }

            if (Has(StoryModifier.MutedHints))
            {
                AppendStatus(status, "Muted Hints", "ON", "assist hints hidden");
            }

            if (Has(StoryModifier.HungerMeter))
            {
                AppendStatus(
                    status,
                    "Insatiable Hunger",
                    "LINES",
                    $"{RelayTag(StoryModifier.HungerMeter)}{hungerClearedLineProgress}/{InsatiableHungerLineRequirement} cleared lines; next trigger eats one whole top-layer block");
            }

            if (Has(StoryModifier.SedationWindows))
            {
                if (SedatingSpitActive)
                {
                    AppendStatus(status, "Sedating Spit", "ACTIVE", $"friendly assists blocked for {Seconds(sedatingSpitCurrentActiveSeconds - sedatingSpitActiveTimer)}; clear a row to end early");
                }
                else
                {
                    AppendStatus(status, "Sedating Spit", "TIMER", $"assist lockout in {Seconds(SedatingSpitCooldownSeconds - sedatingSpitCooldownTimer)}");
                }
            }

            if (Has(StoryModifier.AdrenalineMonitor))
            {
                if (adrenalineRushActive)
                {
                    AppendStatus(
                        status,
                        "Adrenaline Rush",
                        "ACTIVE",
                        $"{RelayTag(StoryModifier.AdrenalineMonitor)}enemy abilities boosted for {Seconds(AdrenalineRushActiveSeconds - adrenalineRushActiveTimer)}");
                }
                else
                {
                    AppendStatus(
                        status,
                        "Adrenaline Rush",
                        "TIMER",
                        $"{RelayTag(StoryModifier.AdrenalineMonitor)}next boost in {Seconds(AdrenalineRushCooldownSeconds - adrenalineRushCooldownTimer)}");
                }
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
            if (HasClaimingCells)
            {
                board.SeedTerritorySource();
                EmitModifierTrigger(ClaimingCellsEventModifier, "SETUP", "permanent claimed source seeded");
            }

            if (HasDeclared(StoryModifier.ReducedPreview))
            {
                EmitModifierTrigger(StoryModifier.ReducedPreview, "ON", $"{spec.NextPreviewCount} next shown");
            }

            if (HasDeclared(StoryModifier.NoHold))
            {
                EmitModifierTrigger(StoryModifier.NoHold, "ON", "hold disabled");
            }

            hungerClearedLineProgress = 0;
            guardPressureTimer = 0f;
            territoryTimer = 0f;
            guardPressureRowTimers.Clear();
            sedatingSpitCooldownTimer = 0f;
            sedatingSpitActiveTimer = 0f;
            sedatingSpitActive = false;
            relayTimer = 0f;
            adrenalineRushCooldownTimer = 0f;
            adrenalineRushActiveTimer = 0f;
            adrenalineRushActive = false;
            blindedCooldownTimer = 0f;
            blindedActiveTimer = 0f;
            blindedCurrentActiveSeconds = BlindedActiveSeconds;
            sedatingSpitCurrentActiveSeconds = SedatingSpitActiveSeconds;
            blindedActive = false;
            relayActive = false;
            calculatedPlanningDebuffQueued = false;
            lastCalculatedPlanningStatus = "safe";
            lastPrecisionPressureStatus = "safe";
        }

        public void Tick(float deltaTime)
        {
            TickAdrenalineRush(deltaTime);
            TickBlinded(deltaTime);
            TickGuardPressure(deltaTime);
            TickTerritoryCells(deltaTime);

            TickSedatingSpit(deltaTime);

            if (HasDeclared(StoryModifier.SignalRelay))
            {
                relayTimer += deltaTime;
                if (!relayActive && relayTimer >= SignalRelayCycleSeconds)
                {
                    relayActive = true;
                    relayTimer = 0f;
                    relayedModifier = PickRelayModifier();
                    EmitModifierTrigger(StoryModifier.SignalRelay, "ACTIVE", $"{ModifierLabel(relayedModifier)} relayed");
                    if (relayedModifier == StoryModifier.ResilientCells)
                    {
                        if (board.GetTerritorySourceCells().Count == 0)
                        {
                            board.SeedTerritorySource();
                            EmitModifierTrigger(StoryModifier.ResilientCells, "SETUP", "relay seeded a claimed source");
                        }
                        else if (board.TryClaimAdjacentTerritoryCell(rng))
                        {
                            EmitModifierTrigger(StoryModifier.ResilientCells, "CLAIM", "relay claimed a touching block");
                        }
                    }
                }
                else if (relayActive && relayTimer >= SignalRelayActiveSeconds)
                {
                    relayActive = false;
                    relayTimer = 0f;
                    EmitModifierTrigger(StoryModifier.SignalRelay, "END", "relay expired");
                }
            }
        }

        private int InsatiableHungerLineRequirement =>
            Mathf.Clamp(3 - (EffectiveDifficultyTier / 5), 1, 3);

        private float GuardPressureWindowSeconds =>
            Mathf.Max(4f, GuardPressureBaseWindowSeconds - (EffectiveDifficultyTier * 1.4f));

        private float BlindedActiveSeconds =>
            Mathf.Clamp(BlindedBaseActiveSeconds + (EffectiveDifficultyTier * 0.3f), BlindedBaseActiveSeconds, BlindedMaxActiveSeconds);

        private float SedatingSpitActiveSeconds =>
            Mathf.Clamp(SedatingSpitBaseActiveSeconds + (EffectiveDifficultyTier * 0.4f), SedatingSpitBaseActiveSeconds, SedatingSpitMaxActiveSeconds);

        private float CalculatedPlanningScoreMultiplier =>
            Mathf.Clamp(0.7f - (EffectiveDifficultyTier * 0.05f), 0.25f, 0.7f);

        private int EffectiveDifficultyTier =>
            adrenalineRushActive ? spec.DifficultyTier + AdrenalineRushDifficultyBoost : spec.DifficultyTier;

        private int TerritoryClaimLimit =>
            Mathf.Clamp(1 + (EffectiveDifficultyTier / 3), 1, 4);

        private bool HasClaimingCells =>
            Has(StoryModifier.ResilientCells) || Has(StoryModifier.TerritoryCells);

        private StoryModifier ClaimingCellsEventModifier =>
            HasDeclared(StoryModifier.ResilientCells) || Has(StoryModifier.ResilientCells)
                ? StoryModifier.ResilientCells
                : StoryModifier.TerritoryCells;

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
                StoryModifier.ResilientCells,
                StoryModifier.GhostFlicker,
                StoryModifier.AdrenalineMonitor,
            };
            return options[rng.Next(options.Length)];
        }

        private void TickBlinded(float deltaTime)
        {
            if (!Has(StoryModifier.GhostFlicker) || board.GameOver)
            {
                blindedActive = false;
                blindedActiveTimer = 0f;
                return;
            }

            if (blindedActive)
            {
                blindedActiveTimer += deltaTime;
                if (blindedActiveTimer >= blindedCurrentActiveSeconds)
                {
                    blindedActive = false;
                    blindedActiveTimer = 0f;
                    blindedCooldownTimer = 0f;
                    EmitModifierTrigger(StoryModifier.GhostFlicker, "END", "placed blocks restored visible");
                }

                return;
            }

            blindedCooldownTimer += deltaTime;
            if (blindedCooldownTimer >= BlindedCooldownSeconds)
            {
                blindedActive = true;
                blindedActiveTimer = 0f;
                blindedCooldownTimer = 0f;
                blindedCurrentActiveSeconds = BlindedActiveSeconds;
                EmitModifierTrigger(StoryModifier.GhostFlicker, "ACTIVE", $"placed blocks flicker for {Seconds(blindedCurrentActiveSeconds)}");
            }
        }

        private void TickAdrenalineRush(float deltaTime)
        {
            if (!HasDeclared(StoryModifier.AdrenalineMonitor) || board.GameOver)
            {
                adrenalineRushActive = false;
                adrenalineRushActiveTimer = 0f;
                return;
            }

            if (adrenalineRushActive)
            {
                adrenalineRushActiveTimer += deltaTime;
                if (adrenalineRushActiveTimer >= AdrenalineRushActiveSeconds)
                {
                    adrenalineRushActive = false;
                    adrenalineRushActiveTimer = 0f;
                    adrenalineRushCooldownTimer = 0f;
                    EmitModifierTrigger(StoryModifier.AdrenalineMonitor, "END", "enemy ability boost expired");
                }

                return;
            }

            adrenalineRushCooldownTimer += deltaTime;
            if (adrenalineRushCooldownTimer >= AdrenalineRushCooldownSeconds)
            {
                adrenalineRushCooldownTimer = 0f;
                adrenalineRushActiveTimer = 0f;
                adrenalineRushActive = true;
                EmitModifierTrigger(StoryModifier.AdrenalineMonitor, "ACTIVE", $"enemy abilities boosted for {Seconds(AdrenalineRushActiveSeconds)}");
            }
        }

        private void TickSedatingSpit(float deltaTime)
        {
            if (!Has(StoryModifier.SedationWindows) || board.GameOver)
            {
                sedatingSpitActive = false;
                sedatingSpitActiveTimer = 0f;
                return;
            }

            if (sedatingSpitActive)
            {
                sedatingSpitActiveTimer += deltaTime;
                if (sedatingSpitActiveTimer >= sedatingSpitCurrentActiveSeconds)
                {
                    EndSedatingSpit("END", "assist lockout expired");
                }

                return;
            }

            sedatingSpitCooldownTimer += deltaTime;
            if (sedatingSpitCooldownTimer >= SedatingSpitCooldownSeconds)
            {
                sedatingSpitCooldownTimer = 0f;
                sedatingSpitActiveTimer = 0f;
                sedatingSpitActive = true;
                sedatingSpitCurrentActiveSeconds = SedatingSpitActiveSeconds;
                EmitModifierTrigger(StoryModifier.SedationWindows, "ACTIVE", $"friendly assists reset and blocked for {Seconds(sedatingSpitCurrentActiveSeconds)}");
            }
        }

        private void EndSedatingSpit(string state, string detail)
        {
            if (!sedatingSpitActive)
            {
                return;
            }

            sedatingSpitActive = false;
            sedatingSpitActiveTimer = 0f;
            sedatingSpitCooldownTimer = 0f;
            EmitModifierTrigger(StoryModifier.SedationWindows, state, detail);
        }

        private void HandleLinesCleared(int lines)
        {
            if (lines > 0 && guardPressureRowTimers.Count > 0)
            {
                var removed = board.ClearOldestGuardPressureRow();
                guardPressureRowTimers.RemoveAt(0);
                if (removed)
                {
                    EmitModifierTrigger(StoryModifier.GuardPressure, "CLEARED", "oldest pressure row removed by line clear");
                }
            }

            if (lines > 0 && board.GetTerritoryClaimedCells().Count > 0)
            {
                if (board.ClearOldestTerritoryClaim())
                {
                    EmitModifierTrigger(ClaimingCellsEventModifier, "CLEARED", "oldest claimed block unclaimed by line clear");
                    var unlockedRows = board.ClearRowsUnlockedByTerritoryUnclaim();
                    if (unlockedRows > 0)
                    {
                        EmitModifierTrigger(ClaimingCellsEventModifier, "UNLOCKED", $"{unlockedRows} row{Plural(unlockedRows)} cleared after unclaim");
                    }
                }
            }

            if (lines > 0 && Has(StoryModifier.HungerMeter))
            {
                hungerClearedLineProgress += lines;
                var requirement = InsatiableHungerLineRequirement;
                while (hungerClearedLineProgress >= requirement)
                {
                    hungerClearedLineProgress -= requirement;
                    if (board.TryConsumeTopLayerPiece(out var pieceId, out var pieceType, out var removedCells))
                    {
                        EmitModifierTrigger(StoryModifier.HungerMeter, "TRIGGER", $"ate {pieceType} block #{pieceId} ({removedCells} cells)");
                    }
                }
            }

            if (lines > 0 && sedatingSpitActive)
            {
                EndSedatingSpit("CLEARED", "line clear ended assist lockout early");
            }
        }

        private void TickTerritoryCells(float deltaTime)
        {
            if (!HasClaimingCells || board.GameOver)
            {
                return;
            }

            if (board.GetTerritorySourceCells().Count == 0)
            {
                board.SeedTerritorySource();
                EmitModifierTrigger(ClaimingCellsEventModifier, "SETUP", "permanent claimed source seeded");
            }

            if (board.GetTerritoryClaimedBlockCount() >= TerritoryClaimLimit)
            {
                territoryTimer = 0f;
                return;
            }

            territoryTimer += deltaTime;
            while (territoryTimer >= TerritoryClaimWindowSeconds && !board.GameOver && board.GetTerritoryClaimedBlockCount() < TerritoryClaimLimit)
            {
                territoryTimer -= TerritoryClaimWindowSeconds;
                if (board.TryClaimAdjacentTerritoryCell(rng))
                {
                    var claimCount = board.GetTerritoryClaimedBlockCount();
                    EmitModifierTrigger(ClaimingCellsEventModifier, "CLAIM", $"{claimCount} claimed block{Plural(claimCount)} active");
                }
                else
                {
                    territoryTimer = 0f;
                    break;
                }
            }
        }

        private void TickGuardPressure(float deltaTime)
        {
            for (var index = 0; index < guardPressureRowTimers.Count; index += 1)
            {
                guardPressureRowTimers[index] += deltaTime;
            }

            while (guardPressureRowTimers.Count > 0 && guardPressureRowTimers[0] >= GuardPressureActiveSeconds)
            {
                var removed = board.ClearOldestGuardPressureRow();
                guardPressureRowTimers.RemoveAt(0);
                if (removed)
                {
                    EmitModifierTrigger(StoryModifier.GuardPressure, "END", "oldest pressure row expired");
                }
            }

            if (!Has(StoryModifier.GuardPressure) || board.GameOver)
            {
                return;
            }

            guardPressureTimer += deltaTime;
            var window = GuardPressureWindowSeconds;
            while (guardPressureTimer >= window && !board.GameOver)
            {
                guardPressureTimer -= window;
                if (board.AddGuardPressureRow())
                {
                    guardPressureRowTimers.Add(0f);
                    EmitModifierTrigger(StoryModifier.GuardPressure, "ACTIVE", $"pressure row added; {guardPressureRowTimers.Count} active");
                }
            }
        }

        private void HandlePieceLocked(PieceLockEvent lockEvent)
        {
            if (Has(StoryModifier.CalculatedPlanning))
            {
                if (calculatedPlanningDebuffQueued)
                {
                    calculatedPlanningDebuffQueued = false;
                    board.MarkPieceScoreDebuffed(lockEvent.PieceId, CalculatedPlanningScoreMultiplier);
                    var reductionPercent = Mathf.RoundToInt((1f - CalculatedPlanningScoreMultiplier) * 100f);
                    lastCalculatedPlanningStatus = $"score -{reductionPercent}% applied to next block";
                    EmitModifierTrigger(StoryModifier.CalculatedPlanning, "APPLIED", lastCalculatedPlanningStatus);
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
                    EmitModifierTrigger(StoryModifier.PrecisionPressure, "TRIGGER", lastPrecisionPressureStatus);
                }
                else
                {
                    lastPrecisionPressureStatus = "clean lock";
                }
            }
        }

        private void HandlePieceRotated(int rotations)
        {
            if (!Has(StoryModifier.CalculatedPlanning) || calculatedPlanningDebuffQueued || rotations <= CalculatedPlanningRotationBudget)
            {
                return;
            }

            calculatedPlanningDebuffQueued = true;
            var reductionPercent = Mathf.RoundToInt((1f - CalculatedPlanningScoreMultiplier) * 100f);
            lastCalculatedPlanningStatus = $"score -{reductionPercent}% queued after {rotations} rotations";
            EmitModifierTrigger(StoryModifier.CalculatedPlanning, "QUEUED", lastCalculatedPlanningStatus);
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

        private void EmitModifierTrigger(StoryModifier modifier, string state, string detail)
        {
            OnModifierTriggered?.Invoke(new StoryModifierTriggerEvent(
                modifier,
                ModifierLabel(modifier),
                state,
                detail
            ));
        }

        private static string Seconds(float seconds) =>
            $"{Mathf.CeilToInt(Mathf.Max(0f, seconds))}s";

        private static string Plural(int count) =>
            count == 1 ? string.Empty : "s";

        private static string ModifierLabel(StoryModifier modifier) =>
            modifier switch
            {
                StoryModifier.GuardPressure => "Guard Pressure",
                StoryModifier.TerritoryCells => "Resilient Cells",
                StoryModifier.CalculatedPlanning => "Calculated Planning",
                StoryModifier.PrecisionPressure => "Precision Pressure",
                StoryModifier.GhostFlicker => "Blinded",
                StoryModifier.EcholocationDim => "Echolocation Dim",
                StoryModifier.ResilientCells => "Resilient Cells",
                StoryModifier.MutedHints => "Muted Hints",
                StoryModifier.HungerMeter => "Insatiable Hunger",
                StoryModifier.SedationWindows => "Sedating Spit",
                StoryModifier.AdrenalineMonitor => "Adrenaline Rush",
                StoryModifier.SignalRelay => "Signal Relay",
                StoryModifier.ReducedPreview => "Reduced Preview",
                StoryModifier.NoHold => "No Hold",
                _ => modifier.ToString(),
            };
    }
}
