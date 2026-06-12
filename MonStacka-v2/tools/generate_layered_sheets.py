# Generates layered animation source sheets from the artist PSD.
#
# Input:  C:/Users/fishe/Downloads/monstacka/block sprites/monstacka-blocks-layers (PSD)
#         + the three original frame sheets in the same folder.
# Output: Assets/MonStacka/Art/SpriteSheets/monster-sheet-body{1,2,3}.png  (featureless bodies)
#         Assets/MonStacka/Art/SpriteSheets/monster-sheet-features.png     (features only, frame-1 positions)
#         Assets/MonStacka/Art/SpriteSheets/feature-manifest.json          (per-piece feature rects + motion)
#
# The PSD is layer-split by the artist: bottom layers "Layer 43" + "Background" are the
# patched featureless bodies; every other layer is one facial feature. Frames 2/3 bodies
# are derived from the original sheets by replacing the (dilated) union feature mask with
# frame-1 body pixels, clipped to each frame's own silhouette so outlines stay hand-drawn.
#
# Requires: pip install psd-tools pillow numpy
import json
import os

import numpy as np
from PIL import Image, ImageFilter
from psd_tools import PSDImage

ART_DIR = r"C:\Users\fishe\Downloads\monstacka\block sprites"
PSD_PATH = os.path.join(ART_DIR, "monstacka-blocks-layers")
FRAME_PATHS = [os.path.join(ART_DIR, f"monstacka-blocks-frame{i}.png") for i in (1, 2, 3)]
OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       "MonStacka-v2" if False else "", "Assets", "MonStacka", "Art", "SpriteSheets")
OUT_DIR = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "MonStacka", "Art", "SpriteSheets"))

BODY_LAYERS = {"Layer 43", "Background"}
SKIP_LAYERS = {"Layer 44"}  # empty in the PSD
MASK_DILATE_PX = 12
ALPHA_THRESHOLD = 10

# Mirrors PieceDefinitions.GetFrameBounds(type, 0) - used only to assign layers to pieces.
FRAME_BOUNDS = {
    "I": (403, 182, 151, 571),
    "O": (43, 314, 299, 289),
    "T": (622, 371, 432, 291),
    "S": (38, 5, 430, 289),
    "Z": (585, 19, 435, 292),
    "J": (49, 626, 293, 427),
    "L": (595, 622, 291, 430),
}

# PSD layer name -> (piece, feature name, motion). Motion names match the Unity FeatureMotion enum.
# Piece is explicit because the sheet regions overlap (the I column crosses the S bbox).
FEATURE_TABLE = {
    "Layer 1": ("S", "eye_right", "Roam"),
    "Layer 2": ("S", "nose", "SquashPulse"),
    "Layer 3": ("S", "eye_left", "Roam"),
    "Layer 4": ("S", "zipper_mouth", "Chatter"),
    "Layer 5": ("Z", "eye", "Blink"),
    "Layer 6": ("Z", "grin_teeth", "Chatter"),
    "Layer 7": ("Z", "nose", "SquashPulse"),
    **{f"Layer {i}": ("I", f"eye_{i - 8}", "Roam") for i in range(9, 21)},
    "Layer 21": ("L", "small_eye", "Roam"),
    "Layer 22": ("L", "ear_top", "Twitch"),
    "Layer 23": ("L", "blind_eye", "Blink"),
    "Layer 24": ("L", "ear_mid", "Twitch"),
    "Layer 25": ("L", "ear_low", "Twitch"),
    "Layer 26": ("L", "ear_foot", "Twitch"),
    **{f"Layer {i}": ("T", f"eye_{i - 26}", "Blink") for i in range(27, 33)},
    "Layer 33": ("T", "tongue", "Flick"),
    "Layer 34": ("I", "maw", "SquashPulse"),
    "Layer 35": ("J", "sewn_mouth", "Static"),
    "Layer 36": ("J", "nose_upper", "SquashPulse"),
    "Layer 37": ("J", "nose_lower", "SquashPulse"),
    "Layer 38": ("J", "eye_right", "Blink"),
    "Layer 39": ("J", "eye_left", "Blink"),
    "Layer 40": ("O", "eye_mouthlid", "Blink"),
    "Layer 45": ("T", "tongue_drip", "Drip"),
    "Layer 46": ("O", "whiskers", "Chatter"),
    "Layer 47": ("I", "drool", "Drip"),
}


def clamp_to_piece(piece, rect, tolerance=6):
    x, y, w, h = FRAME_BOUNDS[piece]
    overhang = max(x - rect[0], y - rect[1], rect[2] - (x + w), rect[3] - (y + h))
    if overhang > tolerance:
        raise ValueError(f"feature rect {rect} outside declared piece {piece} bounds {FRAME_BOUNDS[piece]}")
    return (max(rect[0], x), max(rect[1], y), min(rect[2], x + w), min(rect[3], y + h))


def tight_bbox(arr_alpha, bbox):
    x0, y0, x1, y1 = bbox
    region = arr_alpha[y0:y1, x0:x1]
    ys, xs = np.where(region > ALPHA_THRESHOLD)
    if len(xs) == 0:
        return None
    return (x0 + int(xs.min()), y0 + int(ys.min()), x0 + int(xs.max()) + 1, y0 + int(ys.max()) + 1)


