"""Turns gauntlet_d.png (full-bleed art on a flat near-black backdrop) into the app icon set.

Usage: python prepare_icon_d.py gauntlet_d.png out_d

Writes:
  icon_1024.png          full-bleed square, no alpha (legacy/round/iOS slots)
  foreground_1024.png    Android adaptive foreground (art sized for the 72dp mask)
  background_1024.png    Android adaptive background (flat backdrop color)
  play_store_icon_512.png
  preview.png            square, rounded and circle mask check
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

# The source is 1254 px with the gauntlet cuff ending at row 1195 and plain backdrop
# below it. Crop a centred 1196 px square from the top so the cuff runs off the bottom
# edge (as on gauntlet_c) instead of floating above it.
CROP = (29, 0, 1225, 1196)
N = 1024


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
    full = Image.open(src).convert("RGB")
    a = np.asarray(full)
    bg = tuple(int(v) for v in np.median(a[:40, :40].reshape(-1, 3), 0))
    img = full.crop(CROP)
    icon = img.resize((N, N), Image.LANCZOS)
    icon.save(out / "icon_1024.png")
    icon.resize((512, 512), Image.LANCZOS).save(out / "play_store_icon_512.png")

    # Android adaptive: 108dp canvas, launchers show the centre 72dp. Size the art
    # to 76dp so it fills the visible area with a little room for parallax, then
    # edge-extend outward so the cuff keeps running off the bottom if a launcher
    # shows more of the layer.
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
    print("backdrop", bg)


if __name__ == "__main__":
    main()
