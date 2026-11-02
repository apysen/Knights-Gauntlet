# Audio licenses

Every third-party sound below is CC0 1.0 (public domain dedication, https://creativecommons.org/publicdomain/zero/1.0/). No attribution is required, though crediting Kenney and Versilian Studios (VSCO 2 Community Edition) in the game credits is a nice gesture. Nothing here is CC-BY or NC.

Proof: each OpenGameArt page lists "License(s): CC0" in its license field (checked October 31, 2026). Each Kenney pack ships a License.txt that says "License: (Creative Commons Zero, CC0)". The VSCO 2 CE repository's LICENSE file is the CC0 1.0 Universal text, and GitHub reports its license as CC0-1.0 (checked November 1, 2026). Copies are in `license-proof/`.

## Music (`Assets/Resources/SFX/Music/`): original, built in `MusicBuild/`

The whole score (Score Menu, Score Trap/Battle/Adventure Meadow/Bridge/Castle, Score Boss, the four Launch phrases and Game Over Theme) and the transformation set (`SFX/Transform Whoosh.wav`, `Transform Hum.wav`, `Transform Poof.wav`, `Transform Break.wav`) are original compositions for this game, written and rendered by `MusicBuild/build_music.py`. See `MusicBuild/README.md`.

They grow out of the game's own earlier Logic bounces (`SFX/MenuTheme.wav`, `Transformation.wav`, `Adventure Mode Next Level.wav`, still used by the legacy pixel style); see the README for what was carried over. They are played on these samples, retuned and cut per note (none is used as a recording of music):

| Instrument in the score | Source | Author | License | Files (repo paths) |
|---|---|---|---|---|
| French horns | https://github.com/sgossner/VSCO-2-CE | Versilian Studios (Sam Gossner, Simon Dalzell; cut by Elan Hickler) | CC0 | Brass/F Horn/sus/MOHorn_sus_*_v2_1.wav (plus A0, D4, F4 v1), Brass/F Horn/stac/*_v2_rr1.wav |
| tenor trombones | same | same | CC0 | Brass/Tenor Trombone/sus/*_v2_1.wav, Brass/Tenor Trombone/stac/*_v2_rr1.wav |
| cello section spiccato, tremolo, sustain, pizzicato | same | same | CC0 | Strings/Cello Section/spic/*_v2_RR1/RR2, trem/*_v1_1 (not trem_D2), susvib/*_v1_1, pizzT/*_v2_RR1 |
| contrabass spiccato | same | same | CC0 | Strings/Solo Contrabass/Spic/*_v1_rr1.wav |
| viola section sustain | same | same | CC0 | Strings/Viola Section/susvib/*_v1_* |
| violin section tremolo | same | same | CC0 | Strings/Violin Section/Trem/*_v1.wav |
| organ | same | same | CC0 | Keys/Organ/Quiet/NT5_Man3Quiet_*_rr1.wav |
| large drums, low toms, gong | same (the VSCO 1 percussion folder) | same | CC0 | VSCO 1 Percussion/drums/other/ethnic/giant/{hand,mallet,sticks}, drums/tenor/tenor_lower, varMetal/Gong/gong_hit_{p,mp}.wav |
| timpani, crash cymbal | same | same | CC0 | Percussion/Timpani/Timpani{2,3}_Hit_v3_rr1_Sum.wav (retuned to A2, D3), Rolls/Timpani3_Roll_v3_rr1_Sum.wav, Percussion/cymbal-crash1_mp_rr1.wav |
| harp, glockenspiel (transformation set only; the break also uses a large drum, timpani and horn and trombone staccatos) | same | same | CC0 | Strings/Harp/KSHarp_*.wav, Percussion/Glock/glock_medium_*.wav |
| reverbs, wind, whoosh and poof noise, hum tones | synthesized | | | no recording |

The raw samples are not in the repo: `MusicBuild/fetch_vsco.py` downloads exactly the files above.

## SFX (`Assets/Resources/SFX/`)

All clips below were processed with ffmpeg before import: downmixed to mono, leading silence trimmed, gain set to sit at today's mix level (see "Processing").

| Project file | Used for | Source page | Author | License | Original file |
|---|---|---|---|---|---|
| Move Wood 1-5.wav | PieceMove (round robin) | https://kenney.nl/assets/impact-sounds | Kenney | CC0 | impactWood_light_000 to _004.ogg |
| Coin Ting.wav | ShillingCollect (pitch chain) | https://opengameart.org/content/80-cc0-rpg-sfx | rubberduck | CC0 | item_coins_02.ogg |
| Coin Land 1-3.wav | CoinLand (HUD counter) | https://kenney.nl/assets/casino-audio | Kenney | CC0 | chips-collide-1, -2, -4.ogg |
| Level Up Fanfare.wav | TrapLevelUp | https://opengameart.org/content/classic-fanfare-lick | fvcalderan | CC0 | fanfare_3.ogg |
| Boss Phase Hit.wav | BossPhase | https://kenney.nl/assets/music-jingles | Kenney | CC0 | Hit jingles/jingles_HIT11.ogg |
| Queen Shield Up.wav | QueenShieldUp | https://kenney.nl/assets/interface-sounds | Kenney | CC0 | glass_004.ogg |
| Queen Shield Pop.wav | QueenShieldPop | https://opengameart.org/content/3-pop-sounds | wubitog | CC0 | 3pops.zip: pop1.ogg |
| Powerup Chronokinesis.wav | PowerupChrono | https://kenney.nl/assets/impact-sounds | Kenney | CC0 | impactBell_heavy_000.ogg, reversed into a swell |
| Powerup Divine.wav | PowerupDivine | https://opengameart.org/content/magic-words-healing-sound-effect | Spring Spring | CC0 | health_restore.wav |
| Powerup Divine Path.wav | PowerupPath | https://kenney.nl/assets/rpg-audio | Kenney | CC0 | bookFlip1.ogg |
| Powerup War Horn.wav | PowerupWarHorn (War Horn blast) | https://github.com/sgossner/VSCO-2-CE | Versilian Studios | CC0 | built by `MusicBuild/build_warhorn.py`: F horn and tenor trombone sustains and staccatos (D-A open fifth), giant mallet drum, timpani (D), crash cymbal; synthesized hall reverb |
| Rally Tick.wav | RallyTick (War Horn count-up, pitched in code) | same | same | CC0 | built by `build_warhorn.py`: glockenspiel D, plus a synthesized noise click |
| Rally Slam.wav | RallySlam (War Horn count lands) | same | same | CC0 | built by `build_warhorn.py`: giant mallet drum, timpani, tenor drum, crash, F horn and trombone staccatos (D major), glockenspiel; synthesized crack and reverb |
| Powerup Berserker.wav | PowerupBerserker (Berserker Draught) | https://github.com/sgossner/VSCO-2-CE | Versilian Studios | CC0 | built by `build_warhorn.py`: giant mallet drum, tenor drum, timpani (D), tenor trombone staccatos (D2 A2 D3), F horn sustains (D4, Eb4); cork pop, gulp, flame roar, crackles and reverb synthesized |
| Adventure Level Complete.wav | AdventureNextLevel (Adventure goal reached, 3D style; the pixel style keeps the old Adventure Mode Next Level) | https://github.com/sgossner/VSCO-2-CE | Versilian Studios | CC0 | built by `MusicBuild/build_level_complete.py`: F horn sustains and staccatos, tenor trombone sustains and staccatos, cello section sustains, tenor drums, giant mallet drum, timpani hit and roll (D), crash cymbal, soft gong, harp, glockenspiel; synthesized hall reverb |
| Fury Crackle.wav | FuryCrackle (captures under the draught) | | | | synthesized in `build_warhorn.py` (noise flame burst and crackles), no recording |
| Menu Entrance Whoosh.wav | MenuEntrance (main menu entrance swell) | | | | synthesized in `build_warhorn.py` (swept band noise, reverb), no recording |
| Menu Title Tinks.wav | MenuTitleTinks (one tink per 3D title letter) | | | | synthesized in `build_warhorn.py` (inharmonic sine partials on a D minor pentatonic), no recording |
| Knight Clank 1-3.wav | KnightTap (tapping the menu knight, round robin) | | | | synthesized in `build_warhorn.py` (inharmonic plate partials, noise click, low body), no recording |

## Processing

- SFX peaks or loudness were set so they sit near the existing clips under the current 0.1 volume ceilings: moves peak at -6 dBFS, the coin ting at -1 dBFS, HUD clinks at -8 dBFS, stings around -14 to -16 LUFS (mono), with a -1 dBFS limiter where needed.
- The score is mastered to -16 LUFS per cue (true peak under -1 dBTP); SfxManager plays it 3.6 dB up so it matches the old menu theme's level in game.
- Downloads were kept outside the project; only the files above were copied in.
