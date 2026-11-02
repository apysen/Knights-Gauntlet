"""Adds marketing headers to the raw store screenshots (Archero-style tilted banner).

Usage: python frame_screenshots.py <raw_dir> <out_dir>
  raw_dir: a dated folder written by Tools > Store Screenshots (one subfolder per size, 01.png..)
  out_dir: where the framed copies go (same subfolder layout, same pixel sizes)

Edit CAPTIONS below to change the text. Fonts are the game's own: Bangers (headings) and
Roboto Bold (body), both from the TextMesh Pro folder (SIL OFL / Apache).
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
HEAD_FONT = PROJECT / "Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf"
BODY_FONT = PROJECT / "Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf"
POPOUT = PROJECT / "ArtSource/AppIcon/gauntlet_b.png"

# shot number -> (headline, tagline, show the gauntlet pop-out)
CAPTIONS = {
    1: ("ENTER THE GAUNTLET", "One knight. Endless danger.", True),
    2: ("BUILD MASSIVE COMBOS", "Chain captures to multiply your score", False),
    3: ("DODGE DEADLY TRAPS", "One wrong jump and it's over", False),
    4: ("CONQUER 7 WORLDS", "Hand-crafted levels with 3 stars to earn", False),
    5: ("TRANSFORM YOUR KNIGHT", "Shift into new pieces mid-level", False),
    6: ("COLLECT EPIC SKINS", "Unlock knights, effects and powerups", False),
}

NAVY_TOP = (22, 24, 58)
NAVY_BOT = (44, 52, 128)
INK = (14, 14, 30)
GOLD_HI = (255, 236, 150)
GOLD_LO = (240, 160, 40)
TILT_DEG = 3.2


def vgradient(w, h, top, bot):
    t = np.linspace(0, 1, h)[:, None, None]
    arr = np.array(top, float) * (1 - t) + np.array(bot, float) * t
    return Image.fromarray(np.repeat(arr, w, axis=1).astype(np.uint8), "RGB")


def fit_font(path, text, max_w, max_h):
    size = int(max_h)
    while size > 10:
        f = ImageFont.truetype(str(path), size)
        l, t, r, b = f.getbbox(text)
        if r - l <= max_w and b - t <= max_h:
            return f
        size -= 2
    return ImageFont.truetype(str(path), 10)


def text_layer(w, h, headline, tagline, band_h):
    """Headline (gold gradient, dark outline, shadow) over a white tagline, unrotated."""
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    hf = fit_font(HEAD_FONT, headline, w * 0.86, band_h * 0.50)
    bf = fit_font(BODY_FONT, tagline, w * 0.80, band_h * 0.15)
    stroke = max(4, hf.size // 11)

    hl, ht, hr, hb = hf.getbbox(headline, stroke_width=stroke)
    hw, hh = hr - hl, hb - ht
    bl, bt, br, bb = bf.getbbox(tagline, stroke_width=max(2, bf.size // 9))
    bw, bh = br - bl, bb - bt
    gap = band_h * 0.04
    total = hh + gap + bh
    y0 = (band_h - total) / 2 + band_h * 0.02
    hx, hy = (w - hw) / 2 - hl, y0 - ht
    bx, by = (w - bw) / 2 - bl, y0 + hh + gap - bt

    # drop shadow
    sh = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(sh).text((hx, hy + stroke * 0.9), headline, font=hf, fill=(0, 0, 0, 170),
                            stroke_width=stroke, stroke_fill=(0, 0, 0, 170))
    layer.alpha_composite(sh.filter(ImageFilter.GaussianBlur(stroke * 0.6)))
    # outline
    ImageDraw.Draw(layer).text((hx, hy), headline, font=hf, fill=INK, stroke_width=stroke, stroke_fill=INK)
    # gold gradient fill through the glyph mask
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).text((hx, hy), headline, font=hf, fill=255)
    grad = Image.new("RGB", (w, h))
    top, bot = int(hy + ht), int(hy + hb)
    grad.paste(vgradient(w, max(1, bot - top), GOLD_HI, GOLD_LO), (0, top))
    layer.paste(grad, (0, 0), mask)
    # thin highlight line on the upper half of the glyphs
    hi = Image.new("L", (w, h), 0)
    ImageDraw.Draw(hi).rectangle([0, top, w, top + (bot - top) * 0.18], fill=70)
    hi = Image.composite(hi, Image.new("L", (w, h), 0), mask)
    layer.paste(Image.new("RGB", (w, h), (255, 255, 255)), (0, 0), hi)
    # tagline
    ImageDraw.Draw(layer).text((bx, by), tagline, font=bf, fill=(255, 255, 255),
                               stroke_width=max(2, bf.size // 9), stroke_fill=INK)
    return layer


def frame(shot, headline, tagline, popout):
    W, H = shot.size
    phone = W / H < 0.6
    band = int(H * (0.165 if phone else 0.17))
    slope = math.tan(math.radians(TILT_DEG))
    drop = slope * W                     # right edge is this much higher than the left

    out = Image.new("RGB", (W, H), NAVY_TOP)
    # gameplay pushed down under the banner (its bottom bar trims off)
    out.paste(shot, (0, int(band - drop * 0.35)))

    # banner: navy gradient with a slanted bottom edge (higher on the right)
    left_y, right_y = band + drop * 0.5, band - drop * 0.5
    banner = vgradient(W, H, NAVY_TOP, NAVY_BOT).convert("RGBA")
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).polygon([(0, 0), (W, 0), (W, right_y), (0, left_y)], fill=255)
    # soft gold rays inside the banner
    yy, xx = np.mgrid[0:H, 0:W]
    ang = np.arctan2(yy - band * 0.45, xx - W * 0.5)
    rays = ((0.5 + 0.5 * np.cos(ang * 18)) ** 8) * np.clip(1 - np.hypot(xx - W * .5, yy - band * .45) / (W * .7), 0, 1)
    rays_img = Image.fromarray((rays * 60).astype(np.uint8), "L")
    banner.paste(Image.new("RGB", (W, H), GOLD_LO), (0, 0), rays_img)
    # shadow cast onto the gameplay
    sh = Image.new("L", (W, H), 0)
    ImageDraw.Draw(sh).polygon([(0, 0), (W, 0), (W, right_y + H * .012), (0, left_y + H * .012)], fill=150)
    out.paste(Image.new("RGB", (W, H), (0, 0, 0)), (0, 0), sh.filter(ImageFilter.GaussianBlur(H * .008)))
    out.paste(banner.convert("RGB"), (0, 0), m)
    # gold trim along the slanted edge
    t = max(6, int(H * .006))
    trim = Image.new("L", (W, H), 0)
    ImageDraw.Draw(trim).polygon([(0, left_y - t), (W, right_y - t), (W, right_y + t), (0, left_y + t)], fill=255)
    out.paste(vgradient(W, H, GOLD_HI, GOLD_LO), (0, 0), trim)

    # pop-out art over the banner's right side
    if popout and POPOUT.exists():
        art = Image.open(POPOUT).convert("RGBA")
        art = art.crop(art.getbbox())
        ah = int(H * (0.28 if phone else 0.32))
        art = art.resize((int(art.width * ah / art.height), ah), Image.LANCZOS)
        ax, ay = int(W - art.width * 0.95), int(right_y - ah * 0.38)
        glow = Image.new("RGBA", art.size, (0, 0, 0, 0))
        glow.putalpha(art.getchannel("A").point(lambda v: int(v * .6)))
        glow = glow.filter(ImageFilter.GaussianBlur(H * .01))
        o = out.convert("RGBA")
        o.alpha_composite(glow, (ax, ay + int(H * .006)))
        o.alpha_composite(art, (ax, ay))
        out = o.convert("RGB")

    # text, tilted to match the banner; shifted left when the pop-out takes the right side
    tw = int(W * (0.70 if popout else 1.0))
    tx = int(W * 0.035) if popout else 0
    txt = text_layer(tw, band, headline, tagline, band)
    txt = txt.rotate(TILT_DEG, resample=Image.BICUBIC, expand=True)
    o = out.convert("RGBA")
    o.alpha_composite(txt, (tx + int((tw - txt.width) / 2), int((band - txt.height) / 2 - drop * 0.1)))
    return o.convert("RGB")


def main():
    raw, dst = Path(sys.argv[1]), Path(sys.argv[2])
    for size_dir in sorted(p for p in raw.iterdir() if p.is_dir()):
        for png in sorted(size_dir.glob("*.png")):
            n = int(png.stem)
            if n not in CAPTIONS:
                continue
            head, tag, pop = CAPTIONS[n]
            img = frame(Image.open(png).convert("RGB"), head, tag, pop)
            out = dst / size_dir.name / png.name
            out.parent.mkdir(parents=True, exist_ok=True)
            img.save(out)
            print(out, img.size)


if __name__ == "__main__":
    main()
