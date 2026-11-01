"""DSP and instruments for the Knight's Gauntlet score (see README.md).

Everything renders at 44.1 kHz stereo float. Loops are rendered into a circular buffer exactly
the loop's length: notes and reverb tails that run past the end wrap onto the start, filters
run over a wrapped pre-roll, and the reverb is a circular convolution, so the files loop with
no seam. Stings render into a plain buffer with room for their tail.
"""
import site, sys
sys.path.append(site.getusersitepackages())
import re
from pathlib import Path
import numpy as np
from scipy import signal
from scipy.io import wavfile

RATE = 44100


def db(x):
    return 10 ** (x / 20.0)


def midi_hz(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


def pan(x, p):
    """Equal-power pan of a mono signal, p in -1..1."""
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], 1)


def mono(x):
    return x.mean(1) if x.ndim == 2 else x


class Buffer:
    """A stereo buffer. circular=True wraps writes past the end onto the start (for loops)."""

    def __init__(self, seconds, circular):
        self.n = int(round(seconds * RATE))
        self.circular = circular
        self.x = np.zeros((self.n, 2))

    def place(self, sig, t):
        sig = sig if sig.ndim == 2 else np.stack([sig, sig], 1)
        start = int(round(t * RATE))
        if self.circular:
            start %= self.n
            pos = 0
            while pos < len(sig):
                k = min(len(sig) - pos, self.n - start)
                self.x[start:start + k] += sig[pos:pos + k]
                pos += k
                start = 0
        else:
            if start < 0:
                sig = sig[-start:]
                start = 0
            k = min(len(sig), self.n - start)
            if k > 0:
                self.x[start:start + k] += sig[:k]


def filt(sos, x, circular, preroll=6.0):
    """IIR filter; on a loop it runs over a wrapped pre-roll so the start is already settled."""
    if not circular:
        return signal.sosfilt(sos, x, axis=0)
    p = min(int(preroll * RATE), len(x))
    y = signal.sosfilt(sos, np.concatenate([x[-p:], x]), axis=0)
    return y[p:]


def lowpass(x, f, circular, order=2):
    return filt(signal.butter(order, f, "low", fs=RATE, output="sos"), x, circular)


def highpass(x, f, circular, order=2):
    return filt(signal.butter(order, f, "high", fs=RATE, output="sos"), x, circular)


# ------------------------------------------------------------------------------------- reverb
ROOMS = {
    # name: (seconds, predelay s, [(lo Hz, hi Hz, T60 s)], early-reflection spacing)
    "meadow": (1.6, 0.012, [(60, 400, 0.9), (400, 1500, 0.8), (1500, 5000, 0.6), (5000, 12000, 0.35)]),
    "bridge": (2.6, 0.025, [(60, 400, 1.7), (400, 1500, 1.5), (1500, 5000, 1.1), (5000, 12000, 0.6)]),
    "castle": (4.2, 0.040, [(60, 400, 3.0), (400, 1500, 2.6), (1500, 5000, 1.8), (5000, 12000, 0.9)]),
    "hall":   (3.2, 0.030, [(60, 400, 2.2), (400, 1500, 2.0), (1500, 5000, 1.4), (5000, 12000, 0.7)]),
    "magic":  (3.0, 0.020, [(60, 400, 2.0), (400, 1500, 2.0), (1500, 5000, 1.8), (5000, 12000, 1.4)]),
}


def make_ir(room, seed):
    """Stereo algorithmic impulse: decorrelated noise in bands, each with its own decay."""
    seconds, predelay, bands = ROOMS[room]
    r = np.random.default_rng(seed)
    n = int(seconds * RATE)
    t = np.arange(n) / RATE
    ir = np.zeros((n, 2))
    for lo, hi, t60 in bands:
        sos = signal.butter(2, (lo, hi), "band", fs=RATE, output="sos")
        for ch in range(2):
            ir[:, ch] += signal.sosfilt(sos, r.standard_normal(n)) * np.exp(-6.91 * t / t60)
    ir *= (1 - np.exp(-t / 0.015))[:, None]
    ir = np.concatenate([np.zeros((int(predelay * RATE), 2)), ir])
    return ir / np.sqrt((ir ** 2).sum(0).mean())


def reverb(x, ir, circular):
    out = np.zeros_like(x)
    n = len(x)
    for ch in range(2):
        y = signal.fftconvolve(x[:, ch], ir[:, ch])
        if circular:
            for k in range(0, len(y), n):
                seg = y[k:k + n]
                out[:len(seg), ch] += seg
        else:
            out[:, ch] = y[:n]
    return out


