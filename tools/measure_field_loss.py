"""Check that the field-loss radius is in degrees of visual angle.

Usage: python tools/measure_field_loss.py <gallery-out-dir>   (needs numpy and Pillow)

Reads the frames VipSimEffectGallery writes with -galleryFieldLoss: a one-pixel checkerboard
behind myFieldLoss (central loss) and myFieldLossInverted (peripheral loss) at each radius in
fieldloss.json, with the gaze at the centre of the screen.

The shaders blur by sampling a coarser mip level: level = max(0, w * MaxLOD - 1), where w is
the overlay's weight and MaxLOD = 2 + floor(ln(max(width, height))) (myFieldLoss.LevelsOfBlur).
A one-pixel checkerboard has full contrast at level 0 and none from level 1, so its contrast
halves where the level is 0.5, that is where w = 1.5 / MaxLOD. The overlay's radial profile
gives the radius at which w takes that value, as a multiple of the half-weight radius, which
radius_deg sets. This script predicts that radius in pixels from the texture and measures it
in the frames along the horizontal and the vertical axis through the gaze.

Exits 1 if a measured radius is more than 5% from the prediction, or if the horizontal and
vertical radii differ by more than 3% (the patch must be round).
"""

import itertools
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

out = Path(sys.argv[1])
repo = Path(__file__).resolve().parents[1]
meta = json.loads((out / "gallery.json").read_text())
fl = json.loads((out / "fieldloss.json").read_text())
w, h = meta["width"], meta["height"]
ppd, half = fl["pixels_per_degree"], fl["overlay_half_radius"]
max_lod = 2 + math.floor(math.log(max(w, h)))
level_w = 1.5 / max_lod  # overlay weight at mip level 0.5

# Radial profile of the overlay, as the shader reads it: linear values, Rec. 601 weights.
srgb = (
    np.asarray(
        Image.open(repo / "windows/Assets/Resources/macular-degeneration.jpg").convert(
            "RGB"
        ),
        np.float64,
    )
    / 255
)
lin = np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)
grey = lin @ np.array([0.299, 0.587, 0.114])
th, tw = grey.shape
yy, xx = np.mgrid[0:th, 0:tw]
rt = np.hypot((xx - (tw - 1) / 2) / tw, (yy - (th - 1) / 2) / th)
edges = np.arange(0, 0.3, 0.0025)
centres = (edges[:-1] + edges[1:]) / 2
profile = np.array(
    [grey[(rt >= a) & (rt < b)].mean() for a, b in itertools.pairwise(edges)]
)


def radius_where(g):
    """Texture radius (fraction of its side) at which the rising profile reaches grey g."""
    return float(np.interp(g, profile, centres))


# Central loss: w = 1 - grey. Peripheral loss: w = grey.
predicted_tex = {
    "central": radius_where(1 - level_w),
    "peripheral": radius_where(level_w),
}


def contrast(img, axis):
    """Mean absolute neighbour difference across the axis through the centre, smoothed."""
    if axis == "h":
        band = img[h // 2 - 2 : h // 2 + 3, :]
        d = np.abs(np.diff(band, axis=1)).mean(axis=0)
    else:
        band = img[:, w // 2 - 2 : w // 2 + 3]
        d = np.abs(np.diff(band, axis=0)).mean(axis=1)
    return np.convolve(d, np.ones(5) / 5, mode="same")


def load(name):
    rgb = np.asarray(Image.open(out / f"{name}.png").convert("RGB"), np.float64) / 255
    return rgb @ np.array([0.299, 0.587, 0.114])


def crossing(ratio, centre, step, rising):
    """Distance from the centre to where ratio first crosses 0.5, walking by step."""
    i = centre
    while 0 < i + step < len(ratio) - 1:
        a, b = ratio[i], ratio[i + step]
        if (a < 0.5 <= b) if rising else (a > 0.5 >= b):
            return abs(i - centre) + (0.5 - a) / (b - a)
        i += step
    return float("nan")


base = load("00_baseline")
failed = False
print(
    f"{w}x{h} px, {ppd:.2f} px/deg, MaxLOD {max_lod}, half-weight radius {half} of the texture"
)
print("effect      radius  predicted px  left  right    up  down  error  h/v")
for kind in ("central", "peripheral"):
    for r_deg in fl["radii_deg"]:
        name = f"fieldloss_{kind}_r{r_deg:.0f}"
        img = load(name)
        side = r_deg * ppd / half  # the overlay texture's side on screen, px
        pred = predicted_tex[kind] * side
        rising = kind == "central"
        found = {}
        for axis, centre in (("h", w // 2), ("v", h // 2)):
            ratio = contrast(img, axis) / np.maximum(contrast(base, axis), 1e-6)
            found[axis] = (
                crossing(ratio, centre, -1, rising),
                crossing(ratio, centre, 1, rising),
            )
        hr, vr = np.mean(found["h"]), np.mean(found["v"])
        err = (np.mean([hr, vr]) - pred) / pred
        hv = hr / vr - 1
        bad = not (abs(err) <= 0.05 and abs(hv) <= 0.03)
        failed |= bad
        print(
            f"{kind:<10} {r_deg:5.1f}  {pred:12.1f}  {found['h'][0]:4.0f}  {found['h'][1]:5.0f}  "
            f"{found['v'][0]:4.0f}  {found['v'][1]:4.0f}  {err:+5.1%}  {hv:+4.1%}{'  FAIL' if bad else ''}"
        )
sys.exit(1 if failed else 0)
