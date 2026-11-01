"""Build the Knight's Gauntlet score: every music cue, launch phrase and sting from one theme.

    python -I build_music.py <vsco folder> [--only Menu,Trap,stings] [--no-copy]

<vsco folder> is where fetch_vsco.py put the VSCO 2 CE samples (outside the repo). Writes WAVs,
Oggs, report.json and timeline.txt into ./out, and copies the Oggs (music) and WAVs (the
transformation SFX) into Assets/Resources/SFX/. Seeded, so every run makes the same files.
See README.md for the musical design, and analyze_old.py for the old Logic bounces it grew from.
"""
import site, sys
sys.path.append(site.getusersitepackages())
import json, re, shutil, subprocess
from pathlib import Path
import numpy as np
from scipy.io import wavfile

sys.path.insert(0, str(Path(__file__).parent))
import music_lib as M
from music_lib import RATE, Buffer, db

HERE = Path(__file__).resolve().parent
OUT = HERE / "out"
PROJECT = HERE.parents[2]
MUSIC_DIR = PROJECT / "Assets" / "Resources" / "SFX" / "Music"
SFX_DIR = PROJECT / "Assets" / "Resources" / "SFX"
SEED = 1415  # any fixed number keeps the build reproducible

MUSIC_LUFS = -16.0
TP_CEILING = -1.0

# ============================================================================== the theme
# Home key D minor (aeolian), with the phrygian-dominant colour (Eb over D, F# against F)
# for the castle and the queen. Both colours come from the old MenuTheme, which sits in G
# phrygian dominant: a rising G B D then Eb F G hook over a D pedal (see README).
#
# The motif is the knight's move: up a fifth, then a half step (D A Bb), an L-shape in sound
# whose last step is the old theme's Eb-over-G sigh moved to D. Melody: (beat, beats, midi).
THEME_A = [
    [(0, 1.5, 62), (1.5, 0.5, 69), (2, 2, 70)],             # Dm  D . A Bb   the call
    [(0, 1, 69), (1, 1, 67), (2, 1, 65), (3, 1, 67)],        # Bb  A G F G    falling, as the old theme's answer
    [(0, 1.5, 72), (1.5, 0.5, 70), (2, 1, 69), (3, 1, 65)],  # F   C . Bb A F
    [(0, 1, 67), (1, 1, 64), (2, 2, 69)],                    # A   G E A      half cadence
    [(0, 1.5, 62), (1.5, 0.5, 69), (2, 2, 70)],             # Dm  the call again
    [(0, 1, 74), (1, 1, 72), (2, 1, 70), (3, 1, 69)],        # Gm  D' C Bb A  from above
    [(0, 1, 67), (1, 1, 64), (2, 1, 61), (3, 1, 64)],        # A7  G E C# E
    [(0, 3, 62)],                                            # Dm  home
]
CHORDS_A = ["Dm", "Bb", "F", "A", "Dm", "Gm", "A7", "Dm"]

THEME_B = [
    [(0, 1, 65), (1, 1, 70), (2, 2, 74)],                   # Bb  F Bb D'   bVI: the lift
    [(0, 1, 76), (1, 1, 74), (2, 2, 72)],                   # C   E' D' C   bVII
    [(0, 1.5, 74), (1.5, 0.5, 69), (2, 2, 65)],             # Dm  D' . A F
    [(0, 1, 64), (1, 1, 73), (2, 2, 69)],                   # A   E C# A
    [(0, 1.5, 70), (1.5, 0.5, 69), (2, 1, 67), (3, 1, 65)], # Bb  Bb . A G F   (A held under: the old theme's pedal)
    [(0, 1, 67), (1, 1, 69), (2, 2, 72)],                   # C   G A C
    [(0, 1, 74), (1, 1, 70), (2, 1, 67), (3, 1, 70)],       # Gm  D' Bb G Bb
    [(0, 2, 69), (2, 1, 64), (3, 1, 61)],                   # A   A E C#   (leads home)
]
CHORDS_B = ["Bb", "C", "Dm", "A", "Bb", "C", "Gm", "A"]

INTRO_CHORDS = ["Dm", "Dm", "Bb", "A"]
TAG = [[(0, 1.5, 62), (1.5, 0.5, 69), (2, 2, 70)], [], [(0, 1.5, 62), (1.5, 0.5, 69), (2, 1, 70), (3, 1, 73)], []]
TAG_CHORDS = ["Dm", "Bb", "Gm", "A"]

# The queen's version: bar 3 of A goes to Eb (bII, the Neapolitan) with the tune bent onto it.
BOSS_A_CHORDS = ["Dm", "Bb", "Eb", "A", "Dm", "Gm", "A7", "Dm"]
BOSS_A_BAR3 = [(0, 1.5, 72), (1.5, 0.5, 70), (2, 1, 67), (3, 1, 63)]  # C . Bb G Eb

CHORD_TONES = {  # root (bass register, D2..D3) and intervals
    "Dm": (38, [0, 3, 7]), "Bb": (46, [0, 4, 7]), "F": (41, [0, 4, 7]), "A": (45, [0, 4, 7]),
    "A7": (45, [0, 4, 7, 10]), "Gm": (43, [0, 3, 7]), "C": (48, [0, 4, 7]), "Eb": (39, [0, 4, 7]),
}
D_MINOR = [2, 4, 5, 7, 9, 10, 0]        # D E F G A Bb C
D_HARMONIC = [2, 4, 5, 7, 9, 10, 1]     # with C#, over the A chords
D_PHRYGIAN_DOM = [2, 3, 6, 7, 9, 10, 0]  # D Eb F# G A Bb C, the old theme's mode on D


def scale_for(chord):
    return D_HARMONIC if chord in ("A", "A7") else D_MINOR


def degree_walk(start, steps, scale):
    """The note `steps` scale degrees from `start` (which should be in the scale)."""
    pcs = sorted(set(scale))
    notes = [m for m in range(20, 100) if m % 12 in pcs]
    i = min(range(len(notes)), key=lambda k: abs(notes[k] - start))
    return notes[max(0, min(len(notes) - 1, i + steps))]


def voicing(chord, low=50, n=4, sus=False):
    """Chord tones from `low` upward. sus=True swaps the third for the fourth (the bridge's
    suspended, unresolved colour)."""
    root, ivs = CHORD_TONES[chord]
    if sus:
        ivs = [0, 5, 7] + ([10] if 10 in ivs else [])
    pcs = [(root + i) % 12 for i in ivs]
    out, m = [], low
    while len(out) < n:
        if m % 12 in pcs:
            out.append(m)
        m += 1
    return out


