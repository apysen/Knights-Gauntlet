# Knight's Gauntlet score: build

Every music cue in the 3D style, the launch phrases, the game over sting and the transformation set are built here from one theme. Nothing is downloaded music: the notes are written in `build_music.py`, played on CC0 orchestral samples (VSCO 2 Community Edition), mixed and measured in Python. The score is the grown-up, darker version of the game's original Logic bounces (MenuTheme, Transformation, Adventure Mode Next Level).

## Rebuild

1. `python -I fetch_vsco.py <folder outside the repo>` downloads only the samples the score uses from https://github.com/sgossner/VSCO-2-CE, checks the repo's LICENSE is CC0, and decodes them to float WAV. Nothing in that folder is ever run.
2. `python -I pitchcheck.py <that folder>` checks every pitched instrument sounds the note it is asked for. VSCO names most notes an octave low, and the organ's files are numbered; this catches a wrong mapping. Bowed cellos (spiccato, tremolo) carry more energy in the 2nd harmonic than the fundamental, so the check reports them "ok (octave)" after the fundamental was confirmed in the spectrum.
3. `python -I build_music.py <that folder>` renders everything into `out/` (WAVs, Oggs, `report.json`, `timeline.txt`) and copies the Oggs into `Assets/Resources/SFX/Music/` and the transformation WAVs into `Assets/Resources/SFX/`. `--only Menu,Trap,stings` builds a subset; `--no-copy` leaves the project alone.
4. `python -I analyze_old.py <wav> <bpm>` transcribes an old bounce (eighth-note melody and bass, key estimate). That is how the old music below was read.

It is seeded (`SEED`), so the same input gives the same files. A full build takes about a minute. Needs numpy, scipy and ffmpeg. `out/` is git-ignored.

Unity import settings for `SFX/Music/*.ogg`: Vorbis, quality 0.6, Compressed In Memory, Preload Audio Data on, Load In Background off (the loops must be loaded before their scheduled start). `Transform Hum.wav` must stay PCM: ADPCM pads it to a block boundary and the loop would click. The other two transformation sounds are ADPCM, mono, Normalize off. If a tempo changes, update `BarSeconds` in `SfxManager.Music.cs` (the launch landing).

## What the old music was (and what was carried over)

