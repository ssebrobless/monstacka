# MonStacka Animation Art Spec & Review Sheet

Status: PSD-DRIVEN REBUILD (round 2) - implemented from the artist layer split,
awaiting visual review of the new contact sheet.

Review artifacts:
- `animation-contact-sheet.png` - per monster: body frames 1-3 (featureless) +
  full composite with feature layers.
- In-game screenshots captured after the rebuild.

## Source of truth

`C:\Users\fishe\Downloads\monstacka\block sprites\monstacka-blocks-layers` (PSD,
1080x1080, aligned 1:1 with `monstacka-blocks-frame1.png`). The artist split
frame 1 into:
- bottom layers `Layer 43` + `Background` = fully patched featureless bodies
- 42 feature layers (eyes, mouths, noses, ears, tongue, drools, whiskers)

`tools/generate_layered_sheets.py` converts this into the Unity inputs (rerun it
whenever the PSD changes, then rerun the bootstrap):
- `Assets/MonStacka/Art/SpriteSheets/monster-sheet-body1.png` - PSD body composite
- `monster-sheet-body2/3.png` - original frames 2/3 with the dilated union
  feature mask replaced by frame-1 body pixels (silhouette preserved per frame,
  so the hand-drawn body wobble survives outside the face regions)
- `monster-sheet-features.png` - all feature layers at frame-1 positions
- `feature-manifest.json` - per-feature piece / name / motion / sheet rect

## Runtime model

- Bodies: 3-frame idle loop as before (PieceSkin), now featureless.
- Features: independent `SpriteRenderer` overlays built by `FacialPartAnimator`
  from `PieceSkinData.features` (seed rects converted to normalized box space by
  the bootstrap). Features never deform the body (handoff hard requirement).
- Previews/hold show static features; live pieces (active + locked) animate.
- Procedural motions (`FeatureMotion`): Roam, Blink (vertical squash - this is
  Muwerde's mouth-eyelid closing over the giant eye, per the artist), SquashPulse
  (nose sniffs, Blyndoolie maw gulp), Twitch (Galiffambos ears), Chatter
  (Sorrisol-grin teeth, Aggraso zipper ripple, Muwerde whiskers), Flick
  (Lysergicada tongue, top-anchored stretch), Drip (drool trails, alpha + stretch).

## Feature inventory (42 layers, all identified)

| Sheet region | Monster art | Features |
|---|---|---|
| S (red, top-left) | sawtooth zipper monster | 2 roaming eyes, sniffing nose, chattering zipper mouth |
| Z (green, top-right) | grin monster | blinking eye, chattering grin teeth, sniffing nose |
| O (yellow) | Muwerde | giant eye with teeth-mouth eyelid (Blink), whiskers |
| I (cyan column) | Blyndoolie | 12 roaming eyes, gulping maw (top), dripping drool trail |
| T (purple) | Lysergicada | 6 blinking eyes, flicking tongue, tongue drip |
| L (orange) | Galiffambos | roaming small eye, blinking blind eye, 4 twitching ears |
| J (pink) | Dousema | 2 blinking eyes, 2 sniffing noses, static sewn mouth |

Resolved from round 1: Muwerde's "face loss" in frames 2/3 was the face fading -
irrelevant now, the face is a permanent overlay; the Dousema noses and the
nose/eye ambiguities are settled by the PSD layer split.

## Bugs found and fixed during the rebuild

- `ConnectedBodyBuilder.Rotate90Clockwise` actually rotated counter-clockwise.
  J/L spawned as corner-less bars (the art of the wrong rotation, cropped), and
  I could render empty at spawn. S/Z/O (base rotation 0) and T (180 deg) masked
  it. Fixed to a true clockwise rotation; verification now asserts per-cell
  occupancy of every rotation so this cannot regress silently.
- O pieces broke when rotated in-game: O's definition is identical at every
  rotation index but the texture was still being turned (its art is off-center
  in the 4x4 box). O now always renders with 0 turns.
- Hold/Next slot frames (added in the UI cleanup pass) had an opaque fill drawn
  over the preview sprites, hiding the monsters; only their edges peeked out.
  Frames are now near-transparent and previews sit in front of the canvas plane.
- The player now supports `-monstacka-capture <path>`: it writes its own
  framebuffer to disk after the smoke pass, so visual validation never depends
  on window focus (tools/smoke-capture.ps1 wraps launch+capture+close).

## Open items for review

1. Contact sheet + in-game look: any residue of old baked-in faces in body
   frames 2/3 (dilation misses), wrong feature placement, or stacking issues.
2. Motion feel (blink rate, chatter amplitude, drip subtlety) - tune on request.
3. Feature placement on ROTATED pieces needs a human playtest check (headless
   smoke can't rotate pieces).