# The old Transformation sting's spinning eighth-note figure (B minor at 134 BPM: A B D G B A
# F# F# | F# B A F# B F# F# E), written as scale steps from the chord root so it can run under
# any chord. It becomes the Trap and Battle cello ostinato.
SPIN = [-1, 0, 2, -2, 0, -1, -3, -3, -3, 0, -1, -3, 0, -3, -3, -4]

# ============================================================================== forms
CUES = {
    # tempo, sections, drive (0 brooding, 1 urgent, 2 aggressive), boss flag, biome variants
    "Menu":      dict(tempo=88,  form=["A", "B", "A2"], drive=0, boss=False, biomes=["menu"]),
    "Trap":      dict(tempo=132, form=["intro", "A", "B", "A2", "tag"], drive=1, boss=False,
                      biomes=["meadow", "bridge", "castle"]),
    "Battle":    dict(tempo=132, form=["intro", "A", "B", "A2", "A3"], drive=2, boss=False,
                      biomes=["meadow", "bridge", "castle"]),
    "Adventure": dict(tempo=66,  form=["intro", "A", "Bq", "tag"], drive=0, boss=False,
                      biomes=["meadow", "bridge", "castle"]),
    "Boss":      dict(tempo=132, form=["intro", "A", "A2", "B", "A3"], drive=2, boss=True, biomes=["castle"]),
}


def section(name, boss):
    """(melody bars, chord per bar, flags) for one section of the form."""
    if name == "intro":
        return [[] for _ in INTRO_CHORDS], INTRO_CHORDS, {}
    if name == "tag":
        return TAG, TAG_CHORDS, {}
    if name in ("A", "A2", "A3"):
        mel, ch = list(THEME_A), list(CHORDS_A)
        if boss:
            mel[2], ch = BOSS_A_BAR3, list(BOSS_A_CHORDS)
        return mel, ch, {"counter": name == "A2", "harmony": name == "A3"}
    if name == "B":
        return THEME_B, CHORDS_B, {"pedal": True}
    if name == "Bq":  # adventure's quiet middle: the B tune only every other bar, low
        return [bar if i % 2 == 1 else [] for i, bar in enumerate(THEME_B)], CHORDS_B, {"pedal": True}
    raise KeyError(name)


class Note:
    __slots__ = ("t", "dur", "midi", "vel", "role", "kind")

    def __init__(self, t, dur, midi, vel, role, kind=""):
        self.t, self.dur, self.midi, self.vel, self.role, self.kind = t, dur, midi, vel, role, kind


def compose(cue_name, spec):
    """The note list for one cue (seconds), shared by all its biome arrangements."""
    rng = np.random.default_rng(SEED + sum(map(ord, cue_name)))
    beat = 60.0 / spec["tempo"]
    bar_s = 4 * beat
    drive, boss = spec["drive"], spec["boss"]
    calm = drive == 0
    notes, bars = [], []

    def j():  # small, seeded timing looseness
        return rng.normal(0, 0.005)

    bar = 0
    for sec in spec["form"]:
        mel, chords, flags = section(sec, boss)
        for i, (mbar, chord) in enumerate(zip(mel, chords)):
            bars.append((bar, chord, sec, i))
            t0 = bar * bar_s
            root, _ = CHORD_TONES[chord]
            scale = scale_for(chord)
            # Lead: the theme. Adventure plays it an octave down, brooding.
            shift = -12 if cue_name == "Adventure" else 0
            for b, d, m in mbar:
                notes.append(Note(t0 + b * beat + j(), d * beat, m + shift, rng.uniform(0.85, 1.0), "lead"))
                if flags.get("harmony"):
                    if boss:
                        h = m - 12  # unison power: the trombones an octave under the horns
                    else:
                        h = degree_walk(m, -2, scale)  # a third below
                    notes.append(Note(t0 + b * beat + j(), d * beat, h + shift, rng.uniform(0.75, 0.9), "harmony"))
            # Pad: low strings (organ in the castle) holding the chord, all through calm cues
            # and in the fuller sections of the driving ones.
            if calm or flags.get("counter") or flags.get("pedal") or sec == "A3":
                for m in voicing(chord, 50, 3):
                    notes.append(Note(t0 + j(), 4 * beat, m, 0.8, "pad"))
            # The old theme's held pedal: an A under the second half of B, pulling home.
            if flags.get("pedal") and i >= 4:
                notes.append(Note(t0, 4 * beat, 45, 0.9, "pad", "pedal"))
            # Ostinato: the old Transformation figure on cellos (eighths; sixteenths in Battle and Boss).
            if drive >= 1:
                start = degree_walk(root + 12 if root + 12 <= 52 else root, 0, scale)
                if drive == 1:
                    half = SPIN[(i % 2) * 8:(i % 2) * 8 + 8]
                    for e, st in enumerate(half):
                        notes.append(Note(t0 + e * 0.5 * beat + j(), 0.45 * beat, degree_walk(start, st, scale),
                                          (0.95 if e % 2 == 0 else 0.7) * rng.uniform(0.9, 1.0), "osti"))
                else:
                    for e, st in enumerate(SPIN):
                        notes.append(Note(t0 + e * 0.25 * beat + j(), 0.22 * beat, degree_walk(start, st, scale),
                                          (1.0 if e % 4 == 0 else 0.72) * rng.uniform(0.9, 1.0), "osti"))
            elif calm and sec != "A2":
                # Brooding cues: a slow low-string figure, root and fifth, two per bar.
                fifth = degree_walk(root + 12 if root + 12 <= 52 else root, 4, scale)
                for b, m in ((0, root + 12 if root + 12 <= 52 else root), (2, fifth)):
                    notes.append(Note(t0 + b * beat + j(), 1.6 * beat, m, 0.75, "osti"))
            # Bass
            if calm:
                notes.append(Note(t0 + j(), 4 * beat, root, 0.9, "bass"))
            elif drive == 1:
                for b in (0, 1.5, 2, 3):
                    notes.append(Note(t0 + b * beat + j(), 0.45 * beat, root, 1.0 if b in (0, 2) else 0.7, "bass"))
            else:
                for e in range(8):
                    notes.append(Note(t0 + e * 0.5 * beat + j(), 0.4 * beat, root, 1.0 if e % 2 == 0 else 0.75, "bass"))
            # Drums, as abstract strokes the biome turns into instruments
            if drive >= 1:
                notes.append(Note(t0, 0, 0, 1.0, "drum", "boom"))
                notes.append(Note(t0 + 2 * beat + j(), 0, 0, 0.85, "drum", "boom2"))
                toms = [0, 3, 4, 6] if drive == 1 else [0, 2, 3, 4, 6, 7]
                for e in toms:
                    notes.append(Note(t0 + e * 0.5 * beat + j(), 0, 0, 0.9 if e in (0, 4) else 0.65, "drum", "tom"))
                if i == len(mel) - 1 or (drive == 2 and i % 4 == 3):
                    for e in range(8):  # a sixteenth-note roll on the toms into the next phrase
                        notes.append(Note(t0 + 2 * beat + e * 0.25 * beat + j(), 0, 0, 0.5 + 0.06 * e, "drum", "fill"))
            elif sec != "intro" and i % 2 == 0:
                notes.append(Note(t0, 0, 0, 0.7, "drum", "boom"))
            if drive == 2 and sec in ("A", "A2", "A3"):
                for b in (0, 1.5):  # brass stabs: hit, then the and of two
                    for m in voicing(chord, 50, 3):
                        notes.append(Note(t0 + b * beat + j(), 0.3 * beat, m, 0.95 if b == 0 else 0.8, "stab"))
            if i == 0 and sec != "intro":
                notes.append(Note(t0, 0, root, 1.0, "accent", "section"))  # timpani (and gong in the castle)
            elif drive >= 1 and i % 2 == 0:
                notes.append(Note(t0, 0, root, 0.75, "accent", "timp"))
            # The castle's phrygian sigh: Eb falling to D over the D minor bars.
            if chord == "Dm" and sec != "intro":
                notes.append(Note(t0 + 2 * beat + j(), 1.0 * beat, 51, 0.8, "sigh"))
                notes.append(Note(t0 + 3 * beat + j(), 1.0 * beat, 50, 0.7, "sigh"))
            bar += 1
    return notes, bars, bar * bar_s


