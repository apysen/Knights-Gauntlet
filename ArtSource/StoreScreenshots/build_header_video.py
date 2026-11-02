"""Renders the App Store product page header as a seamless looping video.

Usage: python build_header_video.py <out_dir> [seconds] [fps] [scale]
  seconds: loop length (Apple allows 5-30; default 10)
  fps:     30 or 60 (default 30)
  scale:   1.0 renders the full 3840x1646; 0.25 makes a fast low-res draft

Writes header_3840x1646.mp4 (H.264, yuv420p, no audio) and header_poster.png (frame 0,
which Apple shows before the video plays). No gameplay and no text: the store draws the
app name over the header, so this is pure key art. Everything moves on a whole number of
cycles per loop, so the last frame flows straight back into the first:
  - the gold rays turn by exactly one ray spacing
  - the checkerboard floor glides toward the camera by exactly two tiles
  - motes drift up and wrap, the hero bobs and breathes, the glow pulses, and one
    light sweep crosses the gold each loop
"""
import math
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

PROJECT = Path(__file__).resolve().parents[2]
HERO = PROJECT / "ArtSource/AppIcon/gauntlet_b.png"

NAVY_DEEP = np.array([8, 9, 22], np.float32)
NAVY = np.array([22, 24, 58], np.float32)
NAVY_LIFT = np.array([52, 58, 132], np.float32)
GOLD = np.array([240, 176, 52], np.float32)
RAYS = 22


