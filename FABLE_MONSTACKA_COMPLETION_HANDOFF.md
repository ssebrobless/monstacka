# Fable Handoff: MonStacka Completion Plan

## Mission

Finish MonStacka as a polished Unity game while preserving the working TypeScript/Tauri version as the behavioral reference. Complete the three main modes, add story mode, rebuild/improve monster animations, clean the in-match UI, and optimize aggressively so active play has no visible lag.

Do not treat editor verification as sufficient. A build is not complete until the Windows player launches and passes live smoke tests.

## Current Source Of Truth

- Stable gameplay reference: `repo_review/enhanced`
- Unity recovery target: `repo_review/MonStacka-v2`
- Story draft and chapter outline: `C:\Users\fishe\Downloads\monstacka\Story Script\story-dialogue -script.txt`
- Existing recovery docs:
  - `repo_review/MONSTACKA_UNIFIED_RECOVERY_ISSUES.md`
  - `repo_review/MONSTACKA_RECOVERY_IMPLEMENTATION_PLAN.md`
  - `repo_review/MONSTACKA_V1_REFERENCE_CHECKLIST.md`
  - `repo_review/MONSTACKA_ENGINE_VISUAL_PORT_PLAN.md`

## Hard Requirements

1. Keep gameplay deterministic and grid-based. Visual deformation must never affect board occupancy.
2. Use the v1 Tauri implementation as the behavior reference for:
   - O.G.B.M.
   - X(4)-LINES
   - You... Suck? / training
   - controls
   - controller defaults
   - hold, queue, line clears, scoring, gravity, lock delay
3. Finish Unity live gameplay, not just editor tests.
4. Rebuild the visual/animation pipeline from the best available source art.
5. Do not let ripple, eyes, mouths, tongues, ears, or other facial features distort the body sprite.
6. Story mode must get progressively harder.
7. Story mode must use explicit chapter metadata. Do not infer chapter-piece mapping from prose unless metadata is missing and the chapter is marked `needs_user_mapping`.
8. Optimize for low runtime churn. Avoid destroy/recreate loops during gameplay.

## Current Art Reality

Separate layered original files were not found in the repo. The best available art sources are:

- `repo_review/MonStacka-v2/Assets/MonStacka/Art/SpriteSheets/monster-sheet.png`
- `repo_review/MonStacka-v2/Assets/MonStacka/Art/SpriteSheets/monster-sheet-frame2.png`
- `repo_review/MonStacka-v2/Assets/MonStacka/Art/SpriteSheets/monster-sheet-frame3.png`
- `repo_review/enhanced/src/monsterSkin.ts`
- `repo_review/enhanced/src/ui/homeMenu.ts`

If the user has earlier/layered original drawings elsewhere, request/import them before rebuilding animation. If not, rebuild from the three sprite sheets and create reviewable per-piece body/feature masks.

## Monster / Piece Mapping

Use this mapping everywhere:

- `Z`: Aggraso
- `O`: Muwerde
- `L`: Galiffambos
- `J`: Dousema
- `S`: Sorrisol
- `T`: Lysergicada
- `I`: Blyndoolie

Existing code sometimes spells Blyndoolie as `BLYNDOOLIE`. Preserve the in-game display spelling unless the user asks to standardize it.

## Story Direction

Fable can finish the story script if it follows this structure and preserves the existing voice:

- Tone: horror-comedy, crude player reactions, sterile corporate PA language, grotesque biological science, gradual descent from absurd tour to existential body horror.
- Existing style: PA System is upbeat and euphemistic; Player is confused, hostile, profane, and increasingly disturbed; Narrator is physical/sensory and claustrophobic.
- Do not rewrite completed dialogue unless needed for continuity.
- Expand unfinished chapters by matching the cadence and character voices in the existing script.

### Chapter 1: Aggraso / Z

Theme: first stable organism, guard dog, beginning of the team's shared dream.

- `1.1 "A Yucky Building"`: intro challenge and basic rules.
- `1.2 "Guard Dog"`: Aggraso introduced as first stable organism.
- `1.3 "Lock the Door Behind You"`: scientists begin locking down process/facility boundaries, early team confidence.
- `1.4 "A Shared Dream"`: scientists become a real team, excited by shared ambition.

