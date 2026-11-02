"""Build the War Horn powerup sounds from the score's palette (CC0 VSCO 2 CE samples).

    python -I build_warhorn.py <vsco folder> [--no-copy]

<vsco folder> is where fetch_vsco.py put the samples (outside the repo). Writes three mono
WAVs into ./out and copies them into Assets/Resources/SFX/:

- Powerup War Horn.wav  the blast at activation: horns and trombones on an open fifth D-A
                        (no third, so it sits over any chord of the score), a big drum,
                        timpani and a crash, in the castle hall. About 1.6 s with its tail.
- Rally Tick.wav        one short bright glockenspiel D with a metal click. SfxManager
                        raises its pitch up a D major pentatonic as the count climbs, so
                        one clip covers every step.
- Rally Slam.wav        the count lands on the peak: big drum, timpani, low tom and a crash
                        under a staccato D major stab on horns and trombones (the score's
                        Picardy landing, as in the queen's launch) and a glock shimmer.

It also builds the Berserker Draught set:

- Powerup Berserker.wav cork pop and gulp, the flame igniting, then a low brass and drum
                        hit at 0.16 s (the "FURY!" card after the hit-stop), fire roaring
                        down. About 1.35 s.
- Fury Crackle.wav      a quiet fire burst played on each capture under the draught.

And the main menu sounds (synthesized only, played on the UI mixer group):

- Menu Entrance Whoosh.wav  a soft airy swell under the entrance pop-ins.
- Menu Title Tinks.wav      a tiny tink per 3D title letter landing (15, 45 ms apart).
- Knight Clank 1-3.wav      a light armour clank when the knight portrait is tapped.

War Horn timeline in game: horn at 0.00 s, ticks 0.30 to 1.05 s, slam at 1.05 s. Seeded, so every
run makes the same files. Mastered like the transformation set (mono, gain to a loudness
target, true peak under -1 dBTP).
"""
import site, sys
sys.path.append(site.getusersitepackages())
import shutil
from pathlib import Path
import numpy as np

sys.path.insert(0, str(Path(__file__).parent))
import music_lib as M
from music_lib import RATE, Buffer, db
import build_music as B

SEED = B.SEED + 77


def blend_reverb(x, ir, amount):
    w = M.reverb(x, ir, False)
    return x + amount * w / (np.sqrt(np.mean(w ** 2)) + 1e-9) * np.sqrt(np.mean(x ** 2))


def fade_tail(x, seconds):
    f = int(seconds * RATE)
    x = x.copy()
    x[-f:] *= (np.cos(np.linspace(0, np.pi / 2, f)) ** 2)[:, None]
    return x


def horn(kit, ir):
    L = 1.6
    buf = Buffer(L, False)
    hold = 0.78  # the blast holds, then releases as the count starts climbing
    # Sustained brass on the open fifth: horns D4 A4 D5, trombones D3 A3.
    for m, p, g in [(62, 0.15, 0.55), (69, -0.15, 0.5), (74, 0.05, 0.32)]:
        buf.place(M.pan(M.mono(M.shape(kit.horn.get(m), hold, 0.35, fade_in=0.006)), p) * g, 0.0)
    for m, p, g in [(50, -0.2, 0.55), (57, 0.2, 0.45)]:
        buf.place(M.pan(M.mono(M.shape(kit.tbn.get(m), hold, 0.35, fade_in=0.006)), p) * g, 0.0)
    # Staccato attacks on the same notes give the blast its bite at the flash.
    for m in (62, 69):
        buf.place(M.pan(M.mono(M.ring(kit.horn_stac.get(m), 0.4)), 0.1) * 0.35, 0.0)
    buf.place(M.pan(M.mono(M.ring(kit.tbn_stac.get(50), 0.4)), -0.1) * 0.4, 0.0)
    # Drum, timpani and a crash under it.
    buf.place(M.ring(kit.hit("mallet_ff"), 1.5) * 0.8, 0.0)
    buf.place(M.ring(kit.timp_d, 1.6) * 0.55, 0.0)
    buf.place(M.ring(kit.hit("cym"), 1.6) * 0.18, 0.0)
    x = blend_reverb(buf.x, ir, 0.4)
    return fade_tail(x, 0.3)


