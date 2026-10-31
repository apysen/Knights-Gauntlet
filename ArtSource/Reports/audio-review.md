# Audio review (October 31, 2026)

From the `game-audio-engineer` agent (`.claude/agents/game-audio-engineer.md`), working within this project's limits: Unity built-in audio through `Assets/Scripts/View/SfxManager.cs`, a solo developer, mid-range phones, no FMOD, Wwise or spatial audio. Recommendations only; nothing has been implemented yet. Line numbers refer to the code as of this date.

## What the game sounds like today (checked in code)

- **Music:** only `MenuTheme.wav`, a 17.5 s stereo loop that streams. Every gameplay entry point calls `StopMenuMusic()` (`GameManager.cs` lines 294, 324, 665, 867). Trap, Battle, Adventure, Daily and the boss all play in silence.
- **Silent moments:** death and game over, revive, trap rows arriving, coins landing in the HUD counter, all three powerups, the queen's bubble shield, its pop, and the boss phase change. The boss phase change reuses the normal capture sound (`AdvanceBossPhase`, line 1045). Star results, shop purchases and leaderboard are also silent.
- **Odd mappings:**
  - `Sfx.Transform` has no file at all.
  - `ShieldBreak` fires when your transform absorbs a hit, not on the queen's shield.
  - The 7.2 s `Transformation.wav` is played as a loop (`PlayLoop`, line 1215), so that sting repeats for the whole transform.
- **Mix ceiling:** `MaxSfxVolume` and `MaxMusicVolume` are both capped at 0.1 (`SfxManager.cs` lines 64 to 67). That suggests the source files are hot and badly matched. A 10% ceiling also throws away a lot of usable range.

## 1. Sonic identity: warm, tactile, regal

The pieces look like lacquered, gold-trimmed sculptures on a storybook board in front of grass, a wooden bridge and a castle. So the sound should feel physical: wood and ceramic clacks, felt-padded landings, little brass and bell accents for rewards. "Regal" covers the castle and the queen boss with heraldic brass and timpani, kept playful rather than epic. Sessions are short and puzzle-paced, so everything should be quick, clear and pleasant to hear 200 times. The square-wave blips belong to the pixel era and make the polished 3D art feel cheaper.

## 2. Music

**Style:** light orchestral and folk chamber. Use plucked lute or harp, pizzicato strings, recorder or flute, celesta or glockenspiel, frame drum and tambourine, and light brass for royal moments. Avoid big cinematic drums and synth pads.

| Cue | Notes | Tempo and length |
|---|---|---|
| Menu (replace or extend) | Gentle courtly theme. The main melody can repeat in the other cues. | 90 to 100 BPM, 60 to 90 s loop. 17 s is too short and you'll hear the repeat. |
| Trap / Daily | Driving, bouncy folk with pizzicato and frame drum | 120 to 130 BPM, 60 to 90 s loop |
| Battle | Bolder version of the same tune with brass stabs and snare | 130 to 140 BPM, 60 to 90 s loop |
| Adventure | One calm puzzle loop per biome (meadow, bridge, castle), sparse with room to think | 80 to 95 BPM, 60 to 120 s each. Three loops shared across 7 worlds is fine. |
| Queen boss | Minor-key heraldic theme with timpani and harpsichord | 140 BPM, 60 s loop. Optionally speed or brighten it a little per phase. |
| Stings | Game over (falling lute and pizzicato, 2 s), level clear (exists), 3-star fanfare (3 s), new high score, boss defeated (5 s) | 2 to 5 s |

**Adaptive layering (worth doing, but keep it simple):** use two sample-locked stems per gameplay track. One is the base bed and the other is a percussion and brass "intensity" layer.

- Play both on two AudioSources started with `PlayScheduled` on the same `AudioSettings.dspTime`.
- Fade the intensity layer's volume over about 1 s.
- In Trap, drive it from the speed level, hooked where `TrapLevelUp` fires (`GameManager.cs` line 845).
- In Battle, drive it from combo at 3x or more, then fade it back down after the combo drops.

Skip horizontal re-sequencing and beat-quantized transitions. The cost isn't worth it here. Duck the music by about 6 dB under stings and on pause.

## 3. SFX

