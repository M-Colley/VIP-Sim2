"""Measure and tile the frames written by VipSim.EditorTools.VipSimEffectGallery.

Usage: python tools/effect_gallery.py <gallery-out-dir> [thumb-width]   (needs numpy and Pillow)

Each raw RGBA frame is composited the way the desktop shows the overlay. DWM blends the
window's backbuffer as premultiplied alpha, displayed = rgb + (1 - a) * behind, and behind
the overlay is the real window (where the test image stands in for a captured window) or
the desktop (a checkerboard here). So a pixel inside the window with alpha below 1 lets the
undegraded window show through, and a pixel outside it with alpha 0 but non-zero rgb adds
light to the desktop.

Writes metrics.csv and one contact sheet per pass (sheet_default.png, sheet_sev050.png,
sheet_sev100.png, sheet_off.png).
"""

import csv
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

out = Path(sys.argv[1])
thumb_w = int(sys.argv[2]) if len(sys.argv) > 2 else 480
meta = json.loads((out / "gallery.json").read_text())
w, h = meta["width"], meta["height"]
sx, sy, sw, sh = meta["stimulus"]  # Unity rect: origin bottom-left
with open(out / "gallery.csv", encoding="utf-8") as f:
    rows = list(csv.DictReader(f))


def load(name):
    # PNG rows are top-down; flip so row 0 is the bottom, matching Unity's rect.
    return np.asarray(Image.open(out / f"{name}.png").convert("RGBA"), dtype=np.float32)[::-1] / 255.0


inside = np.zeros((h, w), bool)
inside[sy + 6 : sy + sh - 6, sx + 6 : sx + sw - 6] = True
window = np.zeros((h, w), bool)
window[sy : sy + sh, sx : sx + sw] = True
outside = np.ones((h, w), bool)
outside[max(0, sy - 6) : sy + sh + 6, max(0, sx - 6) : sx + sw + 6] = False

base = load("00_baseline")
yy, xx = np.mgrid[0:h, 0:w]
checker = np.where(((xx // 24 + yy // 24) % 2) == 0, 0.35, 0.63)[..., None].repeat(3, axis=2)
behind = np.where(window[..., None], base[..., :3], checker)


def composite(img):
    return np.clip(img[..., :3] + (1 - img[..., 3:4]) * behind, 0, 1)


shown_base = composite(base)
metrics = []
for r in rows:
    img = load(r["image"])
    shown = composite(img)
    a = img[..., 3]
    m = {
        "image": r["image"],
        "enabled": r["enabled"],
        # What the viewer sees, against what they saw with nothing enabled (0..255).
        "shown_diff_in": 255 * np.abs(shown - shown_base)[inside].mean(),
        # Share of the window where the real window shows through by more than 10%.
        "see_through_in": (a[inside] < 0.9).mean(),
        "alpha_in_min": 255 * a[inside].min(),
        # Light added to or covering the desktop around the window.
        "shown_diff_out": 255 * np.abs(shown - shown_base)[outside].mean(),
        "alpha_out_max": 255 * a[outside].max(),
        "camera_euler": r["camera_euler"],
        "errors": r["errors"],
    }
    metrics.append(m)

with open(out / "metrics.csv", "w", newline="", encoding="utf-8") as f:
    wr = csv.DictWriter(f, fieldnames=list(metrics[0]))
    wr.writeheader()
    for m in metrics:
        wr.writerow({k: (f"{v:.3f}" if isinstance(v, float) else v) for k, v in m.items()})

font = ImageFont.load_default(size=15)
th = round(thumb_w * h / w)
lab = 38
cols = 4
passes = {
    "default": lambda n: n.startswith("default_") and not n.endswith("__off"),
    "sev050": lambda n: n.startswith("sev050_") and not n.endswith("__off"),
    "sev100": lambda n: n.startswith("sev100_") and not n.endswith("__off"),
    "off": lambda n: n.endswith("__off"),
}
by_name = {m["image"]: m for m in metrics}
for key, pick in passes.items():
    sel = ["00_baseline"] + [m["image"] for m in metrics if pick(m["image"])]
    sheet = Image.new("RGB", (cols * thumb_w, -(-len(sel) // cols) * (th + lab)), "white")
    d = ImageDraw.Draw(sheet)
    for k, name in enumerate(sel):
        m = by_name[name]
        x, y = (k % cols) * thumb_w, (k // cols) * (th + lab)
        shown = (composite(load(name))[::-1] * 255).astype(np.uint8)
        sheet.paste(Image.fromarray(shown).resize((thumb_w, th), Image.LANCZOS), (x, y + lab))
        bad = m["errors"] or m["see_through_in"] > 0.01 or (key == "off" and m["shown_diff_in"] > 0.5)
        d.text(
            (x + 4, y + 2), name.split("_", 1)[-1] if name != "00_baseline" else name, fill="black", font=font
        )
        d.text(
            (x + 4, y + 19),
            f"diff {m['shown_diff_in']:.1f}  see-through {100 * m['see_through_in']:.0f}%  out {m['shown_diff_out']:.1f}"
            + ("  ERR" if m["errors"] else ""),
            fill="red" if bad else "dimgray",
            font=font,
        )
    sheet.save(out / f"sheet_{key}.png")
    print(out / f"sheet_{key}.png", len(sel))