def tick(kit, ir):
    L = 0.16
    n = int(L * RATE)
    t = np.arange(n) / RATE
    buf = Buffer(L, False)
    g = M.mono(M.ring(kit.glock.get(86), L))  # D6 written, a bright bell
    g = g * np.exp(-t[: len(g)] / 0.05)
    buf.place(g * 0.8, 0.0)
    # A tiny metal click on the front edge, like a coin tapping.
    click = M.swept_noise(L, [(3500, 9000, np.exp(-t / 0.004)), (1800, 3500, np.exp(-t / 0.006))], SEED + 1)
    buf.place(click / (np.abs(click).max() + 1e-9) * 0.35, 0.0)
    x = blend_reverb(buf.x, ir, 0.12)
    return fade_tail(x, 0.04)


def slam(kit, ir):
    L = 1.3
    buf = Buffer(L, False)
    t = np.arange(int(L * RATE)) / RATE
    crack = M.swept_noise(L, [(lo, hi, np.exp(-t / tau)) for lo, hi, tau in
                              [(1500, 4000, 0.03), (4000, 10000, 0.02)]], SEED + 2)
    buf.place(M.pan(crack / (np.abs(crack).max() + 1e-9) * 0.3, 0), 0.0)
    buf.place(M.ring(kit.hit("mallet_ff"), 1.3) * 0.95, 0.0)
    buf.place(M.ring(kit.timp_d, 1.3) * 0.6, 0.0)
    buf.place(M.ring(kit.hit("tom_f"), 0.8) * 0.45, 0.0)
    buf.place(M.ring(kit.hit("cym"), 1.3) * 0.28, 0.0)
    # D major stab: horns D4 F#4 A4 D5, trombones D3 A3.
    for m, p in [(62, 0.1), (66, -0.1), (69, 0.15), (74, -0.05)]:
        buf.place(M.pan(M.mono(M.ring(kit.horn_stac.get(m), 0.55)), p) * 0.4, 0.004)
    for m, p in [(50, -0.15), (57, 0.15)]:
        buf.place(M.pan(M.mono(M.ring(kit.tbn_stac.get(m), 0.55)), p) * 0.45, 0.004)
    # Shimmer: the glock on A and D above, just after the hit.
    for k, m in enumerate((81, 86)):
        buf.place(M.pan(M.mono(M.ring(kit.glock.get(m), 1.1)), 0.2 - 0.4 * k) * 0.22, 0.03 + 0.05 * k)
    x = blend_reverb(buf.x, ir, 0.38)
    return fade_tail(x, 0.25)


def crackles(L, count, seed, start=0.0, decay=None):
    """Sparse fire crackles: tiny filtered clicks at random times, louder early if decay is set."""
    r = np.random.default_rng(seed)
    n = int(L * RATE)
    y = np.zeros(n)
    for _ in range(count):
        t0 = start + r.uniform(0, L - start - 0.02)
        k = int(t0 * RATE)
        m = int(r.uniform(0.002, 0.006) * RATE)
        burst = r.standard_normal(m) * np.exp(-np.arange(m) / (m / 4))
        g = r.uniform(0.3, 1.0) * (np.exp(-t0 / decay) if decay else 1.0)
        y[k:k + m] += burst[: n - k] * g
    return M.mono(M.highpass(np.stack([y, y], 1), 1800, False))


def flame(L, seed, ignite_at, rise, tau):
    """A roar of flame: band noise whose band sweeps up as it ignites, then flickers down."""
    n = int(L * RATE)
    t = np.arange(n) / RATE
    r = np.random.default_rng(seed)
    up = np.clip((t - ignite_at) / rise, 0, 1)
    body = np.where(t < ignite_at + rise, up ** 2, np.exp(-(t - ignite_at - rise) / tau))
    flicker = 1 + 0.25 * np.interp(t, np.linspace(0, L, 40), r.uniform(-1, 1, 40))
    bands = [(150, 450, body * flicker),
             (450, 1200, body * flicker * (0.4 + 0.6 * up)),
             (1200, 3500, body * flicker * up * 0.6 * np.exp(-np.clip(t - ignite_at - rise, 0, None) / (tau * 0.5)))]
    return M.swept_noise(L, bands, seed)