# ============================================================================== instruments
class Kit:
    def __init__(self, vsco):
        f = Path(vsco) / "f32"
        S = M.SampleBank
        self.horn = S(f / "Brass/F Horn/sus", 12)
        self.horn_stac = S(f / "Brass/F Horn/stac", 12)
        self.tbn = S(f / "Brass/Tenor Trombone/sus", 12)
        self.tbn_stac = S(f / "Brass/Tenor Trombone/stac", 12)
        self.vc_spic = S(f / "Strings/Cello Section/spic", 12)
        self.cb_spic = S(f / "Strings/Solo Contrabass/Spic", 12, lo=30, hi=60)
        self.vc_pizz = S(f / "Strings/Cello Section/pizzT", 12)
        self.viola = S(f / "Strings/Viola Section/susvib", 12)
        self.cello = S(f / "Strings/Cello Section/susvib", 12)
        self.vc_trem = S(f / "Strings/Cello Section/trem", 12, pattern="*_v1_1.wav")
        # trem_D2 has no energy at D3 (146 Hz) and sounds an octave high; leave it out.
        self.vc_trem.notes.pop(50, None)
        self.vln_trem = S(f / "Strings/Violin Section/Trem", 12)
        self.harp = S(f / "Strings/Harp", 0, lo=30, hi=100)
        self.glock = S(f / "Percussion/Glock", 12)
        # The organ's files are numbered: manual number - 86 and pedal number - 28 give the
        # MIDI note (measured by samplecheck.py: 122 -> C2, 131 -> A2, pedal 064 -> C2).
        def organ_midi(name):
            n = int(re.search(r"_(\d{3})_", name).group(1))
            return n - 86 if "Man3" in name else n - 28
        self.organ = S(f / "Keys/Organ/Quiet", 0, pattern="NT5_Man3Quiet_*.wav", midi_of=organ_midi, lo=36, hi=80)
        p = f / "Percussion"
        v1 = f / "VSCO 1 Percussion"

        def one(path):
            if not path.exists():
                path = Path(str(path).replace(".wav", "_Sum.wav"))
            rate, x = wavfile.read(path)
            x = x.astype(np.float64)
            if x.ndim == 1:
                x = np.stack([x, x], 1)
            env = np.abs(x).max(1)
            on = max(0, int(np.argmax(env > env.max() * 0.02)) - int(0.003 * RATE))
            x = x[on:]
            return x / (np.abs(x).max() + 1e-9)
        self.one = one
        # Timpani: principal tones measured at 122.3 Hz (drum 2) and 142.7 Hz (drum 3), retuned
        # onto A2 and D3.
        self.timp_d = self._retune(one(p / "Timpani/Timpani3_Hit_v3_rr1_Sum.wav"), 146.83 / 142.7)
        self.timp_a = self._retune(one(p / "Timpani/Timpani2_Hit_v3_rr1_Sum.wav"), 110.0 / 122.3)
        self.timp_roll = self._retune(one(p / "Timpani/Rolls/Timpani3_Roll_v3_rr1_Sum.wav"), 146.83 / 142.7)
        g = v1 / "drums/other/ethnic/giant"
        t = v1 / "drums/tenor/tenor_lower"
        self.perc = {k: [one(x) for x in v] for k, v in {
            "hand_ff": [g / "hand/EthnicLargeHand_hit_ff_1.wav", g / "hand/EthnicLargeHand_hit_ff_2.wav"],
            "hand_f": [g / "hand/EthnicLargeHand_hit_f_1.wav", g / "hand/EthnicLargeHand_hit_f_2.wav"],
            "hand_mp": [g / "hand/EthnicLargeHand_hit_mp_1.wav", g / "hand/EthnicLargeHand_hit_mp_2.wav"],
            "mallet_ff": [g / "mallet/EthnicLargeMallet_hit_ff_1.wav", g / "mallet/EthnicLargeMallet_hit_ff_2.wav"],
            "mallet_f": [g / "mallet/EthnicLargeMallet_hit_f_1.wav", g / "mallet/EthnicLargeMallet_hit_mf_1.wav"],
            "sticks": [g / "sticks/EthnicLargeSticks_hit_mf_1.wav", g / "sticks/EthnicLargeSticks_hit_mf_2.wav"],
            "sticks_soft": [g / "sticks/EthnicLargeSticks_hit_mp_1.wav", g / "sticks/EthnicLargeSticks_hit_p_1.wav"],
            "tom": [t / "tenor_mf_1.wav", t / "tenor_mf_2.wav", t / "tenor_mf_3.wav"],
            "tom_f": [t / "tenor_f_1.wav", t / "tenor_f_2.wav", t / "tenor_ff_1.wav"],
            "tom_soft": [t / "tenor_mp_1.wav", t / "tenor_mf_4.wav"],
            "gong": [v1 / "varMetal/Gong/gong_hit_mp.wav"], "gong_soft": [v1 / "varMetal/Gong/gong_hit_p.wav"],
            "cym": [p / "cymbal-crash1_mp_rr1.wav"], "swell": [p / "susCymb1-cresc-Short_v1.wav"],
        }.items()}
        self._rr = {}

    @staticmethod
    def _retune(x, ratio):
        t = np.arange(int(len(x) / ratio)) * ratio
        return np.stack([np.interp(t, np.arange(len(x)), x[:, c]) for c in range(2)], 1)

    def hit(self, name):
        v = self.perc[name]
        i = self._rr.get(name, 0)
        self._rr[name] = i + 1
        return v[i % len(v)]


