"""Build the Adventure level complete sting from the score's palette (CC0 VSCO 2 CE samples).

    python -I build_level_complete.py <vsco folder> [--no-copy]

<vsco folder> is where fetch_vsco.py put the samples (outside the repo). Writes one mono WAV
into ./out and copies it to Assets/Resources/SFX/Adventure Level Complete.wav (the 3D style;
the pixel style keeps the old chiptune "Adventure Mode Next Level").

Adventure Level Complete.wav  the knight reaches the goal. The score's horn call, the
    "knight's move" (D up to A, then the half step to Bb), now pushed on up to C and D over
    the bVI-bVII-I lift of the theme's B section (Bb, C, D), landing on a D major chord (the
    Picardy landing, as in the Rally Slam and the queen's launch). Horns lead, trombones
    play the chords, cellos swell under the landing, toms push the climb, then timpani, big
    drum, crash and a soft gong land it, with a glockenspiel and harp shimmer on top. Eighths
    at 132 BPM, landing at 0.795 s, about 2.9 s with the hall tail.

Mastered like the War Horn set (mono, -14 LUFS target through build_music.master, true peak
under -1 dBTP), so it sits with Powerup War Horn, Rally Slam and Transform Break.
"""
import site, sys
sys.path.append(site.getusersitepackages())
import shutil
from pathlib import Path
import numpy as np

sys.path.insert(0, str(Path(__file__).parent))
import music_lib as M
from music_lib import RATE, Buffer
import build_music as B
from build_warhorn import blend_reverb, fade_tail

SEED = B.SEED + 91
NAME = "Adventure Level Complete"
E = 60.0 / 132 / 2  # one eighth at the score's 132 BPM
LAND = 3.5 * E      # the D major landing (0.795 s)


def level_complete(kit, ir):
    L = 2.9
    buf = Buffer(L, False)
    rng = np.random.default_rng(SEED)
    loose = lambda: rng.uniform(-0.004, 0.004)  # a hand-played feel, a few ms

    def brass(bank, m, t, hold, rel, p, g, fade=0.006):
        buf.place(M.pan(M.mono(M.shape(bank.get(m), hold, rel, fade_in=fade)), p) * g, max(0.0, t + loose()))

    def stab(bank, m, t, p, g, ring=0.5):
        buf.place(M.pan(M.mono(M.ring(bank.get(m), ring)), p) * g, max(0.0, t + loose()))

    # 0: the hit that says "you made it", D minor still: trombones D2 D3, horn D4, big drum.
    stab(kit.tbn_stac, 50, 0.0, -0.15, 0.5)
    stab(kit.tbn_stac, 38, 0.0, 0.15, 0.35)
    stab(kit.horn_stac, 62, 0.0, 0.05, 0.45)
    buf.place(M.ring(kit.hit("mallet_f"), 1.0) * 0.6, 0.0)
    buf.place(M.ring(kit.timp_d, 0.9) * 0.4, 0.0)

    # The call climbs: A4 (eighth), Bb4 (dotted-eighth feel over Bb), C5 (over C), then D5.
    lead = [(69, 1 * E, 0.85 * E), (70, 1.5 * E, 0.95 * E), (72, 2.5 * E, 0.95 * E)]
    for m, t, d in lead:
        brass(kit.horn, m, t, d, 0.07, 0.1, 0.5)
        stab(kit.horn_stac, m, t, -0.1, 0.22, 0.3)
    # Trombone chords under the climb: Bb (Bb2 F3) then C (C3 G3), short and heavy.
    for (lo, hi), t in [((46, 53), 1.5 * E), ((48, 55), 2.5 * E)]:
        brass(kit.tbn, lo, t, 0.9 * E, 0.06, -0.2, 0.42)
        brass(kit.tbn, hi, t, 0.9 * E, 0.06, 0.2, 0.36)
        stab(kit.tbn_stac, lo, t, 0.0, 0.25, 0.3)
    # Toms push each step, a soft timpani roll swells into the landing.
    for k, t in enumerate([1 * E, 1.5 * E, 2.5 * E, 3 * E]):
        buf.place(M.ring(kit.hit("tom_f" if k >= 2 else "tom"), 0.5) * (0.35 + 0.08 * k), t + loose())
    roll = M.ring(kit.timp_roll, 3 * E)
    roll = roll * np.linspace(0.05, 1, len(roll))[:, None] ** 2
    buf.place(roll * 0.4, LAND - len(roll) / RATE)
    # A quick harp run up D major into the landing (D4 F#4 A4 D5 F#5 A5), lifting the colour.
    for k, m in enumerate((62, 66, 69, 74, 78, 81)):
        buf.place(M.pan(M.mono(M.ring(kit.harp.get(m), 1.2)), -0.3 + 0.12 * k) * 0.22,
                  LAND - 6 * 0.045 + k * 0.045)

    # The landing: D major. Horns D4 F#4 A4 and the lead on D5, trombones D3 A3, cellos D2 D3.
    hold, rel = 1.05, 0.55
    brass(kit.horn, 74, LAND, hold, rel, 0.1, 0.62, fade=0.004)
    for m, p, g in [(62, -0.2, 0.42), (66, 0.25, 0.36), (69, -0.05, 0.38)]:
        brass(kit.horn, m, LAND, hold, rel, p, g, fade=0.004)
    for m, p, g in [(50, -0.15, 0.5), (57, 0.15, 0.4)]:
        brass(kit.tbn, m, LAND, hold, rel, p, g, fade=0.004)
    for m in (74, 62, 66):
        stab(kit.horn_stac, m, LAND, 0.0, 0.3, 0.5)
    stab(kit.tbn_stac, 50, LAND, 0.0, 0.4, 0.5)
    for m, p in [(38, -0.1), (50, 0.1)]:
        buf.place(M.pan(M.mono(M.shape(kit.cello.get(m), hold + 0.1, rel + 0.2, fade_in=0.05)), p) * 0.4, LAND)
    buf.place(M.ring(kit.hit("mallet_ff"), 1.6) * 0.85, LAND)
    buf.place(M.ring(kit.timp_d, 1.6) * 0.6, LAND)
    buf.place(M.ring(kit.hit("cym"), 1.9) * 0.24, LAND)
    buf.place(M.ring(kit.hit("gong_soft"), 2.0) * 0.18, LAND + 0.01)
    # The shimmer: glockenspiel D6 then A6 just after the hit, a bright crown on the chord.
    for k, m in enumerate((86, 93)):
        buf.place(M.pan(M.mono(M.ring(kit.glock.get(m), 1.4)), 0.2 - 0.4 * k) * 0.2, LAND + 0.03 + 0.07 * k)

    x = blend_reverb(buf.x, ir, 0.42)
    return fade_tail(x, 0.45)


def main():
    kit = B.Kit(sys.argv[1])
    copy = "--no-copy" not in sys.argv
    ir = M.make_ir("castle", SEED)
    y = M.mono(level_complete(kit, ir))
    y = M.highpass(np.stack([y, y], 1), 45, False)
    y = B.master(y, -14.0, loop=False)
    o = B.write(y, NAME, B.SFX_DIR, ogg=False, mono=True)
    print(NAME, dict(seconds=round(len(y) / RATE, 3), lufs=round(M.lufs(M.mono(y)), 2),
                     true_peak=round(M.true_peak_db(y), 2)))
    if copy:
        shutil.copyfile(o, B.SFX_DIR / (NAME + ".wav"))


if __name__ == "__main__":
    main()
