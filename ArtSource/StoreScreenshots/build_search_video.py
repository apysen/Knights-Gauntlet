"""Renders the App Store search results creative as a seamless looping video.

Usage: python build_search_video.py <footage_root> <out_dir> [seconds] [scale]
  footage_root: <repo parent>/Store Art/Footage, written by Tools > Store Footage
                (Battle/0000.jpg ..., Trap/0000.jpg ...), 30 fps iPhone-size frames
  seconds:      loop length (Apple allows 5-30; default 12)
  scale:        1.0 = 3840x2560; 0.25 for a quick draft

Same layout as the still (build_creatives.search_results): the gauntlet hero on the
left, the game name top right, and two gameplay cards that now play real footage
(Battle on the left card, Trap on the right). Search videos are muted, so there is no
audio track. Loop seam: each card crossfades its clip's tail into its head over the last
FADE frames, and every other motion runs a whole number of cycles per loop.
"""
import math
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

import build_creatives as bc

FPS = 30
FADE = 20          # frames of crossfade at the loop seam
CROP = (0.105, 0.86)  # keep the board, drop the HUD bars (same as the stills)


class Clip:
    """Footage frames, loaded and fitted to the card on demand (a full clip at card
    size would need gigabytes)."""
    def __init__(self, folder, n_needed, card_h):
        self.files = sorted(Path(folder).glob("*.jpg"))
        if len(self.files) < n_needed:
            raise SystemExit(f"{folder}: need {n_needed} frames, found {len(self.files)}")
        self.h = card_h
        self.w = self[0].width

    def __getitem__(self, i):
        im = Image.open(self.files[i]).convert("RGB")
        W0, H0 = im.size
        im = im.crop((0, int(H0 * CROP[0]), W0, int(H0 * CROP[1])))
        return im.resize((int(im.width * self.h / im.height), self.h), Image.LANCZOS)


def card_parts(w, h):
    """Gold rim plate and the rounded mask for the footage, built once per card."""
    rim = max(4, int(h * .012)); rad = int(h * .045)
    cw, ch = w + rim * 2, h + rim * 2
    plate = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    m = Image.new("L", (cw, ch), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, cw - 1, ch - 1], rad + rim, fill=255)
    gold = Image.fromarray(np.repeat(np.linspace(bc.GOLD_HI, bc.GOLD_LO, ch)[:, None, :], cw, axis=1).astype(np.uint8), "RGB")
    plate.paste(gold, (0, 0), m)
    sm = Image.new("L", (w, h), 0)
    ImageDraw.Draw(sm).rounded_rectangle([0, 0, w - 1, h - 1], rad, fill=255)
    return plate, sm, rim