The three old files are Logic Pro bounces (each carries Logic's tempo map in a `ResU` chunk and a "Tempo" marker). Git first sees them in commit 5666857 (July 12, 2026); nothing in the repo names a third party, so they look like the project's own Logic work, but that is not proven. Either way the new score takes their contours, rhythms and colours, not their notes.

- **MenuTheme.wav:** 110 BPM, 8 bars (a 4-bar phrase played twice), chiptune squares. Key G minor with the phrygian-dominant colour (G Ab B C D Eb F: B natural and Ab both present). The hook: a quick rising arpeggio G B D, then a scalar push Eb F G to the top; a falling answer Eb ... F F F Eb Bb; then a long held G over a D pedal for two bars.
  - Carried over: the phrygian-dominant colour (the castle's Eb-to-D sigh, the queen's Eb chord and the D major landing of her launch), the b6 half-step at the top of the hook (the motif's A to Bb), the falling answer (bar 2 of the theme, A G F G), and the held note over a dominant pedal (an A held under the second half of the B section).
- **Transformation.wav:** 134 BPM, B minor, a two-bar spinning eighth-note figure (A B D G B A F# F# | F# B A F# B F# F# E) repeated. Carried over: that figure, as scale steps from the chord root, is the cello ostinato of Trap (eighths) and Battle and Boss (sixteenths), and the harp spins it at the start of the new transformation whoosh.
- **Adventure Mode Next Level.wav:** 134 BPM, a fast climb (C# Bb Eb C F G A) landing on a held A. Carried over: every launch phrase's run is that climb in D harmonic minor, landing on the loop's downbeat.

## Musical design

- **Key:** D minor (aeolian) at home; harmonic minor (C#) over the A chords; D phrygian dominant (Eb, F#) for the castle's sigh and the queen.
- **Motif, "the knight's move":** D up a fifth to A, then a half step to Bb (D . A Bb), an L-shape in sound; a horn call. The game over sting turns it over (A F E D, falling).
- **Theme:** A (8 bars: Dm Bb F A | Dm Gm A7 Dm) and B (8 bars: Bb C Dm A | Bb C Gm A, the bVI to bVII lift). The queen's A goes to Eb (bII) in bar 3.
- **Tempos (4/4):** Adventure 66, Menu 88, Trap, Battle and Boss 132 (1:2 and 2:3 apart). 132 is the old stings' 134, near enough.
- **Lengths:** Menu 24 bars (65.5 s), Trap 32 bars (58.2 s), Battle 36 (65.5 s), Boss 36 (65.5 s), Adventure 24 bars at 66 (87.3 s).

| Cue | Form | Feel |
|---|---|---|
| Menu | A, B, A2 | solemn: the horns state the theme over a low cello and viola pad, a slow pizzicato figure, timpani and a soft gong at section tops, a distant big drum in B |
| Trap / Daily | intro (4), A, B, A2, tag (the call twice) | urgent: the spinning cello ostinato in eighths, contrabass pulse, big drums on 1 and 3, a tom pattern, tom rolls into each phrase |
| Battle | intro, A, B, A2, A3 (in thirds) | the most aggressive: sixteenth-note ostinato, eighth-note bass, brass stabs on 1 and the and of 2, heavier drums |
| Adventure | intro, A (an octave low), B every other bar, tag | brooding and sparse: held pad, a slow root-and-fifth figure, a distant drum every other bar |
| Boss | intro, A, A2, B, A3 (horns and trombones in octaves) | the climax: castle palette, Eb chord, phrygian sigh, sixteenths, gong |

**Arrangements per biome** (Trap, Battle, Adventure; same notes, same times, same length, so the game crossfades between them in step). Danger escalates:

| Biome | Theme | Ostinato / figure | Pad | Drums | Room |
|---|---|---|---|---|---|
| Meadow | horns | cello spiccato (pizzicato when calm) | cellos | large hand drum, low toms | small (0.9 s) |
| Bridge | the cellos, in their singing register | thinner spiccato | violin tremolo, high and thin; wind (synthesized, gusting) | only a large drum played softly with sticks | medium (1.7 s) |
| Castle | horns with trombones | cello spiccato (tremolo when calm) | organ | large drum with mallets, toms, timpani, gong | hall (3 s) |

The castle adds the phrygian sigh: trombones falling Eb to D over every D minor bar.

**Launch phrases** (`Launch <cue>`): drawing the sword. One bar at the cue's tempo: a sforzando fifth on horns and trombones with a big drum, then the old Next Level climb as twelve sixteenths on cellos (horns doubling the top), toms driving it, landing at the bar line on a D minor chord with timpani, big drum and cymbal (the queen's lands on D major with a gong). The bar line is where the loop is scheduled to start. Adventure's is quieter: the call low on one horn over a timpani roll, landing on a held D minor with a soft gong.

**Game over** (`Game Over Theme`): 72 BPM, horns A F E D falling low over Gm, A, Dm on cellos with a trombone bass, timpani and a soft gong under the last note. About 7 s.

**Transformation set:** `Transform Whoosh` (a noise sweep, the harp spinning the old Transformation figure in D minor and then climbing, a bell), `Transform Hum` (a 4 s loop on D3, A3, D4; every frequency and LFO completes whole cycles), `Transform Poof` (a darkening burst, the harp stepping down A F D; the timer ran out), `Transform Break` (the form shattering as it takes a hit: a bright crack, a big drum and timpani on D, a staccato D minor chord on horns and trombones, then the poof's burst and harp; about 1 LU louder than the poof).

**No choir:** VSCO 2 CE has none, and nothing was faked. The weight comes from horns, trombones, cellos and drums in the low mids (about 150 Hz to 2 kHz), which phone speakers can play; everything under 45 Hz is filtered out.

## How the loops loop

Every layer renders into a circular buffer exactly the loop's length: notes and reverb tails that run past the end wrap onto the start, filters run over a wrapped pre-roll, the reverb is a circular convolution, the wind's gusts complete whole cycles, and the limiter's filters wrap too. Notes have a little seeded timing looseness (about 5 ms) that is the same in every arrangement of a cue.

## Measured (out/report.json, this build)

- **Loudness:** every loop, launch and the game over sting at -16.0 LUFS integrated (loops measured over two passes), true peak at or under -1.4 dBTP. The limiter took at most 2 dB off (Adventure Meadow); most cues needed none. In game, `SfxManager` adds +3.6 dB so the score plays as loud as the old menu theme did at the same slider position.
- **Low-mid weight:** 75 to 89 % of each cue's energy sits between 150 Hz and 2 kHz.
- **Seams:** the sample step at every loop seam (0.0008 to 0.024; the largest is the bridge's wind noise) is below the largest step inside the same file (0.04 to 0.08), and the RMS around each seam stays inside the file's own range.
- **Exact lengths:** each Ogg decodes to exactly its sample count (2,565,818, 2,886,545 or 3,848,727 frames) in ffmpeg and in Unity's import.
- **Alignment:** the onset envelopes of each cue's three arrangements line up at lag 0 (10 ms resolution).
- **Pitch:** `pitchcheck.py` passes for every instrument.

## Timeline

`out/timeline.txt` lists every lead, harmony and bass note of every cue with its time.
