"""Composes the Knight's Gauntlet app icon from a transparent foreground render.

Usage: python compose_icon.py <foreground.png> <out_dir> [scale] [y_offset]

Writes:
  icon_1024.png         full square icon, no alpha (App Store / iOS)
  background_1024.png   backdrop only (Android adaptive background layer)
  foreground_1024.png   art only, transparent, inside the adaptive safe zone
  preview.png           mock home-screen check at several sizes
Colors come from the web build palette (#1a1a2e body, #2d2d4e cells, #f0c040 gold).
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

N = 1024
NAVY_DEEP = np.array([12, 12, 26], float)
NAVY = np.array([26, 26, 46], float)       # #1a1a2e
NAVY_LIFT = np.array([52, 50, 96], float)  # a lifted #2d2d4e for the centre
GOLD = np.array([240, 192, 64], float)     # #f0c040


def backdrop(glow_center=(0.5, 0.36)):
    y, x = np.mgrid[0:N, 0:N] / N
    cx, cy = glow_center
    d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2)
    # navy radial gradient: lifted centre -> base navy -> deep corners
    t1 = np.clip(d / 0.45, 0, 1)[..., None]
    t2 = np.clip((d - 0.45) / 0.35, 0, 1)[..., None]
    rgb = NAVY_LIFT * (1 - t1) + NAVY * t1
    rgb = rgb * (1 - t2) + NAVY_DEEP * t2

    # soft gold rays fanning out behind the knight's head
    ang = np.arctan2(y - cy, x - cx)
    rays = (0.5 + 0.5 * np.cos(ang * 14)) ** 6
    fall = np.clip(1 - d / 0.62, 0, 1) ** 1.6
    rgb += GOLD[None, None, :] * (rays * fall * 0.16)[..., None]

    # warm halo
    halo = np.exp(-(d / 0.20) ** 2)
    rgb += GOLD[None, None, :] * (halo * 0.38)[..., None]

    # faint checkerboard ring along the bottom for a hint of the board
    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")
    return img


def sparkles(img, seed=7, count=18):
    rng = np.random.default_rng(seed)
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    for _ in range(count):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0.18, 0.44) * N
        x = N * 0.5 + math.cos(a) * r
        y = N * 0.36 + math.sin(a) * r * 0.9
        s = rng.uniform(2, 6)
        al = int(rng.uniform(90, 200))
        dr.ellipse([x - s, y - s, x + s, y + s], fill=(255, 225, 140, al))
    layer = layer.filter(ImageFilter.GaussianBlur(1.6))
    out = img.convert("RGBA")
    out.alpha_composite(layer)
    return out


def place(fg, scale, y_offset, canvas=N):
    bbox = fg.getbbox()
    art = fg.crop(bbox)
    h = int(canvas * scale)
    w = int(art.width * h / art.height)
    art = art.resize((w, h), Image.LANCZOS)
    layer = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    x = (canvas - w) // 2
    y = canvas - h + int(canvas * y_offset)
    layer.alpha_composite(art, (x, y)) if y >= 0 else layer.paste(art, (x, y), art)
    return layer


def drop_shadow(layer, blur=18, opacity=0.55, offset=(0, 10)):
    a = layer.getchannel("A").point(lambda v: int(v * opacity))
    sh = Image.new("RGBA", layer.size, (0, 0, 8, 0))
    sh.putalpha(a)
    sh = sh.filter(ImageFilter.GaussianBlur(blur))
    out = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    out.alpha_composite(sh, offset)
    return out


def rounded(img, radius_frac=0.2237):
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, img.width - 1, img.height - 1],
                                        int(img.width * radius_frac), fill=255)
    out = img.convert("RGBA")
    out.putalpha(m)
    return out


def main():
    src = Path(sys.argv[1])
    out = Path(sys.argv[2])
    scale = float(sys.argv[3]) if len(sys.argv) > 3 else 0.98
    y_off = float(sys.argv[4]) if len(sys.argv) > 4 else 0.07
    out.mkdir(parents=True, exist_ok=True)
    fg = Image.open(src).convert("RGBA")

    bg = sparkles(backdrop())
    art = place(fg, scale, y_off)
    icon = bg.copy()
    icon.alpha_composite(drop_shadow(art))
    icon.alpha_composite(art)
    icon.convert("RGB").save(out / "icon_1024.png")
    bg.convert("RGB").save(out / "background_1024.png")

    # Android adaptive: launcher masks keep only the centre 66%; keep art inside ~64%.
    place(fg, 0.62, -0.19).save(out / "foreground_1024.png")

    # preview: big + home-screen sizes on a dark wallpaper
    pv = Image.new("RGBA", (1400, 560), (34, 38, 52, 255))
    pv.alpha_composite(rounded(icon.resize((512, 512), Image.LANCZOS)), (24, 24))
    x = 580
    for s in (180, 120, 87, 58):
        pv.alpha_composite(rounded(icon.resize((s, s), Image.LANCZOS)), (x, 280 - s // 2))
        x += s + 40
    # adaptive circle-mask preview
    cm = Image.new("L", (180, 180), 0)
    ImageDraw.Draw(cm).ellipse([0, 0, 179, 179], fill=255)
    ad = bg.copy()
    ad.alpha_composite(place(fg, 0.62, -0.19))
    ad = ad.resize((180, 180), Image.LANCZOS).convert("RGBA")
    ad.putalpha(cm)
    pv.alpha_composite(ad, (1190, 370))
    pv.save(out / "preview.png")


if __name__ == "__main__":
    main()