def fury(kit, ir):
    """Berserker Draught: a cork pop and a gulp, the flame igniting, then a low brass and drum
    hit as the 'FURY!' card lands after the 0.16 s hit-stop, and the fire roaring down."""
    L = 1.35
    n = int(L * RATE)
    t = np.arange(n) / RATE
    buf = Buffer(L, False)
    # Cork pop: a falling sine chirp with a click on its front.
    pl = int(0.04 * RATE)
    tp = np.arange(pl) / RATE
    f = 1100 * np.exp(-tp / 0.012) + 380
    pop = np.sin(2 * np.pi * np.cumsum(f) / RATE) * np.exp(-tp / 0.01)
    pop[:40] += np.random.default_rng(SEED + 3).standard_normal(40) * np.linspace(1, 0, 40) * 0.6
    buf.place(M.pan(pop * 0.45, 0.1), 0.0)
    # Gulp: a short low bubble falling in pitch.
    gl = int(0.07 * RATE)
    tg = np.arange(gl) / RATE
    fg = 420 - 2200 * tg
    gulp = np.sin(2 * np.pi * np.cumsum(fg) / RATE) * np.sin(np.pi * tg / tg[-1]) ** 2
    buf.place(M.pan(gulp * 0.35, -0.05), 0.05)
    # Ignition and roar.
    fl = flame(L, SEED + 4, 0.04, 0.12, 0.38)
    buf.place(M.pan(fl / (np.abs(fl).max() + 1e-9) * 0.55, 0), 0.0)
    cr = crackles(L, 60, SEED + 5, start=0.12, decay=0.45)
    buf.place(M.pan(cr / (np.abs(cr).max() + 1e-9) * 0.25, 0.15), 0.0)
    # The hit, on the title card: big drum, low drum, timpani, and low brass on D and A with
    # the horns biting on D and the castle's Eb against it.
    h = 0.16
    buf.place(M.ring(kit.hit("mallet_ff"), 1.2) * 0.95, h)
    buf.place(M.ring(kit.hit("tom_f"), 0.7) * 0.5, h)
    buf.place(M.ring(kit.timp_d, 1.2) * 0.55, h)
    for m, p in [(38, -0.1), (45, 0.1), (50, -0.15)]:
        buf.place(M.pan(M.mono(M.ring(kit.tbn_stac.get(m), 0.55)), p) * 0.5, h + 0.004)
    for m, p, g in [(62, 0.15, 0.4), (63, -0.15, 0.22)]:
        buf.place(M.pan(M.mono(M.shape(kit.horn.get(m), 0.28, 0.2, fade_in=0.004)), p) * g, h + 0.004)
    x = blend_reverb(buf.x, ir, 0.3)
    return fade_tail(x, 0.25)


def fury_crackle(ir):
    """A quieter burst of fire on each capture while the draught is active."""
    L = 0.32
    n = int(L * RATE)
    t = np.arange(n) / RATE
    buf = Buffer(L, False)
    fl = M.swept_noise(L, [(200, 700, np.exp(-t / 0.08)), (700, 2000, np.exp(-t / 0.05))], SEED + 6)
    fl *= np.clip(t / 0.01, 0, 1)
    buf.place(M.pan(fl / (np.abs(fl).max() + 1e-9) * 0.5, 0), 0.0)
    cr = crackles(L, 14, SEED + 7, decay=0.08)
    buf.place(M.pan(cr / (np.abs(cr).max() + 1e-9) * 0.6, 0), 0.0)
    x = blend_reverb(buf.x, ir, 0.1)
    return fade_tail(x, 0.06)


# ------------------------------------------------------------------------- main menu (synthesized)
def metal(freq, ratios, decays, L, seed, amps=None):
    """A small struck metal object: inharmonic sine partials, each with its own decay."""
    r = np.random.default_rng(seed)
    n = int(L * RATE)
    t = np.arange(n) / RATE
    y = np.zeros(n)
    amps = amps or [1.0 / (1 + k) for k in range(len(ratios))]
    for k, (q, d) in enumerate(zip(ratios, decays)):
        f = freq * q * (1 + r.uniform(-0.004, 0.004))
        if f > 16000:
            continue
        y += amps[k] * np.sin(2 * np.pi * f * t + r.uniform(0, 2 * np.pi)) * np.exp(-t / d)
    return y * np.clip(t / 0.0008, 0, 1)


# The menu score is in D minor, so the tinks walk a D minor pentatonic in a gentle wave.
TITLE_TINKS = [86, 89, 91, 93, 91, 89, 93, 96, 93, 91, 96, 98, 96, 93, 98]
LETTER_LAND = 0.42 * 0.6   # MenuTitle3D: a letter lands 60 % of the way through its 0.42 s drop
LETTER_STAGGER = 0.045