def render_note(kit, inst, n, seed):
    """One note on one instrument -> stereo array starting at the note's onset."""
    d = n.dur
    if inst == "horn":
        if d < 0.25:
            return M.ring(kit.horn_stac.get(n.midi), 0.6)
        return M.shape(kit.horn.get(n.midi), d * 0.97, 0.25, fade_in=0.03)
    if inst == "horn_stab":
        return M.ring(kit.horn_stac.get(n.midi), 0.55)
    if inst == "tbn":
        if d < 0.25:
            return M.ring(kit.tbn_stac.get(n.midi), 0.6)
        return M.shape(kit.tbn.get(n.midi), d * 0.97, 0.25, fade_in=0.03)
    if inst == "tbn_stab":
        return M.ring(kit.tbn_stac.get(n.midi), 0.55)
    if inst == "vc_spic":
        return M.ring(kit.vc_spic.get(n.midi), max(0.25, d * 1.4))
    if inst == "cb_spic":
        return M.ring(kit.cb_spic.get(n.midi), max(0.3, d * 1.4))
    if inst == "vc_pizz":
        return M.ring(kit.vc_pizz.get(n.midi), 1.6)
    if inst in ("viola", "cello", "vc_trem", "vln_trem"):
        bank = {"viola": kit.viola, "cello": kit.cello, "vc_trem": kit.vc_trem, "vln_trem": kit.vln_trem}[inst]
        return M.shape(bank.get(n.midi), d, 0.5, fade_in=0.15)
    if inst == "organ":
        return M.shape(kit.organ.get(n.midi), d, 0.6, fade_in=0.08)
    if inst == "harp":
        return M.ring(kit.harp.get(n.midi), max(2.5, d + 1.5))
    if inst == "glock":
        return M.ring(kit.glock.get(n.midi), 2.5)
    raise KeyError(inst)


# Per arrangement: which instrument plays each role, its pan, and its level (dB, active RMS,
# against the lead). Danger escalates meadow -> bridge -> castle.
ARR = {
    # The knight before the journey: horns over a low string pad, drums held back.
    "menu": dict(room="hall", wet=0.32,
                 roles={"lead": ("horn", 0.05, 0), "harmony": ("tbn", -0.2, -4), "pad": ("cello", 0.1, -7),
                        "bass": ("cello", -0.1, -8), "osti": ("vc_pizz", -0.3, -10)},
                 pad_also=("viola", 0.35, -9),
                 drums={"boom": ("mallet_f", 0.0, 0.7)}, drum_db=-14,
                 accent={"section": ["timp", "gong_soft"], "timp": ["timp"]}, accent_db=-11),
    # The start of the road: tense but with some hope. Horns, driving cellos, hand drums.
    "meadow": dict(room="meadow", wet=0.22,
                   roles={"lead": ("horn", 0.05, 0), "harmony": ("horn", -0.25, -5), "osti": ("vc_spic", -0.3, -4),
                          "bass": ("cb_spic", 0.15, -7), "pad": ("cello", 0.25, -10), "stab": ("horn_stab", 0.1, -5)},
                   calm_osti=("vc_pizz", -0.3, -6),
                   drums={"boom": ("hand_ff", 0.0, 1.0), "boom2": ("hand_f", 0.05, 0.8), "tom": ("tom_soft", -0.25, 0.55),
                          "fill": ("tom", -0.2, 0.6)}, drum_db=-9,
                   accent={"section": ["timp"], "timp": ["timp"]}, accent_db=-10),
    # Precarious: thinner, suspended, wind. The tune moves to the cellos; violins tremble high.
    "bridge": dict(room="bridge", wet=0.30,
                   roles={"lead": ("cello", 0.0, 0), "harmony": ("viola", -0.25, -5), "osti": ("vc_spic", -0.35, -7),
                          "bass": ("cb_spic", 0.15, -10), "pad": ("vln_trem", 0.3, -12), "stab": ("tbn_stab", 0.1, -8)},
                   calm_osti=("vc_pizz", -0.3, -8),
                   wind_db=-15,
                   drums={"boom": ("sticks_soft", 0.0, 0.9), "fill": ("tom_soft", -0.2, 0.45)}, drum_db=-13,
                   accent={"section": ["timp"]}, accent_db=-13),
    # The castle interior: organ and low brass, a big hall, the phrygian sigh.
    "castle": dict(room="castle", wet=0.42,
                   roles={"lead": ("horn", 0.05, 0), "harmony": ("tbn", -0.25, -3), "osti": ("vc_spic", -0.3, -5),
                          "bass": ("cb_spic", 0.15, -7), "pad": ("organ", 0.0, -9), "stab": ("tbn_stab", 0.1, -4),
                          "sigh": ("tbn", -0.35, -9)},
                   calm_osti=("vc_trem", -0.3, -9),
                   drums={"boom": ("mallet_ff", 0.0, 1.0), "boom2": ("mallet_f", 0.0, 0.8), "tom": ("tom", -0.25, 0.6),
                          "fill": ("tom_f", -0.2, 0.7)}, drum_db=-8,
                   accent={"section": ["timp", "gong"], "timp": ["timp"]}, accent_db=-9),
}