def main():
    root, out = Path(sys.argv[1]), Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    seconds = float(sys.argv[3]) if len(sys.argv) > 3 else 12.0
    scale = float(sys.argv[4]) if len(sys.argv) > 4 else 1.0
    W, H = int(3840 * scale) // 2 * 2, int(2560 * scale) // 2 * 2
    N = int(round(seconds * FPS))

    # Static backdrop (the motion lives in the cards, hero and motes).
    base = bc.backdrop(W, H, W * .30, H * .42, H * .70)

    card_h = int(H * .58)
    cards = [  # (clip folder, centre, tilt); drawn in order, so Battle (the main mode) sits in front
        (root / "Trap", (W * .845, H * .625), 5),
        (root / "Battle", (W * .615, H * .645), -6),
    ]
    clips, parts, shadows = [], [], []
    for folder, center, angle in cards:
        clip = Clip(folder, N + FADE, card_h)
        plate, mask, rim = card_parts(clip.w, card_h)
        rotated_plate = plate.rotate(angle, resample=Image.BICUBIC, expand=True)
        a = rotated_plate.getchannel("A").point(lambda v: int(v * .7))
        sh = Image.new("RGBA", rotated_plate.size, (0, 0, 6, 0)); sh.putalpha(a)
        pad = int(card_h * .09)
        big = Image.new("RGBA", (sh.width + pad * 2, sh.height + pad * 2), (0, 0, 0, 0))
        big.alpha_composite(sh, (pad, pad))
        shadows.append((big.filter(ImageFilter.GaussianBlur(card_h * .03)), pad))
        clips.append(clip); parts.append((plate, mask, rim, angle, center))

    # Static layers above the cards: hero (drawn per frame for the bob), logo, vignette.
    over = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    bc.logo(over, W * .71, H * .04, W * .44)

    art = Image.open(bc.HERO).convert("RGBA"); art = art.crop(art.getbbox())
    hero_h = int(H * .98)
    art = art.resize((int(art.width * hero_h / art.height), hero_h), Image.LANCZOS)
    glow_l = Image.new("L", (art.width + art.width * 2 // 3, art.height + art.width * 2 // 3), 0)
    gpad = art.width // 3
    glow_l.paste(art.getchannel("A"), (gpad, gpad))
    glow_l = glow_l.filter(ImageFilter.GaussianBlur(art.width * .06))

    yv, xv = np.mgrid[0:H, 0:W].astype(np.float32)
    vd = np.hypot((xv - W / 2) / (W / 2), (yv - H / 2) / (H / 2)) / math.sqrt(2)
    vig = (np.clip((vd - .45) / .55, 0, 1) ** 1.5 * .45)[..., None]
    del yv, xv, vd

    rng = np.random.default_rng(11)
    motes = [(rng.uniform(.02, .6) * W, rng.uniform(0, 1), rng.integers(1, 3), rng.uniform(2, 7) * W / 3840 * 2,
              rng.uniform(80, 210), rng.uniform(0, math.tau)) for _ in range(70)]

    name = "search_results_3840x2560.mp4" if scale == 1.0 else f"search_draft_{W}x{H}.mp4"
    ff = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
                           "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
                           "-c:v", "libx264", "-preset", "slow", "-crf", "16", "-pix_fmt", "yuv420p",
                           "-profile:v", "high", "-movflags", "+faststart", "-an", str(out / name)],
                          stdin=subprocess.PIPE)

    for f in range(N):
        p = f / N; w2 = p * math.tau
        frame = base.copy()

        # motes behind everything but the backdrop
        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0)); dr = ImageDraw.Draw(layer)
        for mx, my0, laps, s, al, ph in motes:
            my = ((my0 - p * laps) % 1.0) * (H + 40) - 20
            xx = mx + math.sin(w2 + ph) * W * .006
            dr.ellipse([xx - s, my - s, xx + s, my + s], fill=(255, 222, 140, int(al * (.6 + .4 * math.sin(w2 * 3 + ph)))))
        frame.alpha_composite(layer.filter(ImageFilter.GaussianBlur(max(1, W / 3840 * 3))))

        # gameplay cards: footage frame f, crossfading the tail back into the head
        for (plate, mask, rim, angle, center), clip, (sh, pad) in zip(parts, clips, shadows):
            # out[0] shows clip[N], so out[N-1] -> out[0] is clip[N-1] -> clip[N]: no jump
            cur = Image.blend(clip[f + N], clip[f], f / FADE) if f < FADE else clip[f]
            c = plate.copy()
            c.paste(cur, (rim, rim), mask)
            c = c.rotate(angle, resample=Image.BICUBIC, expand=True)
            pos = (int(center[0] - c.width / 2), int(center[1] - c.height / 2))
            frame.alpha_composite(sh, (pos[0] - pad + int(card_h * .015), pos[1] - pad + int(card_h * .03)))
            frame.alpha_composite(c, pos)

        # hero: bob, breathing glow, one light sweep per loop
        pulse = .5 + .5 * math.sin(w2 * 2)
        bob = math.sin(w2) * H * .010
        hx = int(W * .265 - art.width / 2); hy = int(H * 1.0 - art.height + bob)
        g = Image.new("RGBA", glow_l.size, (255, 200, 90, 0))
        g.putalpha(glow_l.point(lambda v, k=.6 + .3 * pulse: int(v * k)))
        frame.alpha_composite(g, (hx - gpad, hy - gpad))
        hero = art
        sweep_t = p / .4
        if sweep_t < 1:
            hn = np.asarray(art, np.float32)
            yy, xx = np.mgrid[0:art.height, 0:art.width].astype(np.float32)
            dist = (xx + yy * .55) - (sweep_t * 1.6 - .3) * (art.width + art.height)
            band = np.exp(-(dist / (art.width * .10)) ** 2) * (hn[..., 3] / 255)
            lum = hn[..., :3].mean(2) / 255
            k = (band * (.25 + .75 * lum) * .55)[..., None]
            hn[..., :3] = hn[..., :3] * (1 - k) + np.array([255, 246, 210], np.float32) * k
            hero = Image.fromarray(np.clip(hn, 0, 255).astype(np.uint8), "RGBA")
        frame.alpha_composite(hero, (hx, hy))
        frame.alpha_composite(over)

        rgb = np.asarray(frame.convert("RGB"), np.float32)
        rgb = rgb * (1 - vig) + np.array([4, 4, 12], np.float32) * vig
        buf = np.clip(rgb, 0, 255).astype(np.uint8)
        if f == 0:
            Image.fromarray(buf).save(out / ("search_results_poster.png" if scale == 1.0 else "search_draft_poster.png"))
        ff.stdin.write(buf.tobytes())
        if f % 30 == 0:
            print(f"frame {f}/{N}", flush=True)

    ff.stdin.close(); ff.wait()
    print("done")


if __name__ == "__main__":
    main()
