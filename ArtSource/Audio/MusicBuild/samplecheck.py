"""Checks the downloaded samples: onset time and measured pitch against the note in the filename.

    python -I samplecheck.py <download folder>
"""
import site, sys
sys.path.append(site.getusersitepackages())
import re
from pathlib import Path
import numpy as np
from scipy.io import wavfile

NOTE = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}

def name_midi(fn):
    m = re.search(r"_([A-G]#?)(-?\d)_", fn + "_")
    return None if not m else 12 * (int(m.group(2)) + 1) + NOTE[m.group(1)]

def f0(x, rate, lo=40, hi=2000):
    x = x - x.mean()
    n = len(x)
    spec = np.fft.rfft(x * np.hanning(n), 4 * n)
    ac = np.fft.irfft(np.abs(spec) ** 2)[: n]
    a, b = int(rate / hi), int(rate / lo)
    lag = a + np.argmax(ac[a:b])
    # Prefer the shortest lag with nearly the same correlation (avoids octave-down errors).
    for k in (4, 3, 2):
        l2 = int(round(lag / k))
        if l2 >= a and ac[l2] > 0.9 * ac[lag]:
            lag = l2
            break
    return rate / lag

def main():
    root = Path(sys.argv[1]) / "f32"
    for p in sorted(root.rglob("*.wav")):
        rate, x = wavfile.read(p)
        m = x.mean(1)
        env = np.abs(m)
        onset = int(np.argmax(env > env.max() * 0.05))
        seg = m[onset + int(0.05 * rate): onset + int(0.25 * rate)]
        exp = name_midi(p.name)
        if len(seg) < 1000:
            continue
        f = f0(seg, rate)
        meas = 69 + 12 * np.log2(f / 440)
        tag = "" if exp is None else "%+.2f st vs name" % (meas - exp)
        print("%-60s onset %4.0f ms  %7.1f Hz  midi %6.2f  %s" % (str(p.relative_to(root))[:60], onset * 1000 / rate, f, meas, tag))

if __name__ == "__main__":
    main()
