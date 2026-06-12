# Builds animation-contact-sheet.png for user review: per monster, the three
# featureless body frames plus a composite with the feature overlays applied,
# using the same sheet->box transform as the Unity bootstrap.
import json
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SHEET_DIR = os.path.join(ROOT, "Assets", "MonStacka", "Art", "SpriteSheets")
FRAMES_DIR = os.path.join(ROOT, "Assets", "MonStacka", "Art", "Generated", "BodyFrames")

NAMES = {"S": "zipper (red)", "Z": "grin (green)", "O": "Muwerde", "I": "Blyndoolie",
         "T": "Lysergicada", "L": "Galiffambos", "J": "Dousema"}
FRAME_BOUNDS = {
    "I": (403, 182, 151, 571), "O": (43, 314, 299, 289), "T": (622, 371, 432, 291),
    "S": (38, 5, 430, 289), "Z": (585, 19, 435, 292), "J": (49, 626, 293, 427),
    "L": (595, 622, 291, 430),
}
BASE_CELLS = {
    "I": [(2, 0), (2, 1), (2, 2), (2, 3)],
    "O": [(1, 0), (2, 0), (1, 1), (2, 1)],
    "T": [(0, 1), (1, 1), (2, 1), (1, 2)],
    "S": [(1, 0), (2, 0), (0, 1), (1, 1)],
    "Z": [(0, 0), (1, 0), (1, 1), (2, 1)],
    "J": [(1, 0), (1, 1), (0, 2), (1, 2)],
    "L": [(1, 0), (1, 1), (1, 2), (2, 2)],
}


def composite_piece(piece, features_img, manifest):
    body = Image.open(os.path.join(FRAMES_DIR, f"{piece}_frame1.png")).convert("RGBA")
    out = body.copy()
    bx, by, bw, bh = FRAME_BOUNDS[piece]
    cells = BASE_CELLS[piece]
    min_x = min(c[0] for c in cells)
    min_y = min(c[1] for c in cells)
    width_cells = max(c[0] for c in cells) - min_x + 1
    height_cells = max(c[1] for c in cells) - min_y + 1
    sx = ((width_cells * 112) - 1) / max(1, bw - 1)
    sy = ((height_cells * 112) - 1) / max(1, bh - 1)
    for entry in manifest["features"]:
        if entry["piece"] != piece:
            continue
        crop = features_img.crop((entry["ax"], entry["ay"], entry["ax"] + entry["w"], entry["ay"] + entry["h"]))
        w = max(1, round(entry["w"] * sx))
        h = max(1, round(entry["h"] * sy))
        crop = crop.resize((w, h), Image.LANCZOS)
        px = round((min_x * 112) + ((entry["x"] - bx) * sx))
        py = round((min_y * 112) + ((entry["y"] - by) * sy))
        out.alpha_composite(crop, (px, py))
    return out


def main():
    with open(os.path.join(SHEET_DIR, "feature-manifest.json"), encoding="utf-8") as fh:
        manifest = json.load(fh)
    features_img = Image.open(os.path.join(SHEET_DIR, "monster-sheet-features.png")).convert("RGBA")

    pieces = ["S", "Z", "O", "I", "T", "L", "J"]
    cell = 250
    cols = 4
    sheet = Image.new("RGB", (170 + (cols * cell), 30 + (len(pieces) * cell)), (34, 36, 48))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("arial.ttf", 17)
    except OSError:
        font = ImageFont.load_default()

    draw.text((175, 6), "body frame 1          body frame 2          body frame 3          composite (features on)",
              fill=(200, 200, 220), font=font)
    for row, piece in enumerate(pieces):
        draw.text((6, 30 + (row * cell) + (cell // 2) - 10), f"{piece} / {NAMES[piece]}", fill=(255, 255, 255), font=font)
        images = [Image.open(os.path.join(FRAMES_DIR, f"{piece}_frame{i}.png")).convert("RGBA") for i in (1, 2, 3)]
        images.append(composite_piece(piece, features_img, manifest))
        for col, img in enumerate(images):
            s = min((cell - 14) / img.width, (cell - 14) / img.height)
            scaled = img.resize((max(1, int(img.width * s)), max(1, int(img.height * s))))
            x = 170 + (col * cell) + ((cell - scaled.width) // 2)
            y = 30 + (row * cell) + ((cell - scaled.height) // 2)
            sheet.paste(scaled, (x, y), scaled)

    out_path = os.path.join(ROOT, "animation-contact-sheet.png")
    sheet.save(out_path)
    print("saved", out_path)


if __name__ == "__main__":
    main()