def arrangement(name, cue):
    a = json.loads(json.dumps(ARR[name]))  # copy
    if cue["drive"] == 0 and "calm_osti" in a:
        a["roles"]["osti"] = a["calm_osti"]
    if cue["drive"] == 0 and name != "menu":
        # Brooding: drums only as a distant pulse.
        a["drum_db"] = a.get("drum_db", -10) - 5
    return a


def active_rms_db(x):
    m = M.mono(x)
    fr = int(0.25 * RATE)
    n = len(m) // fr
    if n == 0:
        return -120
    e = np.sqrt((m[:n * fr].reshape(n, fr) ** 2).mean(1))
    e = e[e > e.max() * db(-30)]
    return 20 * np.log10(np.sqrt((e ** 2).mean()) + 1e-12)


def wind(length, circular, seed):
    """Wind for the bridge: bands of noise whose levels drift on LFOs that complete whole
    cycles over the loop."""
    n = int(round(length * RATE))
    t = np.arange(n) / n
    r = np.random.default_rng(seed)
    y = np.zeros((n, 2))
    for k, (lo, hi, cyc) in enumerate([(180, 500, 3), (400, 1100, 5), (900, 2400, 7)]):
        band = r.standard_normal((n, 2))
        band = M.filt(M.signal.butter(2, (lo, hi), "band", fs=RATE, output="sos"), band, circular)
        gust = 0.55 + 0.45 * np.sin(2 * np.pi * cyc * t + k * 1.7) * np.sin(2 * np.pi * (cyc + 2) * t + k)
        y += band * gust[:, None]
    return y


def render_parts(kit, notes, length, arr, circular, seed):
    parts = {}
    rng = np.random.default_rng(seed)
    for idx, n in enumerate(notes):
        if n.role == "drum":
            spec = arr.get("drums", {}).get(n.kind)
            if not spec:
                continue
            name, p, lvl = spec
            buf = parts.setdefault("drum", Buffer(length, circular))
            buf.place(M.pan(M.mono(kit.hit(name)), p) * n.vel * lvl, n.t)
            continue
        if n.role == "accent":
            acc = (arr.get("accent") or {}).get(n.kind)
            if not acc:
                continue
            buf = parts.setdefault("accent", Buffer(length, circular))
            for kind in acc:
                if kind == "timp":
                    sig = kit.timp_a if n.midi % 12 in (9, 4) else kit.timp_d  # A (and E) take the A drum
                    buf.place(M.pan(M.mono(M.ring(sig, 3.0)), -0.1) * n.vel, n.t)
                else:
                    buf.place(M.pan(M.mono(M.ring(kit.hit(kind), 6.0)), 0.2) * 0.6 * n.vel, n.t)
            continue
        role = arr["roles"].get(n.role)
        if not role:
            continue
        inst, p, _ = role
        midi = n.midi
        if n.role == "pad" and inst == "vln_trem":
            midi += 12  # the bridge's tremolo sits high and thin
        if n.role == "lead" and inst == "cello" and midi >= 60:
            midi -= 12  # the bridge's tune in the cellos' singing register
        if n.role == "harmony" and inst == "viola" and midi > 76:
            midi -= 12
        sig = render_note(kit, inst, Note(n.t, n.dur, midi, n.vel, n.role), idx)
        stereo_inst = inst in ("viola", "cello", "vc_trem", "vln_trem", "organ")
        sig = sig * 0.7 if stereo_inst else M.pan(M.mono(sig), p + rng.uniform(-0.05, 0.05))
        buf = parts.setdefault(n.role, Buffer(length, circular))
        buf.place(sig * n.vel, n.t)
        if n.role == "pad" and arr.get("pad_also") and n.kind != "pedal":
            inst2, _, _ = arr["pad_also"]
            sig2 = render_note(kit, inst2, Note(n.t, n.dur, n.midi + 12, n.vel, "pad"), idx)
            parts.setdefault("pad2", Buffer(length, circular)).place(sig2 * 0.7 * n.vel, n.t)
    if arr.get("wind_db") is not None:
        parts["wind"] = wind(length, circular, seed)
    return {k: v.x if isinstance(v, Buffer) else v for k, v in parts.items()}


SENDS = {"lead": 0.35, "harmony": 0.4, "osti": 0.3, "bass": 0.12, "pad": 0.6, "pad2": 0.6, "drum": 0.3,
         "accent": 0.55, "stab": 0.4, "sigh": 0.8, "wind": 0.2}


def mix(parts, arr, circular, seed, ir_cache):
    targets = {k: v[2] for k, v in arr["roles"].items()}
    targets["drum"] = arr.get("drum_db", -10)
    targets["accent"] = arr.get("accent_db", -10)
    targets["wind"] = arr.get("wind_db", -15)
    if arr.get("pad_also"):
        targets["pad2"] = arr["pad_also"][2]
    out = {}
    for k, x in parts.items():
        if not np.any(x):
            continue
        out[k] = x * db(targets.get(k, -8) - active_rms_db(x))
    room = arr["room"]
    if room not in ir_cache:
        ir_cache[room] = M.make_ir(room, seed)
    send = sum(out[k] * SENDS.get(k, 0.3) for k in out)
    wet = M.reverb(send, ir_cache[room], circular)
    dry = sum(out.values())
    y = dry + wet * arr["wet"] / max(1e-9, np.sqrt(np.mean(wet ** 2))) * np.sqrt(np.mean(dry ** 2))
    # Phone speakers have no sub: keep the weight in the low mids, trim the rumble under 45 Hz.
    y = M.highpass(y, 45, circular)
    return y


LIMIT_LOG = []


def master(y, target=MUSIC_LUFS, loop=True):
    # Gain to the target, then a look-ahead limiter holds true peaks under the ceiling; a
    # couple of passes because limiting lowers the loudness a little.
    gr = 0.0
    for _ in range(3):
        probe = np.concatenate([y, y]) if loop else y
        y = y * db(target - M.lufs(probe))
        y, g = M.limit(y, TP_CEILING - 0.5, circular=loop)
        gr = min(gr, g)
    tp = M.true_peak_db(y)
    if tp > TP_CEILING:
        y = y * db(TP_CEILING - tp)
    LIMIT_LOG.append(round(gr, 2))
    return y