# ------------------------------------------------------------------------------------- loudness
def _k_weight(x):
    def shelf(f0, gain, q):
        A = 10 ** (gain / 40)
        w = 2 * np.pi * f0 / RATE
        al = np.sin(w) / (2 * q)
        b = [A * ((A + 1) + (A - 1) * np.cos(w) + 2 * np.sqrt(A) * al), -2 * A * ((A - 1) + (A + 1) * np.cos(w)),
             A * ((A + 1) + (A - 1) * np.cos(w) - 2 * np.sqrt(A) * al)]
        a = [(A + 1) - (A - 1) * np.cos(w) + 2 * np.sqrt(A) * al, 2 * ((A - 1) - (A + 1) * np.cos(w)),
             (A + 1) - (A - 1) * np.cos(w) - 2 * np.sqrt(A) * al]
        return np.array(b) / a[0], np.array(a) / a[0]

    def hp(f0, q):
        w = 2 * np.pi * f0 / RATE
        al = np.sin(w) / (2 * q)
        b = [(1 + np.cos(w)) / 2, -(1 + np.cos(w)), (1 + np.cos(w)) / 2]
        a = [1 + al, -2 * np.cos(w), 1 - al]
        return np.array(b) / a[0], np.array(a) / a[0]
    b1, a1 = shelf(1681.974450955533, 3.999843853973347, 0.7071752369554196)
    b2, a2 = hp(38.13547087602444, 0.5003270373238773)
    return signal.lfilter(b2, a2, signal.lfilter(b1, a1, x, axis=0), axis=0)


def lufs(x):
    """BS.1770 integrated loudness (K-weighted, gated)."""
    x = x if x.ndim == 2 else np.stack([x, x], 1) / np.sqrt(2)
    y = _k_weight(x)
    blk, hop = int(0.4 * RATE), int(0.1 * RATE)
    if len(y) < blk:
        y = np.concatenate([y, np.zeros((blk - len(y), 2))])
    z = np.array([np.mean(y[i:i + blk] ** 2, 0).sum() for i in range(0, len(y) - blk + 1, hop)])
    l = -0.691 + 10 * np.log10(z + 1e-20)
    g = z[l > -70]
    rel = -0.691 + 10 * np.log10(g.mean()) - 10
    g = z[(l > -70) & (l > rel)]
    return -0.691 + 10 * np.log10(g.mean())


def true_peak_db(x):
    up = signal.resample_poly(x, 4, 1, axis=0)
    return 20 * np.log10(np.abs(up).max() + 1e-12)


# ------------------------------------------------------------------------------------- samples
NOTE = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}


def _name_midi(fn, offset):
    m = re.search(r"_([A-G]#?)(-?\d)(?=[_.])", fn)
    return 12 * (int(m.group(2)) + 1) + NOTE[m.group(1)] + offset


class SampleBank:
    """Pitched samples from one folder. VSCO names most notes one octave low (C3 = middle C),
    so offset=12 for those; the harp uses standard names (offset 0). Checked by samplecheck.py."""

    def __init__(self, folder, offset, pattern="*.wav", lo=None, hi=None, midi_of=None):
        self.notes = {}
        for p in sorted(Path(folder).glob(pattern)):
            m = midi_of(p.name) if midi_of else _name_midi(p.name, offset)
            if (lo is not None and m < lo) or (hi is not None and m > hi):
                continue
            rate, x = wavfile.read(p)
            x = x.astype(np.float64)
            env = np.abs(x).max(1)
            on = max(0, int(np.argmax(env > env.max() * 0.02)) - int(0.004 * RATE))
            x = x[on:]
            x /= np.abs(x).max() + 1e-9
            self.notes.setdefault(m, []).append(x)
        assert self.notes, folder
        self._cache = {}
        self._rr = {}

    def get(self, midi):
        """The nearest sample, resampled onto the exact pitch (round robins alternate)."""
        base = min(self.notes, key=lambda k: (abs(k - midi), k < midi))
        i = self._rr.get(midi, 0)
        self._rr[midi] = i + 1
        variants = self.notes[base]
        key = (midi, i % len(variants))
        if key not in self._cache:
            x = variants[i % len(variants)]
            ratio = 2 ** ((midi - base) / 12.0)
            if abs(ratio - 1) < 1e-6:
                y = x
            else:
                t = np.arange(int(len(x) / ratio)) * ratio
                y = np.stack([np.interp(t, np.arange(len(x)), x[:, c]) for c in range(2)], 1)
            self._cache[key] = y
        return self._cache[key]


def shape(x, dur, release, fade_in=0.0, max_len=None):
    """Cuts a sample to dur seconds plus a release fade (sustained instruments)."""
    n = int((dur + release) * RATE)
    if max_len is not None:
        n = min(n, int(max_len * RATE))
    y = x[:n].copy()
    if len(y) < n:
        y = np.concatenate([y, np.zeros((n - len(y), 2))])
    k = int(dur * RATE)
    r = max(1, len(y) - k)
    env = np.ones(len(y))
    env[k:] = np.cos(np.linspace(0, np.pi / 2, r)) ** 2
    if fade_in > 0:
        f = int(fade_in * RATE)
        env[:f] *= np.sin(np.linspace(0, np.pi / 2, f)) ** 2
    return y * env[:, None]