Modifier direction: Z spawn bias, guard/territory pressure.

### Chapter 2: Muwerde / O

Theme: intelligence, neural capability, perseverance, doubt.

- `2.1 "Unlocking Intelligence"`: attempts to increase neural capability.
- `2.2 "Trial and Error"`: repeated failures around intelligence and face/brain tradeoffs.
- `2.3 "Thinking Outside the Box"`: Muwerde reduces facial complexity to gain brain space.
- `2.4 "Weathering the Storm"`: team starts to doubt, struggles financially/emotionally/scientifically, but pushes forward.

Muwerde concept:
- Smartest of all blocks.
- Second stable organism.
- Required staples to keep facial structure simple.
- Chose/reduced facial features to create additional brain space.

Modifier direction: O spawn bias, planning/preview/precision pressure.

### Chapter 3: Galiffambos / L, then Dousema / J

Theme: senses, cost, sacrifice, spare parts, resilience.

`3.1 "Development of Senses"` and `3.2 "The Cost of Success"` focus on Galiffambos.

Galiffambos concept:
- Blind in the larger eye.
- Used for auditory extremity testing.
- First experiment created with more than one eye.
- Developed echolocation due to multiple ears.

Modifier direction: L spawn bias, ghost/visibility/sound/perception mechanics.

`3.3 "Preparing for the Worst"` and `3.4 "Nose to the Grindstone"` focus on Dousema and the main scientist crossing lines others cannot follow.

Dousema concept:
- Mouth sewn shut after teeth and tongue were removed.
- Has two noses.
- Each nose originally had a pair of eyes; those eyes were removed.
- Created as spare parts for others.
- Extremely resilient body survived despite that.

Modifier direction: J spawn bias, resilience/repair/muted hints/awkward recovery.

### Chapter 4: Sorrisol + Lysergicada, then Blyndoolie / I

Theme: control, warped teamwork, disposal, monitoring, chemical control.

`4.1 "Teamwork"`, `4.2 "Waste Management"`, and `4.3 "Identifying a Problem"` focus on Sorrisol and Lysergicada.

Sorrisol concept:
- Huge mouth.
- Took extra teeth from Dousema.
- Teeth chatter from hunger.
- Nose is too flat to function; strange snorting sound.
- Living garbage disposal.
- Chews/processes faulty experiments in its body.

Lysergicada concept:
- Has brain implant to receive signals from Blyndoolie.
- Naturally generates LSD-like chemical from glands.
- Licks targets to subdue them.
- Lower brain function due to natural LSD generation.
- Blyndoolie can alter Lysergicada's norepinephrine and D2 dopamine levels.

Modifier direction: S/T spawn bias, hunger, garbage pressure, control disruption, sedation windows.

`4.4 "Vigilance is Key"` and `4.5 "Knowing When to Let Go"` focus on Blyndoolie.

Blyndoolie concept:
- Many eyes that cannot shut.
- Saliva from mouth above trickles down to hydrate eyes.
- Brain implant monitors adrenaline spikes.
- Relays signal when threshold is exceeded.
- Created to monitor and signal other blocks for assistance.

Modifier direction: I spawn bias, danger monitoring, adrenaline spikes, signal relay, escalation.

### Chapter 5: Final Hard Missions / No Spawn Bias

Theme: self-experimentation, destruction, integration with building, living walls.

- `5.1 "Tethered Minds"`: scientist tries to reconnect his mind as it breaks apart; starts considering self-experimentation and giving up humanity.
- `5.2 "Destruction"`: scientist mutilates and rebuilds himself, replaces limbs with experiment biomatter, implants chips, experiences seizures/shock, fails against human limits.
- `5.3 "Creation"`: scientist realizes he must destroy everything to become complete; fully integrates into the building. The building was alive because he became the living walls controlling it and generating experiments.

No bonus piece spawns in Chapter 5. These should be very difficult remix missions combining previous mechanics.

## Story Mode Chapter Spec

Implement story mode with explicit data, not hardcoded one-off branches.

Suggested data shape:

```text
StoryChapterSpec
- id: string
- title: string
- act: number
- sequence: number
- focusedPieces: PieceType[]
- spawnBias: Record<PieceType, number>
- difficultyTier: number
- introDialogue: DialogueLine[]
- preMatchDialogue: DialogueLine[]
- postMatchDialogue: DialogueLine[]
- objective: StoryObjective
- modifiers: StoryModifier[]
- unlocksNext: string | null
- needsUserMapping: boolean
```

If a chapter has incomplete prose, write a draft in the existing tone and clearly mark it as generated draft text in data comments or editor notes.

## Story Mode Gameplay

Story mode should get harder through:

- higher line goals
- faster gravity
- shorter lock delay
- reduced preview/hold availability in late missions
- garbage/nuisance cells
- chapter-specific modifiers
- Chapter 5 remix combinations

Do not make early chapters too hard. Story mode is the onboarding path for mechanics.

## Chapter Modifiers

Use these chapter modifiers:

- Aggraso / Z: increased Z spawns, territory pressure, faster lock after ground contact.
- Muwerde / O: increased O spawns, longer planning info but stricter precision or rotation constraints.
- Galiffambos / L: increased L spawns, ghost/visibility flicker, sound/perception challenge.
- Dousema / J: increased J spawns, resilient cells, repairs, muted hints.
- Sorrisol / S: increased S spawns, hunger meter, garbage rises if player goes too long without clearing.
- Lysergicada / T: increased T spawns, sedate/control effects, temporary slowdowns or warning-based control disruptions.
- Blyndoolie / I: increased I spawns, danger/adrenaline monitoring, signal relay that can activate earlier mechanics.
- Chapter 5: no spawn bias, combine mechanics at high difficulty.

## Player Assist System

Add a reusable `AssistEffectSystem`.

Core rule:

- When a player places a piece that came from hold, increment `heldPiecesPlayed`.
- Every third held piece placed triggers that piece's assist immediately.
- This should create a point-farming strategy around planned hold usage.

Scope:

- Enabled in O.G.B.M.
- Enabled in X(4)-LINES if it does not break sprint integrity; if enabled, separate sprint records with assists from pure sprint records.
- Enabled in Story Mode.
- Disabled by default in Training unless a practice toggle exists.

Assist effects:

- `Z / Aggraso`: Guard Break. Removes danger cells or garbage near the bottom/edges.
- `O / Muwerde`: Calculation. Improves preview/planning and rewards clean placements.
- `L / Galiffambos`: Echo Guide. Enhances ghost/safe-placement guidance.
- `J / Dousema`: Stitch. Repairs a hole or awkward cell.
- `S / Sorrisol`: Digest. Eats nuisance/garbage cells and awards points.
- `T / Lysergicada`: Sedate. Slows gravity or extends lock delay for a short window.
- `I / Blyndoolie`: Alert. Gives high-danger score boost and warning/signal relay.

Scoring:

- Base assist trigger bonus.
- Bonus for line clears during assist.
- Bonus for danger saves.
- Bonus for using the assist to enable a clear within the next few pieces.
- Combo bonus for repeated planned third-hold triggers.

Avoid making assists stronger than good Tetris play. They should reward planning, not replace skill.

## Main Modes To Finish

### O.G.B.M.

Endless score chase until top-out.

Requirements:
- complete scoring
- hold and assist effects
- pause/resume/retry/home
- leaderboards
- stable HUD
- no visible lag

### X(4)-LINES

40-line sprint.

Requirements:
- line target
- timer
- clear completion state
- sprint records
- clear separation if assists modify fairness

### You... Suck? / Training

Training/finesse mode.

Requirements:
- empty board reset behavior
- show/redo feedback
- optional assist practice toggle only if clearly labeled
- no leaderboard pollution

### Story Mode

Chapter progression with dialogue, modifiers, increasing difficulty, and unlocks.

Requirements:
- chapter select
- dialogue before/after matches
- focused piece spawn bias
- modifier intro/tutorial text
- completion persistence
- Chapter 5 no-bias hard missions

## Animation Rebuild Plan

Goal: better animation from source art without broken-up visual artifacts.