def seam_report(y):
    step = float(np.abs(y[0] - y[-1]).max())
    biggest = float(np.abs(np.diff(y, axis=0)).max())
    w = int(0.1 * RATE)
    around = np.concatenate([y[-2 * RATE:], y[:2 * RATE]])
    rms = [20 * np.log10(np.sqrt(np.mean(around[i:i + w] ** 2)) + 1e-12) for i in range(0, len(around) - w + 1, w)]
    body = [20 * np.log10(np.sqrt(np.mean(y[i:i + w] ** 2)) + 1e-12) for i in range(0, len(y) - w, w)]
    return dict(seam_step=round(step, 5), largest_step=round(biggest, 5),
                seam_rms_range=[round(min(rms), 1), round(max(rms), 1)],
                body_rms_min_p10_p90_max=[round(float(np.min(body)), 1), round(float(np.percentile(body, 10)), 1),
                                          round(float(np.percentile(body, 90)), 1), round(float(np.max(body)), 1)])


def low_mid_share(y):
    """Fraction of energy in 150 Hz - 2 kHz (what a phone speaker can actually play)."""
    m = M.mono(y)
    S = np.abs(np.fft.rfft(m[: 1 << 21] if len(m) > 1 << 21 else m)) ** 2
    f = np.fft.rfftfreq(min(len(m), 1 << 21), 1 / RATE)
    return round(float(S[(f > 150) & (f < 2000)].sum() / S[f > 20].sum()), 3)


def write(y, name, folder, ogg=True, mono=False):
    OUT.mkdir(exist_ok=True)
    if mono:
        y = M.mono(y)
    wav = OUT / (name + ".wav")
    wavfile.write(wav, RATE, y.astype(np.float32))
    if ogg:
        o = OUT / (name + ".ogg")
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis", "-q:a", "4", str(o)], check=True)
        return o
    w16 = OUT / (name + "_16.wav")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "pcm_s16le", str(w16)], check=True)
    return w16


# ============================================================================== stings
def launch(kit, cue_name, spec, ir_cache):
    """Drawing the sword: one bar that rises and lands on the loop's first downbeat (bar end).
    The rising run is the old Adventure Mode Next Level sting (a fast climb that lands on A),
    moved into D harmonic minor and carried on up to D."""
    beat = 60.0 / spec["tempo"]
    bar = 4 * beat
    length = bar + 3.5
    boss, drive = spec["boss"], spec["drive"]
    notes = []
    parts = {}
    if drive == 0:
        # Setting out quietly: the call low on a horn over a timpani roll, landing on D.
        for b, d, m in [(0, 1.5, 50), (1.5, 0.5, 57), (2, 2, 58)]:
            notes.append(Note(b * beat, d * beat, m, 0.8, "lead"))
        notes.append(Note(bar, 3 * beat, 50, 0.85, "lead"))
        for m in voicing("Dm", 38, 3):
            notes.append(Note(bar, 3 * beat, m, 0.8, "pad"))
        arr = dict(room="hall", wet=0.4, roles={"lead": ("horn", 0.0, 0), "pad": ("cello", 0.0, -6)})
        parts = render_parts(kit, notes, length, arr, False, SEED)
        r = Buffer(length, False)
        roll = M.ring(kit.timp_roll, bar + 0.05)
        r.place(roll * np.linspace(0.15, 0.6, len(roll))[:, None], 0.0)
        r.place(M.ring(kit.timp_d, 3.0) * 0.7, bar)
        r.place(M.ring(kit.hit("gong_soft"), 3.0) * 0.4, bar)
        parts["drum"] = r.x
        arr["drum_db"] = -8
        return mix(parts, arr, False, SEED, ir_cache), bar
    # Beat 1: a sforzando fifth on horns and trombones (the blade leaving the scabbard).
    for m in (50, 57, 62):
        notes.append(Note(0, 0.6 * beat, m, 1.0, "stab"))
    # Beats 2-4: the run, twelve sixteenths from A2 to E4 in D harmonic minor (phrygian
    # dominant for the queen), on cellos doubled by horns for the top half.
    scale = D_PHRYGIAN_DOM if boss else D_HARMONIC
    m = 45
    for k in range(12):
        notes.append(Note(beat + k * 0.25 * beat, 0.24 * beat, m, 0.6 + 0.035 * k, "osti"))
        if k >= 6:
            notes.append(Note(beat + k * 0.25 * beat, 0.24 * beat, m + 12, 0.5 + 0.04 * k, "harmony"))
        m = degree_walk(m, 1, scale)
    # The landing: D minor (D major for the queen, as the old theme's tonic) on everything.
    land = voicing("Dm", 50, 4) if not boss else [50, 54, 57, 62]
    for m in land:
        notes.append(Note(bar, 2.5 * beat, m, 1.0, "lead"))
    notes.append(Note(bar, 2.5 * beat, 38, 1.0, "bass"))
    arr = dict(room="castle" if boss else "hall", wet=0.35,
               roles={"lead": ("horn", 0.0, 0), "stab": ("tbn_stab", 0.0, -2), "osti": ("vc_spic", -0.3, -3),
                      "harmony": ("horn_stab", 0.2, -6), "bass": ("cb_spic", 0.1, -6)})
    parts = render_parts(kit, notes, length, arr, False, SEED)
    r = Buffer(length, False)
    for k in range(8):  # toms driving the run
        r.place(M.pan(M.mono(kit.hit("tom")), -0.2) * (0.4 + 0.07 * k), beat + k * 0.375 * beat)
    r.place(M.ring(kit.hit("mallet_ff"), 3.0), 0.0)
    r.place(M.ring(kit.timp_d, 3.0), bar)
    r.place(M.ring(kit.hit("mallet_ff"), 3.0) * 0.9, bar)
    r.place(M.ring(kit.hit("gong" if boss else "cym"), 5.0) * 0.45, bar)
    parts["drum"] = r.x
    arr["drum_db"] = -5
    return mix(parts, arr, False, SEED, ir_cache), bar