def menu_whoosh(ir):
    """A soft airy swell as the crest and buttons pop in: band noise rising, then settling."""
    L = 1.0
    n = int(L * RATE)
    t = np.arange(n) / RATE
    c = np.interp(t, [0, 0.32, 1.0], [np.log(450), np.log(2600), np.log(900)])
    centres = 300 * (4000 / 300) ** (np.arange(7) / 6)
    bands = [(cc / 1.35, cc * 1.35, np.exp(-((c - np.log(cc)) / 0.45) ** 2)) for cc in centres]
    y = M.swept_noise(L, bands, SEED + 10)
    y *= np.clip(t / 0.28, 0, 1) ** 1.5 * np.exp(-np.clip(t - 0.3, 0, None) / 0.22)
    buf = Buffer(L, False)
    buf.place(M.pan(y / (np.abs(y).max() + 1e-9), 0), 0.0)
    return fade_tail(blend_reverb(buf.x, ir, 0.25), 0.2)


def title_tinks(ir):
    """One tiny metal tink as each 3D title letter lands (15 letters, 45 ms apart)."""
    L = 1.05
    r = np.random.default_rng(SEED + 11)
    buf = Buffer(L, False)
    for i, m in enumerate(TITLE_TINKS):
        y = metal(M.midi_hz(m), [1, 2.76, 5.40], [0.05, 0.025, 0.012], 0.12, SEED + 20 + i, [1, 0.35, 0.15])
        g = db(r.uniform(-2, 2) - 1.5 * i / len(TITLE_TINKS))
        buf.place(y * g * 0.5, LETTER_LAND + i * LETTER_STAGGER + r.uniform(-0.003, 0.003))
    return fade_tail(blend_reverb(buf.x, ir, 0.3), 0.08)


def knight_clank(ir, variant):
    """A light armour clink-clank as the knight portrait rears: two plate hits 40 ms apart and
    a little low body, kept at UI-tap weight."""
    L = 0.32
    n = int(L * RATE)
    t = np.arange(n) / RATE
    f0 = [1180, 1260, 1340][variant]
    ratios, decays = [1, 1.47, 2.09, 2.56, 3.41, 4.7], [0.11, 0.08, 0.06, 0.05, 0.035, 0.025]
    buf = Buffer(L, False)
    for k, (dt, g, f) in enumerate([(0.0, 1.0, f0), (0.04 + 0.006 * variant, 0.55, f0 * 1.12)]):
        hit = metal(f, ratios, decays, L - dt, SEED + 30 + 10 * variant + k)
        click = M.swept_noise(L - dt, [(3000, 9000, np.exp(-t / 0.004))], SEED + 40 + variant + k)
        body = np.sin(2 * np.pi * 210 * t[: len(hit)]) * np.exp(-t[: len(hit)] / 0.025)
        x = hit / (np.abs(hit).max() + 1e-9) + 0.4 * click / (np.abs(click).max() + 1e-9) + 0.35 * body
        buf.place(x * g, dt)
    return fade_tail(blend_reverb(buf.x, ir, 0.15), 0.06)


def main():
    kit = B.Kit(sys.argv[1])
    copy = "--no-copy" not in sys.argv
    ir = M.make_ir("castle", SEED)
    small = M.make_ir("meadow", SEED)
    jobs = [("Powerup War Horn", horn(kit, ir), "lufs", -14.0),
            ("Rally Tick", tick(kit, small), "peak", -4.0),
            ("Rally Slam", slam(kit, ir), "lufs", -14.0),
            ("Powerup Berserker", fury(kit, ir), "lufs", -14.0),
            ("Fury Crackle", fury_crackle(small), "peak", -8.0),
            # Main menu: seen constantly over the menu music, so well under it. The UI taps
            # peak at -6 to -8 dBFS (about -21 LUFS); the clank matches them, the entrance
            # sits far below.
            ("Menu Entrance Whoosh", menu_whoosh(small), "peak", -20.0),
            ("Menu Title Tinks", title_tinks(small), "peak", -19.0)]
    jobs += [("Knight Clank %d" % (v + 1), knight_clank(small, v), "peak", -8.0) for v in range(3)]
    for name, y, mode, target in jobs:
        y = M.mono(y)
        y = M.highpass(np.stack([y, y], 1), 45, False)
        if mode == "lufs":
            y = B.master(y, target, loop=False)
        else:
            y = y * db(target - M.true_peak_db(y))
        o = B.write(y, name, B.SFX_DIR, ogg=False, mono=True)
        print(name, dict(seconds=round(len(y) / RATE, 3), lufs=round(M.lufs(M.mono(y)), 2),
                         true_peak=round(M.true_peak_db(y), 2)))
        if copy:
            shutil.copyfile(o, B.SFX_DIR / (name + ".wav"))


if __name__ == "__main__":
    main()
