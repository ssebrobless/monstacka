# Enemy Ability Scripted Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add isolated logic scripts and runtime visual checkpoint coverage for every story enemy ability.

**Architecture:** Keep `MonStackaV2Harness` as the single canonical regression entrypoint. Add direct `StoryModifierSystem` scripts for deterministic enemy ability logic, then add real scene/runtime checkpoint probes that assert HUD text, trigger cues, renderer visibility, enemy-cell visuals, and boss health/score sync.

**Tech Stack:** Unity 6000.3 editor batchmode, C# editor harness, existing MonStacka runtime scene objects.

---

## Goal

Add isolated scripted harness coverage for every current story enemy ability. The tests should disable friendly ability interference and verify each enemy ability's trigger, active behavior, visual/state cue, HUD status text, scaling, cancellation rules, natural cleanup, and event reporting.

## Ability Matrix

| Ability | Block | Trigger | Cancel | Scaling | Harness observations |
| --- | --- | --- | --- | --- | --- |
| Guard Pressure | Aggraso | Timer fills, then adds a temporary full bottom row | Player line clear removes only the oldest active pressure row; natural 6s expiry also removes one | Timer shrinks with tier; high tier can stack rows | Row count, oldest-first cleanup, status `[TIMER]`/`[ACTIVE]`, `GUARD` chip, trigger events |
| Calculated Prediction | Muwerde | More than 4 rotations queues a debuff for the next locked block | Not cancelable after queued/applied | Score multiplier falls from 70% toward 25% | Rotation threshold, hold/swap preservation, one queued event, debuffed piece scoring, status text |
| Vision Loss | Galiffambos | 12s cooldown | Not cancelable | Active duration grows from 4s to 7s | `LockedPiecesVisible` flicker every 0.5s, status `[TIMER]`/`[ACTIVE]`, visible cleanup on end |
| Resilient Cells | Dousema | Match start seeds permanent source; 10s timer claims adjacent locked block | Player line clear removes one temporary claim oldest-first; source remains | Claim cap grows from 1 to 4 blocks | Permanent source, temporary whole-block claims, claim cap, blocked row clear, unlocked-row cascade guard, status text |
| Insatiable Hunger | Sorrisol | Player-cleared line counter reaches requirement | Instant effect; no cancel window | Requirement shrinks from 3 lines to 1 line | Progress status/chip, whole top-layer piece consumption, carryover progress, trigger event |
| Sedating Spit | Lysergicada | 15s cooldown | Player line clear ends active lockout early; natural expiry also ends | Duration grows from 4s to 8s | Active assist suppression, reset of assist charge/progress in isolated assist system, status `[TIMER]`/`[ACTIVE]`, cleanup events |
| Adrenaline Rush | Blyndoolie | 20s cooldown | Not cancelable | Fixed +6 effective enemy difficulty for 11s | Active/end lifecycle, status/chip, boosted Hunger/Guard scaling during active, post-end unboosted behavior |

## Runtime Visual Checkpoint Layer

The logic matrix proves state transitions. The runtime visual checkpoint layer should prove the player-facing scene reflects those transitions:

| Runtime checkpoint | What it proves | Harness observation |
| --- | --- | --- |
| Guard Pressure + Resilient Cells | Enemy rows/cells render with authored enemy visuals above board art | active `GarbageCell*` renderers exist, have non-white sprites, red enemy color, sorting order >= 18 |
| Vision Loss | Placed/locked blocks flicker invisible and restore visible | private `stackRoot` active state changes false/true as `LockedPiecesVisible` changes |
| Sedating Spit | Assist lockout appears in the right HUD and trigger cue | `StoryEnemyStatus` includes `Sedating Spit [ACTIVE]`, trigger cue names the ability |
| Adrenaline Rush | Boost state appears in HUD and clears from the active chip/status | `StoryEnemyStatus` includes `Adrenaline Rush [ACTIVE]`, status chip includes `ADRENALINE RUSH`, then clears after duration |
| Boss HUD sync | Score damage updates boss health fill during enemy activity | `BossHealthBar/Fill.anchorMax.x` matches score-derived HP percent |

## Implementation Steps

- [x] Add `enemy ability scripted scenario matrix` to `MonStackaV2Harness.BuildScenarios()`.
- [x] Add one scripted verifier per ability plus small helpers for status text, status chips, events, and line/claim setup.
- [x] Keep isolated scripted tests enemy-only by using direct `StoryModifierSystem`/`BoardState` scripts instead of runtime story mode assists.
- [x] Run the harness and inspect the latest report.
- [x] Add `enemy ability runtime visual checkpoints` to `MonStackaV2Harness.BuildScenarios()`.
- [x] Add runtime checkpoint helpers for private scene fields, HUD text, stack visibility, enemy renderers, trigger cue text, and boss HP fill.
- [x] Add runtime visual scenarios for Guard Pressure, Resilient Cells, Vision Loss, Sedating Spit, Adrenaline Rush, and boss HP/score sync.
- [x] Discuss with the user before running the full harness again.