def game_over(kit, ir_cache):
    """The call turned over: A F E D, falling low on the horns, the low D ringing out."""
    beat = 60.0 / 72
    length = 4 * beat + 4.0
    notes = []
    for b, d, m in [(0, 1, 57), (1, 1, 53), (2, 1, 52), (3, 3, 50)]:
        notes.append(Note(b * beat, d * beat, m, 0.9, "lead"))
    for b, ch, d in [(0, "Gm", 2), (2, "A", 1), (3, "Dm", 4)]:
        for m in voicing(ch, 45, 3):
            notes.append(Note(b * beat, d * beat, m, 0.75, "pad"))
        notes.append(Note(b * beat, d * beat, CHORD_TONES[ch][0], 0.9, "bass"))
    arr = dict(room="castle", wet=0.38, roles={"lead": ("horn", 0.0, 0), "pad": ("cello", 0.0, -5), "bass": ("tbn", 0.1, -8)})
    parts = render_parts(kit, notes, length, arr, False, SEED + 5)
    b = Buffer(length, False)
    b.place(M.ring(kit.timp_d, 3.0) * 0.9, 3 * beat)
    b.place(M.ring(kit.hit("gong_soft"), 4.0) * 0.5, 3 * beat)
    parts["drum"] = b.x
    arr["drum_db"] = -8
    return mix(parts, arr, False, SEED + 5, ir_cache)


def transform_set(kit, ir_cache):
    """A whoosh as the form starts, a seamless hum while it lasts, a soft poof when it ends.
    The whoosh's harp spins the old Transformation sting's figure (now in D minor) before it
    climbs."""
    rng = np.random.default_rng(SEED + 9)
    out = {}
    L = 1.6
    n = int(L * RATE)
    t = np.arange(n) / RATE
    centres = 300 * (5000 / 300) ** (np.arange(8) / 7)
    sweep = np.log(300 * (5000 / 300) ** np.clip(t / 0.9, 0, 1))
    bands = [(c / 1.4, min(c * 1.4, 18000), np.exp(-((sweep - np.log(c)) / 0.35) ** 2)) for c in centres]
    y = M.swept_noise(L, bands, SEED + 21)
    env = np.clip(t / 0.6, 0, 1) ** 2 * np.exp(-np.clip(t - 0.6, 0, None) / 0.18)
    y *= env
    buf = Buffer(L + 2.0, False)
    buf.place(M.pan(y / (np.abs(y).max() + 1e-9) * 0.5, 0), 0)
    run = [degree_walk(74, s, D_MINOR) for s in SPIN[:6]] + [74, 77, 81, 86]
    for k, m in enumerate(run):
        buf.place(M.pan(M.mono(M.ring(kit.harp.get(m), 1.2)), -0.5 + k * 0.1) * (0.35 + 0.04 * k), 0.1 + k * 0.055)
    buf.place(M.pan(M.mono(M.ring(kit.glock.get(86), 2.0)), 0.2) * 0.5, 0.7)
    x = buf.x
    w = M.reverb(x, ir_cache.setdefault("magic", M.make_ir("magic", SEED)), False)
    out["Transform Whoosh"] = x + 0.35 * w / (np.sqrt(np.mean(w ** 2)) + 1e-9) * np.sqrt(np.mean(x ** 2))

    # Hum: D3, A3, D4 with slow beating; every frequency and LFO is a whole number of cycles
    # over the 4 s loop, so the file loops seamlessly.
    L = 4.0
    n = int(L * RATE)
    t = np.arange(n) / RATE
    q = lambda f: round(f * L) / L
    hum = np.zeros((n, 2))
    # Each tone has a quieter twin 0.25 Hz above it (one slow beat per loop), so the hum
    # shimmers gently; kept quiet so the beat stays a sway, not a pulse, once played mono.
    for f, a in [(146.83, 1.0), (220.0, 0.6), (293.66, 0.45), (440.0, 0.12)]:
        for det, g in ((0.0, 1.0), (0.25, 0.25)):
            ff = q(f) + det
            tone = np.sin(2 * np.pi * ff * t) + 0.25 * np.sin(2 * np.pi * 2 * ff * t) + 0.08 * np.sin(2 * np.pi * 3 * ff * t)
            hum += (a * g * tone)[:, None]
    hum *= (0.8 + 0.2 * np.sin(2 * np.pi * 1 * t / L))[:, None]
    sh = rng.standard_normal((n, 2))
    sh = M.highpass(M.lowpass(sh, 9000, True), 4000, True)
    sh *= (0.5 + 0.5 * np.sin(2 * np.pi * 2 * t / L + 1.0))[:, None]
    hum = hum / np.abs(hum).max() + 0.05 * sh / (np.abs(sh).max() + 1e-9)
    hum = M.lowpass(hum, 2500, True)
    w = M.reverb(hum, ir_cache["magic"], True)
    out["Transform Hum"] = hum + 0.3 * w / (np.sqrt(np.mean(w ** 2)) + 1e-9) * np.sqrt(np.mean(hum ** 2))

    # Poof: a darkening burst and the harp stepping down A F D (D minor now).
    L = 0.9
    n = int(L * RATE)
    t = np.arange(n) / RATE
    bands = [(lo, hi, np.exp(-t / tau)) for lo, hi, tau in
             [(150, 400, 0.20), (400, 900, 0.14), (900, 2000, 0.09), (2000, 4500, 0.05)]]
    y = M.swept_noise(L, bands, SEED + 22)
    y *= np.clip(t / 0.008, 0, 1)
    buf = Buffer(L + 1.6, False)
    buf.place(M.pan(y / (np.abs(y).max() + 1e-9) * 0.7, 0), 0)
    for k, m in enumerate([69, 65, 62]):
        buf.place(M.pan(M.mono(M.ring(kit.harp.get(m), 1.0)), 0.1 - 0.1 * k) * 0.4, 0.04 + 0.07 * k)
    x = buf.x
    w = M.reverb(x, ir_cache["magic"], False)
    out["Transform Poof"] = x + 0.3 * w / (np.sqrt(np.mean(w ** 2)) + 1e-9) * np.sqrt(np.mean(x ** 2))

    # Break: the form shatters as it takes a hit for you. Heavier than the poof: a bright crack,
    # a big drum and timpani on D, a staccato D minor chord on horns and trombones, then the
    # poof's darkening burst and the harp falling A F D.
    L = 1.2
    n = int(L * RATE)
    t = np.arange(n) / RATE
    crack = M.swept_noise(L, [(lo, hi, np.exp(-t / tau)) for lo, hi, tau in
                              [(2500, 6000, 0.035), (6000, 12000, 0.02), (1200, 2500, 0.05)]], SEED + 23)
    crack *= np.clip(t / 0.002, 0, 1)
    burst = M.swept_noise(L, [(lo, hi, np.exp(-t / tau)) for lo, hi, tau in
                              [(150, 400, 0.25), (400, 900, 0.16), (900, 2000, 0.1)]], SEED + 24)
    burst *= np.clip(t / 0.01, 0, 1)
    buf = Buffer(L + 2.0, False)
    buf.place(M.pan(crack / (np.abs(crack).max() + 1e-9) * 0.55, 0), 0)
    buf.place(M.pan(burst / (np.abs(burst).max() + 1e-9) * 0.5, 0), 0.01)
    buf.place(M.ring(kit.hit("mallet_ff"), 1.5) * 0.9, 0)
    buf.place(M.ring(kit.timp_d, 2.0) * 0.7, 0)
    for m in (50, 53, 57):
        buf.place(M.pan(M.mono(M.ring(kit.horn_stac.get(m), 0.5)), 0.1) * 0.45, 0.005)
    buf.place(M.pan(M.mono(M.ring(kit.tbn_stac.get(38), 0.5)), -0.1) * 0.5, 0.005)
    for k, m in enumerate([69, 65, 62]):
        buf.place(M.pan(M.mono(M.ring(kit.harp.get(m), 1.2)), 0.1 - 0.1 * k) * 0.35, 0.12 + 0.08 * k)
    x = buf.x
    w = M.reverb(x, ir_cache.setdefault("castle", M.make_ir("castle", SEED)), False)
    out["Transform Break"] = x + 0.35 * w / (np.sqrt(np.mean(w ** 2)) + 1e-9) * np.sqrt(np.mean(x ** 2))
    return out


