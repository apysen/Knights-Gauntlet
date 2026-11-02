"""Builds the App Store creative assets (stills) for Knight's Gauntlet.

Usage: python build_creatives.py <raw_iphone_dir> <out_dir>
  raw_iphone_dir: an "iPhone 6.9in (1320x2868)" folder written by Tools > Store Screenshots
                  (01.png menu, 02.png battle, 03.png trap, 04.png adventure, ...)
  out_dir:        where the finished PNGs go

Writes search_results_3840x2560.png (3:2, RGB, no alpha), the still version of the
search results creative. The product page header is key art only, no gameplay: see
build_header_video.py, whose first frame (header_poster.png) doubles as the still.

Apple asks for the focal point in the centre (devices crop differently), short text
only, and no prices, platform logos or awards, so the search card has only the game name.
Colours follow the web build palette and the screenshot frames (frame_screenshots.py).
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
TITLE_FONT = PROJECT / "ArtSource/Title/fonts/CinzelDecorative-Black.ttf"   # SIL OFL, same as the 3D title
BODY_FONT = PROJECT / "Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf"
HERO = PROJECT / "ArtSource/AppIcon/gauntlet_b.png"                        # transparent key art

NAVY_DEEP = np.array([8, 9, 22], float)
NAVY = np.array([22, 24, 58], float)
NAVY_LIFT = np.array([52, 58, 132], float)
GOLD = np.array([240, 176, 52], float)
GOLD_HI = (255, 236, 150)
GOLD_LO = (226, 150, 34)
INK = (14, 12, 24)


# ── Backdrop ─────────────────────────────────────────────────────────────────
def backdrop(W, H, cx, cy, floor_y):
    """Royal navy with a lifted glow at (cx, cy), soft gold rays, and a checkerboard
    floor in perspective below floor_y that fades into the dark."""
    y, x = np.mgrid[0:H, 0:W].astype(float)
    d = np.hypot((x - cx) / W, (y - cy) / W)
    t1 = np.clip(d / .38, 0, 1)[..., None]
    t2 = np.clip((d - .30) / .45, 0, 1)[..., None]
    rgb = NAVY_LIFT * (1 - t1) + NAVY * t1
    rgb = rgb * (1 - t2) + NAVY_DEEP * t2

    ang = np.arctan2(y - cy, x - cx)
    rays = (.5 + .5 * np.cos(ang * 22)) ** 7 * np.clip(1 - d / .62, 0, 1) ** 1.4
    rgb += GOLD * (rays * .20)[..., None]
    rgb += GOLD * (np.exp(-(d / .16) ** 2) * .30)[..., None]

    # perspective checkerboard: map each floor pixel back to board coordinates
    fy = y - floor_y
    on = fy > 0
    depth = np.where(on, (H - floor_y) / np.maximum(fy, 1), 1)        # 1 at the bottom edge, grows to the horizon
    bx = (x - cx) / W * depth * 9
    bz = depth * 4
    checker = ((np.floor(bx) + np.floor(bz)) % 2 == 0)
    light = np.array([46, 52, 104], float); dark = np.array([20, 22, 50], float)
    tile = np.where(checker[..., None], light, dark)
    # gold grout lines that thin out with distance
    gx = np.abs(bx - np.round(bx)); gz = np.abs(bz - np.round(bz))
    line = np.clip(1 - np.minimum(gx, gz) / (.035 * depth), 0, 1)
    tile = tile * (1 - line[..., None] * .55) + GOLD * (line[..., None] * .55)
    fade = np.clip(fy / (H - floor_y), 0, 1) ** .7 * np.clip(1 - np.abs(x - cx) / (W * .75), 0, 1)
    k = (on * fade * .9)[..., None]
    rgb = rgb * (1 - k) + tile * k
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


def motes(img, seed, count, cx, cy, spread):
    rng = np.random.default_rng(seed)
    W, H = img.size
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    for _ in range(count):
        a = rng.uniform(0, math.tau); r = rng.uniform(.08, 1) * spread
        x = cx + math.cos(a) * r; y = cy + math.sin(a) * r * .7
        s = rng.uniform(2, 7) * W / 3840 * 2
        dr.ellipse([x - s, y - s, x + s, y + s], fill=(255, 222, 140, int(rng.uniform(80, 210))))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(W / 3840 * 3)))


# ── Pieces ───────────────────────────────────────────────────────────────────
def glow_behind(img, art, pos, color=(255, 200, 90), strength=.85, blur_frac=.06):
    a = art.getchannel("A").point(lambda v: int(v * strength))
    g = Image.new("RGBA", art.size, color + (0,)); g.putalpha(a)
    pad = int(art.width * .3)
    big = Image.new("RGBA", (art.width + pad * 2, art.height + pad * 2), (0, 0, 0, 0))
    big.alpha_composite(g, (pad, pad))
    big = big.filter(ImageFilter.GaussianBlur(art.width * blur_frac))
    img.alpha_composite(big, (pos[0] - pad, pos[1] - pad))


def shadow_under(img, art, pos, offset, blur, opacity=.6):
    a = art.getchannel("A").point(lambda v: int(v * opacity))
    s = Image.new("RGBA", art.size, (0, 0, 6, 0)); s.putalpha(a)
    pad = blur * 3
    big = Image.new("RGBA", (art.width + pad * 2, art.height + pad * 2), (0, 0, 0, 0))
    big.alpha_composite(s, (pad, pad))
    big = big.filter(ImageFilter.GaussianBlur(blur))
    img.alpha_composite(big, (pos[0] - pad + offset[0], pos[1] - pad + offset[1]))


def hero(img, height, center_x, bottom_y):
    art = Image.open(HERO).convert("RGBA")
    art = art.crop(art.getbbox())
    w = int(art.width * height / art.height)
    art = art.resize((w, height), Image.LANCZOS)
    pos = (int(center_x - w / 2), int(bottom_y - height))
    glow_behind(img, art, pos)
    shadow_under(img, art, pos, (0, int(height * .02)), int(height * .025))
    img.alpha_composite(art, pos)


def card(img, shot_path, height, center, angle, crop=(0.105, 0.86)):
    """A gameplay crop on a gold-rimmed rounded card, tilted, with a soft shadow."""
    shot = Image.open(shot_path).convert("RGB")
    W0, H0 = shot.size
    shot = shot.crop((0, int(H0 * crop[0]), W0, int(H0 * crop[1])))
    w = int(shot.width * height / shot.height)
    shot = shot.resize((w, height), Image.LANCZOS)
    rim = max(6, int(height * .012)); rad = int(height * .045)
    cw, ch = w + rim * 2, height + rim * 2
    c = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    m = Image.new("L", (cw, ch), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, cw - 1, ch - 1], rad + rim, fill=255)
    gold = Image.fromarray(np.repeat(np.linspace(GOLD_HI, GOLD_LO, ch)[:, None, :], cw, axis=1).astype(np.uint8), "RGB")
    c.paste(gold, (0, 0), m)
    sm = Image.new("L", (w, height), 0)
    ImageDraw.Draw(sm).rounded_rectangle([0, 0, w - 1, height - 1], rad, fill=255)
    c.paste(shot, (rim, rim), sm)
    c = c.rotate(angle, resample=Image.BICUBIC, expand=True)
    pos = (int(center[0] - c.width / 2), int(center[1] - c.height / 2))
    shadow_under(img, c, pos, (int(height * .015), int(height * .03)), int(height * .03), .7)
    img.alpha_composite(c, pos)


# ── Type ─────────────────────────────────────────────────────────────────────
def gold_text(img, text, font, xy, stroke):
    """Gold-gradient lettering with a dark outline and a soft drop shadow."""
    W, H = img.size
    l, t, r, b = font.getbbox(text, stroke_width=stroke)
    x, y = xy[0] - (r - l) / 2 - l, xy[1] - t
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).text((x, y + stroke * 1.2), text, font=font, fill=(0, 0, 0, 190), stroke_width=stroke, stroke_fill=(0, 0, 0, 190))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(stroke * .8)))
    ImageDraw.Draw(img).text((x, y), text, font=font, fill=INK, stroke_width=stroke, stroke_fill=INK)
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).text((x, y), text, font=font, fill=255)
    top, bot = int(y + font.getbbox(text)[1]), int(y + font.getbbox(text)[3])
    grad = Image.new("RGB", img.size, GOLD_LO)
    grad.paste(Image.fromarray(np.repeat(np.linspace(GOLD_HI, GOLD_LO, max(1, bot - top))[:, None, :], W, axis=1).astype(np.uint8), "RGB"), (0, top))
    img.paste(grad, (0, 0), mask)
    return b - t


def logo(img, center_x, top_y, width):
    """KNIGHT'S over GAUNTLET in the title face, with a thin gold sword rule between."""
    big = ImageFont.truetype(str(TITLE_FONT), 10)
    size = 10
    while True:
        f = ImageFont.truetype(str(TITLE_FONT), size + 4)
        l, _, r, _ = f.getbbox("GAUNTLET")
        if r - l > width: break
        size += 4; big = f
    small = ImageFont.truetype(str(TITLE_FONT), int(size * .62))
    s1 = max(4, size // 14)
    h1 = gold_text(img, "KNIGHT'S", small, (center_x, top_y), max(3, s1 - 2))
    rule_y = top_y + h1 + size * .10
    dr = ImageDraw.Draw(img)
    half = width * .36
    dr.line([(center_x - half, rule_y), (center_x + half, rule_y)], fill=GOLD_LO, width=max(3, size // 26))
    dr.ellipse([center_x - size * .06, rule_y - size * .06, center_x + size * .06, rule_y + size * .06], fill=GOLD_HI, outline=INK, width=max(2, size // 50))
    h2 = gold_text(img, "GAUNTLET", big, (center_x, rule_y + size * .14), s1)
    return rule_y + size * .14 + h2


def vignette(img, strength=.55, bottom=0.0):
    W, H = img.size
    y, x = np.mgrid[0:H, 0:W].astype(float)
    d = np.hypot((x - W / 2) / (W / 2), (y - H / 2) / (H / 2)) / math.sqrt(2)
    a = np.clip((d - .45) / .55, 0, 1) ** 1.5 * strength
    if bottom > 0:
        a = np.maximum(a, np.clip((y / H - (1 - bottom)) / bottom, 0, 1) ** 1.3 * .7)
    m = Image.fromarray((a * 255).astype(np.uint8), "L")
    img.paste(Image.new("RGB", img.size, (4, 4, 12)), (0, 0), m)


# ── Assets ───────────────────────────────────────────────────────────────────
def search_results(raw, out):
    """3:2 search card. Read at thumbnail size, so: one big hero, the game name large,
    and two gameplay cards that show what you actually do."""
    W, H = 3840, 2560
    img = backdrop(W, H, W * .30, H * .42, H * .70)
    motes(img, 11, 80, W * .32, H * .45, W * .40)
    card(img, raw / "03.png", int(H * .58), (W * .845, H * .625), 5)
    card(img, raw / "02.png", int(H * .58), (W * .615, H * .645), -6)   # Battle, the main mode, in front
    hero(img, int(H * .98), W * .265, H * 1.0)
    # name across the top right, above the cards
    logo(img, W * .71, H * .04, W * .44)
    vignette(img, .45)
    img.convert("RGB").save(out / "search_results_3840x2560.png")
    return img


def main():
    raw, out = Path(sys.argv[1]), Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    search_results(raw, out)


if __name__ == "__main__":
    main()