def ring(x, max_len):
    """Plucked or struck samples: let them ring, fading out at max_len."""
    n = min(len(x), int(max_len * RATE))
    y = x[:n].copy()
    f = min(n, int(0.25 * RATE))
    y[-f:] *= np.linspace(1, 0, f)[:, None]
    return y


# ------------------------------------------------------------------------------------- synth strings
def swept_noise(seconds, bands, seed):
    """Noise whose spectrum moves smoothly: fixed band-passed noise layers, each with its own
    gain curve. bands: list of (lo Hz, hi Hz, gain array of len seconds*RATE)."""
    r = np.random.default_rng(seed)
    n = int(seconds * RATE)
    y = np.zeros(n)
    for lo, hi, g in bands:
        sos = signal.butter(2, (lo, hi), "band", fs=RATE, output="sos")
        y += signal.sosfilt(sos, r.standard_normal(n)) * g[:n]
    return y


def ks_string(freq, length, decay, damping, pluck_at, seed, brightness=4200, body=True):
    """Karplus-Strong plucked string (the Kumari yazh recipe), mono, at the exact pitch."""
    r = np.random.default_rng(seed)
    period = RATE / freq - damping
    L = int(period)
    actual = RATE / (L + damping)
    xs = np.arange(L) / L
    line = np.where(xs < pluck_at, xs / pluck_at, (1 - xs) / (1 - pluck_at)) + r.uniform(-0.10, 0.10, L)
    line -= line.mean()
    count = int(length * RATE * actual / freq) + 2
    loss = 0.001 ** (1.0 / (actual * decay))
    excite = np.zeros(count)
    excite[:L] = line
    a = np.zeros(L + 2)
    a[0] = 1.0
    a[L] = -loss * (1 - damping)
    a[L + 1] = -loss * damping
    y = signal.lfilter([1.0], a, excite)
    t_new = np.arange(int(length * RATE)) * (actual / freq)
    y = np.interp(t_new, np.arange(count), y)
    y = signal.sosfilt(signal.butter(1, brightness, "low", fs=RATE, output="sos"), y)
    if body:
        y += 0.3 * signal.sosfilt(signal.butter(2, (170, 420), "band", fs=RATE, output="sos"), y)
    f = min(len(y), int(0.08 * RATE))
    y[-f:] *= np.linspace(1, 0, f)
    return y / (np.abs(y).max() + 1e-9)


def lute(midi, dur, seed):
    """Lute stand-in: a warm, fairly damped string with a wooden body; rings past the note a little."""
    f = midi_hz(midi)
    length = min(max(dur + 0.6, 0.8), 3.0)
    y = ks_string(f, length, decay=1.8 if f < 200 else 1.3, damping=0.45, pluck_at=0.22, seed=seed, brightness=3600)
    # Course of two strings: the second a few cents sharp, slightly late.
    y2 = ks_string(f * 1.0025, length, decay=1.6, damping=0.45, pluck_at=0.25, seed=seed + 7, brightness=3400)
    y = 0.6 * y + 0.4 * np.concatenate([np.zeros(int(0.003 * RATE)), y2])[:len(y)]
    return y


def harpsichord(midi, dur, seed):
    """Harpsichord stand-in: two bright 8' strings plucked near the end, damped when the key lifts."""
    f = midi_hz(midi)
    length = dur + 0.12
    s = ks_string(f, max(length, 0.3), decay=3.5, damping=0.12, pluck_at=0.07, seed=seed, brightness=9000, body=False)
    s2 = ks_string(f * 1.0035, max(length, 0.3), decay=3.2, damping=0.14, pluck_at=0.09, seed=seed + 3, brightness=8000, body=False)
    y = 0.55 * s + 0.45 * s2
    y = signal.sosfilt(signal.butter(2, 140, "high", fs=RATE, output="sos"), y)
    k = int(dur * RATE)
    if k < len(y):
        y[k:] *= np.linspace(1, 0, len(y) - k)  # the damper
    # A tiny quill click at the start.
    click = np.random.default_rng(seed + 11).uniform(-1, 1, int(0.004 * RATE)) * np.linspace(1, 0, int(0.004 * RATE))
    y[:len(click)] += 0.25 * click
    return y / (np.abs(y).max() + 1e-9)


def limit(x, ceiling_db, circular, lookahead=0.004):
    """Look-ahead peak limiter: the gain each sample needs, spread by a min filter over the
    look-ahead window and smoothed by a box of the same width, so peaks are caught without
    pumping. On a loop the filters wrap, so the seam is untouched."""
    from scipy.ndimage import minimum_filter1d, uniform_filter1d
    c = db(ceiling_db)
    peak = np.abs(x).max(1)
    g = np.minimum(1.0, c / (peak + 1e-12))
    w = 2 * int(lookahead * RATE) + 1
    mode = "wrap" if circular else "nearest"
    g = minimum_filter1d(g, w, mode=mode)
    g = uniform_filter1d(g, w, mode=mode)
    g = np.minimum(g, minimum_filter1d(np.minimum(1.0, c / (peak + 1e-12)), 3, mode=mode))
    return x * g[:, None], float(20 * np.log10(g.min()))