# ============================================================================== main
def align_check(variants):
    """Onset envelopes of a cue's biome versions should line up at lag 0 (same notes, same time)."""
    envs = []
    for y in variants:
        m = np.abs(M.mono(y))
        hop = int(0.01 * RATE)
        e = m[:len(m) // hop * hop].reshape(-1, hop).max(1)
        o = np.maximum(0, np.diff(e, prepend=e[0]))
        envs.append((o - o.mean()) / (o.std() + 1e-9))
    lags = []
    for e in envs[1:]:
        c = [np.mean(envs[0] * np.roll(e, k)) for k in range(-20, 21)]
        lags.append(int(np.argmax(c)) - 20)
    return lags


def main():
    vsco = sys.argv[1]
    only = None
    if "--only" in sys.argv:
        only = set(sys.argv[sys.argv.index("--only") + 1].split(","))
    copy = "--no-copy" not in sys.argv
    kit = Kit(vsco)
    ir_cache = {}
    report = json.loads((OUT / "report.json").read_text()) if (OUT / "report.json").exists() else {}
    timeline = []
    shipped = []
    for cue_name, spec in CUES.items():
        if only and cue_name not in only:
            continue
        notes, bars, length = compose(cue_name, spec)
        n_samples = int(round(length * RATE))
        variants = []
        for b in spec["biomes"]:
            arr = arrangement(b, spec)
            parts = render_parts(kit, notes, length, arr, True, SEED)
            y = master(mix(parts, arr, True, SEED, ir_cache))
            assert len(y) == n_samples
            name = "Score " + cue_name + ("" if len(spec["biomes"]) == 1 else " " + b.capitalize())
            o = write(y, name, MUSIC_DIR)
            shipped.append((o, MUSIC_DIR / o.name))
            r = dict(seconds=round(length, 4), samples=n_samples, bars=len(bars), tempo=spec["tempo"],
                     lufs=round(M.lufs(np.concatenate([y, y])), 2), true_peak=round(M.true_peak_db(y), 2),
                     low_mid_share=low_mid_share(y))
            r.update(seam_report(y))
            r["limiter_max_gr_db"] = LIMIT_LOG[-1]
            report[name] = r
            variants.append(y)
            print(name, r)
        if len(variants) > 1:
            report["Score " + cue_name + " alignment lags (10 ms frames)"] = align_check(variants)
        for n in sorted(notes, key=lambda n: n.t):
            if n.role in ("lead", "bass", "harmony"):
                timeline.append("%-10s %8.3f s  %-8s %s" % (cue_name, n.t, n.role, n.midi))
        if cue_name != "Menu":
            y, bar = launch(kit, cue_name, spec, ir_cache)
            y = master(y, MUSIC_LUFS, loop=False)
            name = "Launch " + cue_name
            o = write(y, name, MUSIC_DIR)
            shipped.append((o, MUSIC_DIR / o.name))
            report[name] = dict(seconds=round(len(y) / RATE, 3), lands_at=round(bar, 4), lands_at_samples=int(round(bar * RATE)),
                                lufs=round(M.lufs(y), 2), true_peak=round(M.true_peak_db(y), 2))
            print(name, report[name])
    if not only or "stings" in only:
        y = master(game_over(kit, ir_cache), MUSIC_LUFS, loop=False)
        o = write(y, "Game Over Theme", MUSIC_DIR)
        shipped.append((o, MUSIC_DIR / o.name))
        report["Game Over Theme"] = dict(seconds=round(len(y) / RATE, 3), lufs=round(M.lufs(y), 2), true_peak=round(M.true_peak_db(y), 2))
        print("Game Over Theme", report["Game Over Theme"])
        for name, y in transform_set(kit, ir_cache).items():
            loop = name == "Transform Hum"
            # Mastered as the mono file Unity will play (SFX are imported mono).
            y = M.mono(y)
            y = master(np.stack([y, y], 1), -24.0 if loop else -14.0 if name == "Transform Break" else -15.0, loop=loop)
            o = write(y, name, SFX_DIR, ogg=False, mono=True)
            shipped.append((o, SFX_DIR / (name + ".wav")))
            r = dict(seconds=round(len(y) / RATE, 3), lufs=round(M.lufs(M.mono(y)), 2), true_peak=round(M.true_peak_db(y), 2))
            if loop:
                r.update(seam_report(np.stack([M.mono(y)] * 2, 1)))
            report[name] = r
            print(name, r)
    (OUT / "report.json").write_text(json.dumps(report, indent=1, default=float))
    if timeline:
        (OUT / "timeline.txt").write_text("\n".join(timeline), encoding="utf-8")
    if copy:
        MUSIC_DIR.mkdir(parents=True, exist_ok=True)
        for src, dst in shipped:
            shutil.copyfile(src, dst)
        print("copied", len(shipped), "files into the project")


if __name__ == "__main__":
    main()