def main():
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    seconds = float(sys.argv[2]) if len(sys.argv) > 2 else 10.0
    fps = int(sys.argv[3]) if len(sys.argv) > 3 else 30
    scale = float(sys.argv[4]) if len(sys.argv) > 4 else 1.0
    W, H = int(3840 * scale) // 2 * 2, int(1646 * scale) // 2 * 2
    N = int(round(seconds * fps))
    cx, cy, floor_y = W * .5, H * .40, H * .64

    # ── static fields ──
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.hypot((x - cx) / W, (y - cy) / W)
    t1 = np.clip(d / .38, 0, 1)[..., None]
    t2 = np.clip((d - .30) / .45, 0, 1)[..., None]
    base = NAVY_LIFT * (1 - t1) + NAVY * t1
    base = base * (1 - t2) + NAVY_DEEP * t2
    ang = np.arctan2(y - cy, x - cx)
    ray_fall = np.clip(1 - d / .62, 0, 1) ** 1.4
    halo = np.exp(-(d / .16) ** 2)

    fy = y - floor_y
    on = fy > 0
    depth = np.where(on, (H - floor_y) / np.maximum(fy, 1), 1).astype(np.float32)
    bx = (x - cx) / W * depth * 9
    bz0 = depth * 4
    gx = np.abs(bx - np.round(bx))
    floor_k = (on * np.clip(fy / (H - floor_y), 0, 1) ** .7 * np.clip(1 - np.abs(x - cx) / (W * .75), 0, 1) * .9)[..., None]
    light = np.array([46, 52, 104], np.float32); dark = np.array([20, 22, 50], np.float32)

    # vignette + darker lower edge (where the store draws the app name)
    vd = np.hypot((x - W / 2) / (W / 2), (y - H / 2) / (H / 2)) / math.sqrt(2)
    vig = np.clip((vd - .45) / .55, 0, 1) ** 1.5 * .5
    vig = np.maximum(vig, np.clip((y / H - .70) / .30, 0, 1) ** 1.3 * .7)[..., None]
    del y, x, fy, d, vd

    # ── hero (prepared once at full size, transformed per frame) ──
    art = Image.open(HERO).convert("RGBA")
    art = art.crop(art.getbbox())
    hero_h = int(H * 1.0)
    art = art.resize((int(art.width * hero_h / art.height), hero_h), Image.LANCZOS)
    a = np.asarray(art.getchannel("A"), np.float32) / 255
    pad = int(art.width * .35)
    glow = Image.new("L", (art.width + pad * 2, art.height + pad * 2), 0)
    glow.paste(art.getchannel("A"), (pad, pad))
    glow = glow.filter(ImageFilter.GaussianBlur(art.width * .07))
    glow_np = np.asarray(glow, np.float32) / 255
    shadow = glow.filter(ImageFilter.GaussianBlur(art.width * .02))

    # ── flanking pieces: transparent renders of the game's own enemy models (pieces/*.png) ──
    # (file, centre x as share of W, base y as share of H, height as share of H, mirror)
    # Heights follow real piece proportions; the nearer pair stands lower and larger.
    piece_dir = Path(__file__).resolve().parent / "pieces"
    layout = [("Rook", .135, .90, .40, False), ("Bishop", .300, .97, .50, False),
              ("Queen", .700, .97, .58, False), ("Knight", .865, .90, .44, True)]
    pieces = []
    for i, (name, px, py, ph, mirror) in enumerate(layout):
        path = piece_dir / f"{name}.png"
        if not path.exists():
            continue
        im = Image.open(path).convert("RGBA")
        im = im.crop(im.getbbox())
        if mirror:
            im = im.transpose(Image.FLIP_LEFT_RIGHT)
        h = int(H * ph)
        im = im.resize((int(im.width * h / im.height), h), Image.LANCZOS)
        # in the hero's shadow: dimmer and cooler, warm rim on the side facing the hero
        n = np.asarray(im, np.float32)
        lum = .62 if py > .93 else .48
        n[..., :3] = n[..., :3] * np.array([lum * .92, lum * .95, lum * 1.08], np.float32)
        xs = np.linspace(0, 1, im.width, dtype=np.float32)[None, :]
        rim_side = xs if px < .5 else 1 - xs
        edge = np.asarray(Image.fromarray(n[..., 3].astype(np.uint8)).filter(ImageFilter.MinFilter(9)), np.float32)
        rim = np.clip((n[..., 3] - edge) / 255, 0, 1) * rim_side ** 3
        n[..., :3] += np.array([255, 196, 90], np.float32) * (rim * .8)[..., None]
        im = Image.fromarray(np.clip(n, 0, 255).astype(np.uint8), "RGBA")
        if py < .93:
            im = im.filter(ImageFilter.GaussianBlur(max(1, W / 3840 * 2.5)))   # far pair slightly soft
        # contact shadow ellipse under the base
        sw = int(im.width * 1.1); shh = int(im.width * .22)
        sh_img = Image.new("L", (sw, shh), 0)
        ImageDraw.Draw(sh_img).ellipse([0, 0, sw - 1, shh - 1], fill=150)
        sh_img = sh_img.filter(ImageFilter.GaussianBlur(shh * .25))
        pieces.append((im, sh_img, px * W, py * H, i * 1.3))

    # ── motes (wrap seamlessly: each travels a whole number of screen heights per loop) ──
    rng = np.random.default_rng(3)
    motes = [(rng.uniform(.08, .92) * W, rng.uniform(0, 1), rng.integers(1, 3), rng.uniform(2, 7) * W / 3840 * 2,
              rng.uniform(80, 210), rng.uniform(0, math.tau)) for _ in range(80)]

    ff = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
                           "-s", f"{W}x{H}", "-r", str(fps), "-i", "-",
                           "-c:v", "libx264", "-preset", "slow", "-crf", "15", "-pix_fmt", "yuv420p",
                           "-profile:v", "high", "-movflags", "+faststart", "-an",
                           str(out / ("header_3840x1646.mp4" if scale == 1.0 else f"header_draft_{W}x{H}.mp4"))],
                          stdin=subprocess.PIPE)

    for f in range(N):
        p = f / N                      # 0..1 loop phase
        w2 = p * math.tau
        # rays turn one ray spacing per loop; glow breathes twice
        rays = (.5 + .5 * np.cos(ang * RAYS + w2)) ** 7 * ray_fall
        pulse = .5 + .5 * math.sin(w2 * 2)
        rgb = base + GOLD * (rays * (.17 + .06 * pulse))[..., None] + GOLD * (halo * (.26 + .08 * pulse))[..., None]

        # floor glides two tiles toward the camera per loop
        bz = bz0 + p * 2
        checker = (np.floor(bx) + np.floor(bz)) % 2 == 0
        tile = np.where(checker[..., None], light, dark)
        gz = np.abs(bz - np.round(bz))
        line = np.clip(1 - np.minimum(gx, gz) / (.035 * depth), 0, 1)[..., None]
        tile = tile * (1 - line * .55) + GOLD * (line * .55)
        rgb = rgb * (1 - floor_k) + tile * floor_k
        frame = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB").convert("RGBA")

        # motes
        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dr = ImageDraw.Draw(layer)
        for mx, my0, laps, s, al, ph in motes:
            my = ((my0 - p * laps) % 1.0) * (H + 40) - 20
            xx = mx + math.sin(w2 + ph) * W * .006
            tw = al * (.6 + .4 * math.sin(w2 * 3 + ph))
            dr.ellipse([xx - s, my - s, xx + s, my + s], fill=(255, 222, 140, int(tw)))
        frame.alpha_composite(layer.filter(ImageFilter.GaussianBlur(max(1, W / 3840 * 3))))

        # flanking pieces, each bobbing on its own phase (whole cycles per loop)
        for im, sh_img, px, py, ph in pieces:
            b = math.sin(w2 + ph) * H * .006
            frame.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", sh_img.size, (0, 0, 8)).split(), sh_img)),
                                  (int(px - sh_img.width / 2), int(py - sh_img.height * .55)))
            frame.alpha_composite(im, (int(px - im.width / 2), int(py - im.height + b)))

        # hero: gentle bob + breathe, glow behind, soft shadow
        bob = math.sin(w2) * H * .012
        breathe = 1 + .012 * math.sin(w2 * 2)
        hw, hh = int(art.width * breathe), int(art.height * breathe)
        hx = int(cx - hw / 2); hy = int(H * 1.0 - hh + bob + H * .01)
        g = Image.fromarray((glow_np * 255 * (.55 + .25 * pulse)).astype(np.uint8), "L").resize(
            (int(glow.width * breathe), int(glow.height * breathe)))
        gl = Image.new("RGBA", g.size, (255, 200, 90, 0)); gl.putalpha(g)
        frame.alpha_composite(gl, (hx - int(pad * breathe), hy - int(pad * breathe)))
        sh = Image.new("RGBA", shadow.size, (0, 0, 6, 0)); sh.putalpha(shadow.point(lambda v: int(v * .55)))
        frame.alpha_composite(sh.resize(g.size), (hx - int(pad * breathe), hy - int(pad * breathe) + int(H * .02)))

        hero = art.resize((hw, hh), Image.BILINEAR) if breathe != 1 else art
        # one light sweep across the gold per loop (a diagonal band, during the first 40%)
        sweep_t = p / .4
        if sweep_t < 1:
            hn = np.asarray(hero, np.float32)
            yy, xx = np.mgrid[0:hh, 0:hw].astype(np.float32)
            band_pos = (sweep_t * 1.6 - .3) * (hw + hh)
            dist = (xx + yy * .55) - band_pos
            band = np.exp(-(dist / (hw * .10)) ** 2) * (hn[..., 3] / 255)
            # brighten mostly where the art is already bright (the gold), not the steel
            lum = hn[..., :3].mean(2) / 255
            k = (band * (.25 + .75 * lum) * .55)[..., None]
            hn[..., :3] = hn[..., :3] * (1 - k) + np.array([255, 246, 210], np.float32) * k
            hero = Image.fromarray(np.clip(hn, 0, 255).astype(np.uint8), "RGBA")
        frame.alpha_composite(hero, (hx, hy))

        # vignette last
        out_rgb = np.asarray(frame.convert("RGB"), np.float32)
        out_rgb = out_rgb * (1 - vig) + np.array([4, 4, 12], np.float32) * vig
        buf = np.clip(out_rgb, 0, 255).astype(np.uint8)
        if f == 0:
            Image.fromarray(buf).save(out / ("header_poster.png" if scale == 1.0 else "header_draft_poster.png"))
        ff.stdin.write(buf.tobytes())
        if f % 30 == 0:
            print(f"frame {f}/{N}", flush=True)

    ff.stdin.close()
    ff.wait()
    print("done")


if __name__ == "__main__":
    main()