def nearest_fill(arr, iterations=16):
    # Propagate opaque body pixels outward so edge lookups just past the outline
    # find a sensible body color (covers feature bulges up to `iterations` px).
    out = arr.copy()
    filled = out[:, :, 3] > 0
    for _ in range(iterations):
        if filled.all():
            break
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            shifted = np.roll(out, (dy, dx), axis=(0, 1))
            shifted_filled = np.roll(filled, (dy, dx), axis=(0, 1))
            take = (~filled) & shifted_filled
            out[take] = shifted[take]
            filled |= take
    return out


def main():
    psd = PSDImage.open(PSD_PATH)
    size = psd.size
    assert size == (1080, 1080), size

    body1 = Image.new("RGBA", size, (0, 0, 0, 0))
    union_mask = Image.new("L", size, 0)
    entries = []

    for layer in psd:  # bottom -> top
        if layer.name in SKIP_LAYERS:
            continue
        img = layer.composite()
        if img is None:
            continue
        canvas = Image.new("RGBA", size, (0, 0, 0, 0))
        canvas.paste(img, (layer.bbox[0], layer.bbox[1]), img)
        if layer.name in BODY_LAYERS:
            body1 = Image.alpha_composite(body1, canvas)
            continue
        if layer.name not in FEATURE_TABLE:
            raise ValueError(f"unmapped PSD layer: {layer.name!r}")
        alpha = np.array(canvas)[:, :, 3]
        union_mask = Image.fromarray(np.maximum(np.array(union_mask), (alpha > ALPHA_THRESHOLD).astype(np.uint8) * 255))
        rect = tight_bbox(alpha, (0, 0, size[0], size[1]))
        piece, name, motion = FEATURE_TABLE[layer.name]
        rect = clamp_to_piece(piece, rect)
        # PSD bottom->top order is preserved by list order so overlays stack correctly
        # (e.g. the tongue drip renders above the tongue).
        entries.append({
            "piece": piece,
            "name": name,
            "motion": motion,
            "psdLayer": layer.name,
            "x": rect[0],
            "y": rect[1],
            "w": rect[2] - rect[0],
            "h": rect[3] - rect[1],
            # crop of THIS layer only - feature bboxes overlap each other on the
            # sheet, so the runtime atlas must not be a flat composite
            "_img": canvas.crop(rect),
        })

    # Dilate the union feature mask so hand-drawn feature wobble in frames 2/3 is covered.
    k = (MASK_DILATE_PX * 2) + 1
    dilated = np.array(union_mask.filter(ImageFilter.MaxFilter(k))) > 0

    body1_arr = np.array(body1)
    filled_body1 = nearest_fill(body1_arr)
    outputs = {"monster-sheet-body1.png": body1}
    for frame_idx in (1, 2):
        frame = np.array(Image.open(FRAME_PATHS[frame_idx]).convert("RGBA"))
        out = frame.copy()
        # Replace feature regions with frame-1 body pixels while keeping this frame's
        # own silhouette (its alpha). Features that bulge past the frame-1 body outline
        # (e.g. Blyndoolie edge eyes in frames 2/3) take the nearest body color instead
        # of leaving feature residue.
        replace = dilated & (frame[:, :, 3] > 0) & (filled_body1[:, :, 3] > 0)
        out[replace] = filled_body1[replace]
        out[:, :, 3][replace] = frame[:, :, 3][replace]
        outputs[f"monster-sheet-body{frame_idx + 1}.png"] = Image.fromarray(out)

    os.makedirs(OUT_DIR, exist_ok=True)
    for fname, img in outputs.items():
        img.save(os.path.join(OUT_DIR, fname))
    # Shelf-pack every feature crop into an atlas so runtime rect crops can never
    # bleed a neighboring feature (the source bboxes overlap on the sheet).
    pad = 2
    atlas_width = 1024
    shelf_x, shelf_y, shelf_h = pad, pad, 0
    for entry in sorted(entries, key=lambda e: -e["h"]):
        w, h = entry["w"], entry["h"]
        if shelf_x + w + pad > atlas_width:
            shelf_y += shelf_h + pad
            shelf_x, shelf_h = pad, 0
        entry["ax"], entry["ay"] = shelf_x, shelf_y
        shelf_x += w + pad
        shelf_h = max(shelf_h, h)
    atlas_height = 1 << (shelf_y + shelf_h + pad - 1).bit_length()
    atlas = Image.new("RGBA", (atlas_width, atlas_height), (0, 0, 0, 0))
    for entry in entries:
        atlas.paste(entry.pop("_img"), (entry["ax"], entry["ay"]))
    atlas.save(os.path.join(OUT_DIR, "monster-sheet-features.png"))

    with open(os.path.join(OUT_DIR, "feature-manifest.json"), "w", encoding="utf-8") as fh:
        json.dump({"features": entries}, fh, indent=2)

    print(f"wrote 4 sheets + manifest ({len(entries)} features) to {OUT_DIR}")
    for piece in sorted(FRAME_BOUNDS):
        items = [e for e in entries if e["piece"] == piece]
        print(f"  {piece}: {len(items)} features: {', '.join(i['name'] for i in items)}")


if __name__ == "__main__":
    main()