**Fits the 3D style:** the Capture Slash x3 set (it's mono and has variants), the Transformation sting (as a one-shot), Adventure Next Level, and the shilling pitch chain idea.

**Clashes with the 3D style:** all the JDSherbert square-wave clips, especially PieceMove (heard on every move), ShillingCollect, TrapLevelUp, ShieldBreak and Untransform. Keep them only for `UIFactory.LegacyStyle`.

### Must-have

1. **Knight hop and land:** a short whoosh on lift, then a wood or ceramic piece "tock" on landing. Use three surface sets: soft felt-and-grass thump, hollow wood plank, stone click. Make 3 or 4 round robins each.
2. **Shilling collect:** a bright metallic coin "ting" with a short sparkle tail. Keep the pitch chain.
3. **Coin lands in the HUD counter:** a soft register "clink" plus a tiny tick. It should be quieter than the pickup.
4. **Death / game over:** a piece toppling onto the board (wood clatter), then the game over sting.
5. **Capture by piece type:** the slash plus a material layer. Pawn is a light ceramic chip, rook a heavy stone crunch, bishop a glassy crack, queen a shatter with a bell ring.
6. **Queen bubble shield:** a soft shimmer when it forms and a wet glassy "bloop-pop" when it breaks. Also give the boss phase change its own heavier hit plus a brass stab instead of a normal capture.
7. **Powerup activation:**
   - Chronokinesis: a reversed swell plus a ticking clock that slows, with a low-pass on music for the duration.
   - Divine Intervention: an airy choir "ahh" plus a bell.
   - Pathfinder: a paper map unfurling plus a quill scratch.

### Nice-to-have

8. **Trap row arriving:** a subtle mechanical clunk or ratchet, quiet, with the pitch rising per speed level.
9. **Speed level-up:** a short trumpet flourish to replace the Popup Open 1 blip.
10. **Transformation:** a one-shot sting, a quiet magical hum loop for the duration (2 to 4 s, seamless), and an "un-poof" when it ends.
11. **Combo milestones:** a small orchestral hit at 3x, 5x and 6x, layered under the capture.
12. **UI set:** a soft wood button tap, a parchment page turn for How to Play, a leather tab flick, a purchase "cha-ching" with a coin pour, and an equip shimmer for skins.

### Polish

13. Star reveal dings that rise per star.
14. Very quiet biome ambience: birds and wind for the meadow, creaking and water for the bridge, distant hall reverb and torch crackle for the castle.
15. A Nightfall skin unlock sting.
16. A leaderboard rank-up tick.

## 4. Mix and implementation notes (`SfxManager.cs`)

- **Loudness:** normalize everything before import. Aim for music around -16 LUFS integrated and SFX peaks around -3 dBFS, with priority sounds about 3 dB above routine ones. Then raise `MaxSfxVolume` and `MaxMusicVolume` (lines 64 to 67) to about 1.0 and 0.6, and set defaults near 0.8 and 0.5. Phone speakers have no low end, so put the tonal energy of key sounds at 1 to 4 kHz.
- **AudioMixer:** add a Music group, an SFX group and a UI group, and point the sources' `outputAudioMixerGroup` at them in `Awake()` (lines 119 to 150). Then slider changes, music ducking and the Chronokinesis low-pass become mixer snapshots or exposed parameters rather than code.
- **Shilling chain (`PlayShilling`, line 183):** keep the major scale and the octave cap. Drop the pickup about 2 dB as the chain climbs so high notes don't poke out. Add ±1 dB volume jitter but no pitch jitter, since the pitch is the melody. The HUD clink should not climb.
- **Capture combo (`PlayCapture`, line 196):** the current 0.98 to 1.10 pitch and volume ramp is fine. Add ±3% random pitch on top, and cap the combo volume scale at 1.15. The 1.35 scale clips once real volume ceilings return. Pass the `PieceType` in so each type gets its own material clip list.
- **Variation:** generalize `_captureClips` and `_lastCaptureClip` (lines 36 to 40 and 159 to 165) into a reusable "clip bank" per `Sfx`. It should load numbered variants, pick without repeating the last one, and add ±4% pitch and ±1.5 dB jitter. Use it for moves, landings and UI taps. The PieceMove ×0.68 special case (line 180) can then live as a per-entry volume.
- **Formats** (all clips currently use Vorbis with Decompress On Load, and the menu theme streams):

  | Asset type | Compression | Load type | Other settings |
  |---|---|---|---|
  | Short SFX (under 2 s) | ADPCM | Decompress On Load | Force To Mono, 22 to 32 kHz |
  | UI taps | PCM or ADPCM | Decompress On Load | Fine to keep, since they're tiny |
  | Music | Vorbis, quality 60 to 70 | Streaming | Load In Background on. Check that intensity stems stay in sync while streaming; if not, use Compressed In Memory. |
  | Transform hum loop (2 to 4 s) | Vorbis | Compressed In Memory | |

  `Transformation.wav` is a 1.26 MB stereo WAV. Make it mono and shorter.

- **Voice budget (mid-range phones):** Project Settings > Audio, Real Voices 24, Virtual 48.

  | Use | Voices |
  |---|---|
  | Music (base + intensity) | 2 |
  | Loop | 1 |
  | Captures | 4 |
  | Shillings | 3 |
  | Movement and landings | 2 |
  | UI | 2 |
  | Stings | 2 |
  | Ambience | 1 |
  | Total | about 17 |

  Set `priority` so music and the player's sounds never get stolen. Limit trap-row clunks to one at a time.
- **Muting:** have the `Muted` setter (line 109) also pause or resume music. Right now it only blocks new plays.

## 5. Where to get sounds

- **CC0 and free:** Kenney audio packs (UI, impacts, RPG) are CC0. Sonniss' free GDC game audio bundles are royalty-free (not CC0). Both are safe for commercial use with no attribution needed. Freesound also works: filter for CC0 and avoid NC-licensed files.
- **Paid royalty-free:** Unity Asset Store sound and music packs (Standard EULA, fine for shipped games), Epidemic Sound or Artlist subscriptions (check that the license covers games permanently, not only while you're subscribed), and Humble or GameDev Market bundles.
- **Commissioning:** a freelance composer for about 6 to 8 loops and stings. Agree that you get a perpetual license for games and their trailers, plus delivered stems. Indie rates are often a few hundred dollars per minute.
- **Licensing:** keep a credits and license log file. Avoid CC-BY-NC, and keep any credit lines CC-BY requires.

## Top 5 changes to make first

1. Add gameplay music (Trap/Battle loop, Adventure loop, boss theme) and a game over sting. Right now gameplay is completely silent.
2. Replace the square-wave move, shilling and level-up blips with wooden hop/land sounds, a metallic coin ting and a trumpet flourish, using round robins and pitch jitter.
3. Normalize all clips, raise the 0.1 volume ceilings and route through an AudioMixer with Music, SFX and UI groups.
4. Add sounds for powerup activation, the queen's shield appearing and popping, and the boss phase change.
5. Re-import short SFX as mono ADPCM with Decompress On Load, and make the transform loop a short seamless hum instead of looping the 7 s sting.

Changes that need no new audio files: raise the volume ceilings, add the AudioMixer, fix the transformation loop, make mute pause music, and set the import formats.
