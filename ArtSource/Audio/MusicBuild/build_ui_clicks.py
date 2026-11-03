"""Build the 3D style's UI clicks, all synthesized (no samples, no downloads).

    python -I build_ui_clicks.py [--no-copy]

Writes mono WAVs into ./out and copies the 16-bit versions into Assets/Resources/SFX/.
They replace the old JDSherbert square-wave cursors that were left over from the pixel style.

- UI Tap 1-4.wav      TabSelect, the default click of every UIFactory button and every tab.
                      A soft tock on a small lacquered wooden button with a faint brass
                      glint on top. Heard constantly, so it is short (about 0.09 s), dark and
                      quiet, and the four round-robin variants differ only slightly.
- Page Turn 1-3.wav   PageTurn, How to Play next / previous. A parchment page flicking over:
                      a papery swish with a few crinkles and the soft flap as it lands.
- Play Select 1-2.wav PlaySelect, the Play button and picking an adventure level. A firmer,
                      lower wooden tock with a small brass bell on a rising open fifth (D, A,
                      no third, like the War Horn, so it sits over any chord of the score).

Levels: integrated LUFS means little on a 0.1 s click, so each file is set by its loudest
50 ms window (K-weighted, "short50" below), then held under a -3 dBTP peak by the limiter.
Knight Clank measures about -16.5 that way and the old square cursors -13.4 (tab), -20.2
(page) and -14.3 (select). The tap goes a little under the clank (-17) since it is heard
all the time, the page at -19, the Play confirm at -15.
Seeded, so every run makes the same files.
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
from build_warhorn import metal, blend_reverb, fade_tail

SEED = B.SEED + 131


def env(t, attack, tau):
    return np.clip(t / attack, 0, 1) * np.exp(-t / tau)


def wood(f0, L, seed, decays=(0.030, 0.016, 0.009, 0.006), ratios=(1, 2.32, 3.98, 6.1)):
    """A small struck block of hardwood: a few inharmonic modes that die very fast, so it
    reads as a tock rather than a note."""
    r = np.random.default_rng(seed)
    n = int(L * RATE)
    t = np.arange(n) / RATE
    y = np.zeros(n)
    for k, (q, d) in enumerate(zip(ratios, decays)):
        f = f0 * q * (1 + r.uniform(-0.01, 0.01))
        y += (0.6 ** k) * np.sin(2 * np.pi * f * t + r.uniform(0, 2 * np.pi)) * np.exp(-t / d)
    return y * np.clip(t / 0.0006, 0, 1)


def ui_tap(ir, variant):
    """The everyday button click: a fingertip-on-wood tock, a tiny noise contact and a quiet
    brass glint. No pitch you could hum along to, so hundreds of them never get tiring."""
    L = 0.09
    n = int(L * RATE)
    t = np.arange(n) / RATE
    f0 = [930, 980, 1010, 955][variant]
    tock = wood(f0, L, SEED + variant)
    contact = M.swept_noise(L, [(1800, 6500, env(t, 0.0003, 0.0025))], SEED + 10 + variant)
    body = np.sin(2 * np.pi * 240 * t) * env(t, 0.0008, 0.010)
    glint = metal(f0 * 3.1, [1, 1.53, 2.41], [0.018, 0.010, 0.006], L, SEED + 20 + variant, [1, 0.4, 0.2])
    x = (tock / (np.abs(tock).max() + 1e-9)
         + 0.35 * contact / (np.abs(contact).max() + 1e-9)
         + 0.30 * body
         + 0.10 * glint / (np.abs(glint).max() + 1e-9))
    buf = Buffer(L, False)
    buf.place(x, 0.0)
    return fade_tail(blend_reverb(buf.x, ir, 0.06), 0.02)


def crinkles(L, count, seed, start, end):
    r = np.random.default_rng(seed)
    n = int(L * RATE)
    y = np.zeros(n)
    for _ in range(count):
        k = int(r.uniform(start, end) * RATE)
        m = int(r.uniform(0.0015, 0.004) * RATE)
        burst = r.standard_normal(m) * np.exp(-np.arange(m) / (m / 3))
        y[k:k + m] += burst[: n - k] * r.uniform(0.3, 1.0)
    return M.mono(M.highpass(np.stack([y, y], 1), 2500, False))


def page_turn(ir, variant):
    """A parchment page flicking over: the swish rises as the page lifts, a few papery crinkles,
    and a soft low flap as it settles."""
    L = 0.30
    n = int(L * RATE)
    t = np.arange(n) / RATE
    peak = [0.075, 0.085, 0.07][variant]
    # Swish envelope: a quick lift, then a sweep that falls away.
    sw = np.where(t < peak, (t / peak) ** 1.6, np.exp(-(t - peak) / 0.045))
    swish = M.swept_noise(L, [(700, 1800, sw * 0.6), (1800, 4500, sw), (4500, 9000, sw * 0.35 * np.exp(-t / 0.08))],
                          SEED + 30 + variant)
    cr = crinkles(L, 9, SEED + 40 + variant, 0.02, peak + 0.06)
    land = peak + 0.035
    tl = np.clip(t - land, 0, None)
    flap = M.swept_noise(L, [(120, 600, (t >= land) * env(tl, 0.003, 0.025))], SEED + 50 + variant)
    x = (swish / (np.abs(swish).max() + 1e-9)
         + 0.30 * cr / (np.abs(cr).max() + 1e-9)
         + 0.55 * flap / (np.abs(flap).max() + 1e-9))
    buf = Buffer(L, False)
    buf.place(x, 0.0)
    return fade_tail(blend_reverb(buf.x, ir, 0.08), 0.05)


def play_select(ir, variant):
    """The bigger confirm: a firmer, lower wooden tock (two quick knocks, like a seal pressed
    down) under a small brass bell rising a fifth, D6 to A6 or A5 to D6."""
    L = 0.45
    n = int(L * RATE)
    t = np.arange(n) / RATE
    buf = Buffer(L, False)
    f0 = [700, 660][variant]
    tock = wood(f0, L, SEED + 60 + variant, decays=(0.045, 0.022, 0.012, 0.007))
    contact = M.swept_noise(L, [(1500, 6000, env(t, 0.0003, 0.003))], SEED + 70 + variant)
    body = np.sin(2 * np.pi * 170 * t) * env(t, 0.001, 0.018)
    knock = (tock / (np.abs(tock).max() + 1e-9) + 0.35 * contact / (np.abs(contact).max() + 1e-9) + 0.45 * body)
    buf.place(knock, 0.0)
    buf.place(knock * 0.35, 0.018)
    bells = [(86, 93), (81, 86)][variant]
    for k, m in enumerate(bells):
        b = metal(M.midi_hz(m), [1, 2.76, 5.40], [0.16, 0.06, 0.02], L - 0.01, SEED + 80 + 10 * variant + k,
                  [1, 0.3, 0.1])
        buf.place(b / (np.abs(b).max() + 1e-9) * (0.32 if k == 0 else 0.40), 0.006 + 0.055 * k)
    return fade_tail(blend_reverb(buf.x, ir, 0.22), 0.12)


def short50(y):
    """Loudest 50 ms window, K-weighted, in LUFS-like units (mono)."""
    k = M._k_weight(np.stack([y, y], 1) / np.sqrt(2))[:, 0]
    w = int(0.05 * RATE)
    e = np.convolve(k ** 2, np.ones(w) / w, "valid")
    return 10 * np.log10(2 * e.max() + 1e-20) - 0.691


def main():
    copy = "--no-copy" not in sys.argv
    small = M.make_ir("meadow", SEED)
    jobs = [("UI Tap %d" % (v + 1), ui_tap(small, v), -17.0) for v in range(4)]
    jobs += [("Page Turn %d" % (v + 1), page_turn(small, v), -19.0) for v in range(3)]
    jobs += [("Play Select %d" % (v + 1), play_select(small, v), -15.0) for v in range(2)]
    for name, y, target in jobs:
        y = M.mono(M.highpass(np.stack([M.mono(y)] * 2, 1), 60, False))
        y = y * db(target - short50(y))
        if M.true_peak_db(np.stack([y, y], 1)) > -3.0:
            y = M.limit(np.stack([y, y], 1), -3.5, False, 0.001)[0][:, 0]
        o = B.write(np.stack([y, y], 1), name, B.SFX_DIR, ogg=False, mono=True)
        print(name, dict(seconds=round(len(y) / RATE, 3), short50=round(short50(y), 2),
                         true_peak=round(M.true_peak_db(np.stack([y, y], 1)), 2)))
        if copy:
            shutil.copyfile(o, B.SFX_DIR / (name + ".wav"))


if __name__ == "__main__":
    main()