Process:

1. Build a per-piece art spec.
2. Generate body mask and feature masks separately.
3. Mark uncertain regions for user review.
4. Keep body, eyes, mouth, teeth, tongue, ears, and appendages as separate logical layers.
5. Animate layers with authored motion:
   - body idle pulse
   - eyes roam/blink
   - mouths/teeth/tongues animate without stretching body
   - ears twitch
   - saliva/drip effects for Blyndoolie
   - Sorrisol teeth chatter/snort
   - Lysergicada tongue/chemical/subdue effect
6. Use original sprite sheets only when layered originals are unavailable.
7. Produce a contact sheet for each animation pass and require user-visible review.

Do not use cell-by-cell sprite chunking as the primary visual method.

## In-Match UI Cleanup

The current in-match UI is ugly and must be cleaned up.

Requirements:

- fixed 16:9 artboard scaling
- stable left panel
- clear score/lines/time/goal hierarchy
- hold and next queue aligned and readable
- next queue not cramped
- controls/help accessible but not cluttered
- story modifiers shown as compact status chips
- assist counter visible: e.g. `Held Assist: 2/3`
- active assist state visible with countdown/effect label

## Optimization Plan

Remove lag by targeting runtime churn first.

Requirements:

- no full stack rebuild every frame
- no unnecessary destroy/recreate on movement
- pool active/locked/preview piece views
- cache generated sprites/textures/masks
- cache dominant colors
- update only changed board cells/pieces
- separate gameplay tick from visual animation tick
- profile spawn, lock, line clear, hold, next queue update, story modifier update, and assist trigger

Performance gates:

- no hitch on spawn
- no hitch on hard drop
- no hitch on lock
- no hitch on line clear
- no hitch on next queue/hold update
- stable frame pacing with late-game stack and active animations

## Verification Gates

Do not stop at compile success.

Required checks:

1. v1 reference:
   - `npm test`
   - `npm run build`
2. Unity editor:
   - no compile errors
   - `MonStacka > Verify Vertical Slice`
3. Unity player:
   - build Windows player
   - launch from `Builds/Windows`
   - verify pieces appear in live gameplay
   - verify all three main modes start
   - verify Story Mode chapter 1 starts
   - verify hold/assist trigger
   - verify no obvious lag
   - verify resize/maximize/restore

If verification passes but the built player shows no pieces, verification is insufficient and must be expanded before continuing.

## Known Recent Runtime Failure

The Unity verifier passed, but the launched player showed no pieces generating. This must be treated as a live-player blocker.

First things to inspect:

- active piece spawn state in `BoardState`
- `GameManager.RebuildActivePieceView`
- skin lookup wiring in scene
- `pieceSkins` array assignment
- mask/camera/layer visibility
- board root transforms
- active piece render path after connected-body changes
- whether sprites are created but hidden/clipped

Add a verifier or runtime smoke path that fails if a live game starts with an active piece but no visible active piece renderer.

## Suggested Work Order For Fable

1. Fix live Unity player so pieces visibly spawn and render.
2. Re-run editor verifier and Windows player smoke test.
3. Finish core modes: O.G.B.M., X(4)-LINES, Training.
4. Add AssistEffectSystem.
5. Clean in-match UI.
6. Add Story Mode data model and chapter progression.
7. Draft unfinished story dialogue from chapter outline.
8. Implement chapter modifiers and spawn bias.
9. Rebuild animation pipeline from source art/spec masks.
10. Optimize runtime churn and profile hot paths.
11. Final full regression pass.

## Final Acceptance Criteria

The game is complete when:

- O.G.B.M. is fun, stable, scored, and leaderboard-ready.
- X(4)-LINES is playable and records completion.
- Training works with show/redo behavior.
- Story Mode has all chapters represented, with existing/drafted dialogue, modifiers, increasing difficulty, and completion flow.
- Every monster has an assist effect.
- Hold-trigger assist point farming works and is balanced.
- In-match UI looks intentional and readable.
- Monster animations are improved and preserve body/feature identity.
- No gameplay lag is visible during normal play.
- Windows build launches cleanly and passes live smoke tests.
