"""Transcribes the old Logic bounces (MenuTheme, Transformation, Adventure Mode Next Level) so
the new score can grow out of them. Prints, per eighth note, the strongest pitch in the melody
range, the bass pitch, and the overall pitch-class profile (key estimate).

    python -I analyze_old.py <wav> <bpm>
"""
import site, sys
sys.path.append(site.getusersitepackages())
import numpy as np
from scipy.io import wavfile

NAMES = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"]
MAJOR = np.array([6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88])
MINOR = np.array([6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17])


def name(m):
    m = int(round(m))
    return NAMES[m % 12] + str(m // 12 - 1)


def main():
    path, bpm = sys.argv[1], float(sys.argv[2])
    rate, x = wavfile.read(path)
    x = x.astype(np.float64)
    if x.ndim == 2:
        x = x.mean(1)
    x /= np.abs(x).max() + 1e-9
    step = 60.0 / bpm / 2  # eighth notes
    n = 1 << 14
    fr = np.fft.rfftfreq(n, 1 / rate)
    midi_of_bin = 69 + 12 * np.log2(np.maximum(fr, 1e-9) / 440)
    chroma_total = np.zeros(12)
    rows = []
    t = 0.0
    while t + step <= len(x) / rate + 1e-6:
        a = int(t * rate)
        seg = x[a:a + int(step * rate)]
        if len(seg) < 512:
            break
        S = np.abs(np.fft.rfft(seg * np.hanning(len(seg)), n))
        def peak(lo, hi):
            sel = (fr >= lo) & (fr <= hi)
            # Harmonic sum so the fundamental wins over its overtones.
            best, bm = 0, None
            for m in np.arange(np.ceil(69 + 12 * np.log2(lo / 440)), np.floor(69 + 12 * np.log2(hi / 440)) + 1):
                f = 440 * 2 ** ((m - 69) / 12)
                sc = sum(S[np.argmin(np.abs(fr - k * f))] / k ** 0.8 for k in range(1, 5))
                if sc > best:
                    best, bm = sc, m
            return bm, best
        mel, ms = peak(330, 1600)
        bass, bs = peak(55, 260)
        energy = np.sqrt(np.mean(seg ** 2))
        sel = (fr > 60) & (fr < 4000)
        c = np.zeros(12)
        np.add.at(c, np.round(midi_of_bin[sel]).astype(int) % 12, S[sel] ** 2)
        chroma_total += c
        rows.append((t, name(mel) if energy > 0.02 else "-", name(bass) if energy > 0.02 else "-", 20 * np.log10(energy + 1e-9)))
        t += step
    for k, (t, m, b, e) in enumerate(rows):
        bar, beat = divmod(k, 8)
        print("bar %d beat %.1f  t=%5.2f  melody %-4s bass %-4s  %5.1f dB" % (bar + 1, beat / 2 + 1, t, m, b, e))
    prof = chroma_total / chroma_total.sum()
    best = []
    for tonic in range(12):
        for mode, tmpl in (("major", MAJOR), ("minor", MINOR)):
            best.append((np.corrcoef(np.roll(tmpl, tonic), prof)[0, 1], NAMES[tonic] + " " + mode))
    best.sort(reverse=True)
    print("key estimate:", ", ".join("%s (%.2f)" % (k, r) for r, k in best[:3]))
    print("pitch classes:", " ".join("%s %.0f%%" % (NAMES[i], 100 * prof[i]) for i in np.argsort(-prof)[:8]))


if __name__ == "__main__":
    main()
