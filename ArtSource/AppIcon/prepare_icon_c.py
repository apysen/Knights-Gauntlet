"""Turns gauntlet_c.png (a finished icon with a baked black margin and rounded
corners) into the app icon set.

Usage: python prepare_icon_c.py gauntlet_c.png out_c

Writes:
  icon_1024.png          full-bleed square, no alpha (legacy/round/iOS slots)
  foreground_1024.png    Android adaptive foreground (art sized for the 72dp mask)
  background_1024.png    Android adaptive background (flat backdrop color)
  play_store_icon_512.png
  preview.png            square, rounded and circle mask check
"""
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

# Inner rounded square measured from the source (x 59..1195, y 53..1201).
# The gauntlet cuff runs off the bottom edge, so the crop keeps that bleed.
CROP = (61, 61, 1194, 1194)
N = 1024


def _box(a, r, axis):
    """Box blur of radius r along one axis (edge-padded), via a cumulative sum."""
    pad = [(0, 0)] * a.ndim
    pad[axis] = (r + 1, r)
    c = np.cumsum(np.pad(a, pad, mode="edge"), axis=axis, dtype=np.float64)
    n = a.shape[axis]
    hi = np.take(c, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
    lo = np.take(c, np.arange(0, n), axis=axis)
    return (hi - lo) / (2 * r + 1)


def blur(a, r):
    """Approximate Gaussian: three box passes per axis."""
    b = max(1, r // 2)
    for _ in range(3):
        a = _box(_box(a, b, 0), b, 1)
    return a


def fill_corners(img):
    """The crop still has black rounded corners; flood them with the backdrop color."""
    a = np.asarray(img).astype(np.int32)
    h, w, _ = a.shape
    lum = a.sum(2)
    mask = np.zeros((h, w), bool)
    q = deque()
    for (y, x) in ((0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)):
        if lum[y, x] < 30:
            mask[y, x] = True
            q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not mask[ny, nx] and lum[ny, nx] < 30:
                mask[ny, nx] = True
                q.append((ny, nx))
    # backdrop color: median of a ring just inside the corners
    bg = np.median(a[int(h * .12):int(h * .18), int(w * .1):int(w * .2)].reshape(-1, 3), 0)
    # The backdrop has a soft vignette, so a flat fill leaves a visible arc. Instead
    # inpaint the corners from their neighbours (normalised blur, coarse to fine).
    m = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7))
    hole = np.asarray(m) > 0
    known = (~hole).astype(np.float32)
    rgb = a.astype(np.float32) * known[..., None]
    fill = np.zeros_like(rgb)
    done = np.zeros(hole.shape, bool)
    for r in (8, 16, 32, 64, 128):
        num = np.stack([blur(rgb[..., c], r) for c in range(3)], -1)
        den = blur(known, r)
        ok = (den > 0.02) & ~done
        fill[ok] = num[ok] / den[ok][:, None]
        done |= ok
    out = a.astype(np.float32)
    out[hole] = fill[hole]
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)), tuple(int(v) for v in bg)


def rounded(img, frac=0.2237):
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, img.width - 1, img.height - 1], int(img.width * frac), fill=255)
    out = img.convert("RGBA")
    out.putalpha(m)
    return out


def circle(img):
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).ellipse([0, 0, img.width - 1, img.height - 1], fill=255)
    out = img.convert("RGBA")
    out.putalpha(m)
    return out


def main():
    src, out = Path(sys.argv[1]), Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    img = Image.open(src).convert("RGB").crop(CROP)
    img, bg = fill_corners(img)
    icon = img.resize((N, N), Image.LANCZOS)
    icon.save(out / "icon_1024.png")
    icon.resize((512, 512), Image.LANCZOS).save(out / "play_store_icon_512.png")

    # Android adaptive: 108dp canvas, launchers show the centre 72dp. Size the art
    # to 76dp so it fills the visible area with a little room for parallax, then
    # edge-extend outward so the cuff never ends in mid-air if a launcher shows more.
    s = int(round(N * 76 / 108))
    art = np.asarray(icon.resize((s, s), Image.LANCZOS))
    pad = (N - s) // 2
    # nudge down ~3dp so the horse's ears clear a circle mask; the cuff already bleeds off
    shift = int(round(N * 3 / 108))
    padded = np.pad(art, ((pad + shift, N - s - pad - shift), (pad, N - s - pad), (0, 0)), mode="edge")
    fg = Image.fromarray(padded).convert("RGBA")
    fg.save(out / "foreground_1024.png")
    Image.new("RGB", (N, N), bg).save(out / "background_1024.png")

    # preview: square icon, rounded, and the adaptive icon under a circle mask
    vis = int(N * 72 / 108)
    off = (N - vis) // 2
    adaptive = fg.crop((off, off, off + vis, off + vis))
    pv = Image.new("RGBA", (3 * 340 + 40, 380), (34, 38, 52, 255))
    pv.alpha_composite(icon.resize((320, 320), Image.LANCZOS).convert("RGBA"), (20, 30))
    pv.alpha_composite(rounded(icon.resize((320, 320), Image.LANCZOS)), (360, 30))
    pv.alpha_composite(circle(adaptive.resize((320, 320), Image.LANCZOS)), (700, 30))
    pv.save(out / "preview.png")


if __name__ == "__main__":
    main()
