# Knight's Gauntlet Art Direction

## Ceramic trio exploration (October 15, 2026)

The user requested an alternative prototype for the knight, rook and bishop:
chunky matte ceramic pieces with broad chess silhouettes, simpler pedestals and
restrained color. The exploration and comparison images live in
`ArtSource/CeramicPrototype/README.md`. Its runtime opt-in is
`GameTheme.ceramicPiecePrototype`; the saved default remains the supplied set below.
Review the trio on the production board before extending it to the remaining pieces.

## Direction

Use the **user-supplied colored 3D chess models** in `Assets/Resources/Pieces` as the production pieces. Their saturated lacquer-like bodies, bold gold trim, near-black shadow masses, and sharp ivory highlights define the approved look. Use quiet modeled tiles under an angled orthographic camera, with pieces and the Adventure flag standing perpendicular to the board. Preserve the 5×10 gameplay grid. Size each chess pedestal to about 84% of a square tile and anchor its underside to the tile surface.

These supplied models supersede the earlier generated pixel, armored-character, and procedural poster-style pieces. Preserve their sculptural chess silhouettes, restrained angular eye slits, and bold graphic shading. Do not add expressive faces or redraw their shading. Personality comes from silhouette, color, and material facets. Keep the clean shapes and strong value contrast.

## Visual principles

1. **Read at phone size first.** The knight, hazards, legal moves, enemies, and pickups must be identifiable at their actual board scale. Test art inside the 5×10 board, not only in a large preview.
2. **Silhouette before detail.** The horse-head knight, crenellated rook, split bishop, crowned queen, and round pawn must remain recognizable as chess pieces. Their shapes supply their character.
3. **Reserve the loudest color for gameplay meaning.** Keep the board comparatively quiet; make the player, danger, legal moves, and rewards stand out.
4. **Make effects brief and legible.** One clear action pose and a strong impact shape are more useful than long choreography or constant particles.
5. **Consistent lighting.** Light arrives from the upper left. Left and upward-facing planes carry the color and ivory cuts; right sides, undersides, and recessed bands carry near-black shapes. Highlight placement must describe the turned surface. Preserve the same light direction in front and back artwork.
6. **Keep the whole product in one visual language.** Replace pixel fonts, pixel button frames, and pixel-styled icons as the rest of the art is converted.

## Palette and color roles

Suggested starting palette; tune it against the finished sprites and mobile display:

| Role | Color | Use |
| --- | --- | --- |
| Ink / shadow | `#111210` | Solid shadow shapes, silhouettes, panel borders |
| HUD foundation | `#151512` | High-contrast controls and counters |
| Board light | `#F3E9D8` | Warm paper square |
| Board dark | `#DED2BB` | Warm beige square; keep contrast modest |
| Gold | `#EFB000` | Piece trim, rewards, selected controls |
| Vermillion | `#CF3524` | Hazard signals and boss accents |
| Ivory | `#FFF6E7` | Sharp reflected-light shapes and light pieces |
| Move accent | `#B18418` | Legal landing hints and jump trail |

Preserve the supplied family palette: blue knight, green pawn, orange rook, teal bishop, magenta queen, and red king. Keep the board and interface foundations quiet around these saturated pieces. Shape, position, player glow, and threat markings must also communicate role; color alone is insufficient.

## Piece design

Draw classical chess sculptures with broad color and shadow masses. Keep all designs anchored to a turned pedestal. Do not turn them into people wearing chess-themed armor.

- **Knight:** blue sculpted horse head with angular mane and gold trim. Back view shows the mane and rear neck. Nightrider reuses this artwork.
- **Rook:** crenellated tower on a curved stem and broad base. Use its blocky crown to distinguish it from the queen.
- **Bishop:** pointed, split mitre above a narrow stem, with the slit readable at phone size.
- **Queen:** recognizable crown with rounded tips, collar, and tall turned stem. Strongest enemy presence through its silhouette and scale hierarchy.
- **Pawn:** green sphere, narrow neck, and pedestal; shorter than the major pieces.
- **King:** red cross-topped supplied piece used as the Adventure hunter queen's boss appearance. This is a visual variant; boss rules and the core Queen type are unchanged.
- **Trap:** clear vermillion X / hazard silhouette, without a character face.

## Facing and pose budget

Only **front-facing and back-facing** views are required. Move toward the top of the board: back. Move toward the bottom: front. A purely lateral move keeps the current facing. Scrolling alone must not change facing. Keep the light fixed in world space instead of mirroring the shadow with the piece.

The approved set contains one front and one back image per piece. Reuse those images for current attack/hit events and retain the brief slash, impact, trail, and squash effects. Separate held attack illustrations remain an optional future addition, not part of the current supplied set. Do not create eight directional animation sets or mirror the supplied lighting.

## Board, HUD, and menus

- Keep the 5×10 board, with square cells centered in the HUD-safe area. Preserve the square tile art; let the camera create side margins for environmental framing rather than stretching cells or removing rows.
- Use texture-free Blender meshes for all floor tiles: muted sage ground, broad oak planks, and pale limestone. Small bevels and restrained alternating values define the grid. Model fragile stone as separated fragments with broad fissures. Bridge iron curbs appear only on the outside columns. Avoid foliage, flowers, noisy stone textures, and surface patterns that compete with the pieces.
- In Trap and Battle runs, use grass for the first 60 seconds, drawbridge for the next 30 seconds, then castle stone. In Adventure, use grass in World 1, drawbridge in World 2, and castle stone in later worlds.
- Keep tile detail sparse beneath pieces: beveled edges, clear seams, simple directional shading, and no paths or decorative motifs that resemble gameplay markers.
- Use a warm, light board so the near-black piece shadows remain visible. Keep dark, high-contrast HUD panels and sparse graphic accents. Keep UI overlays and decorative bursts out of the legal-move and hazard reading area.
- Replace pixel lettering with a chunky, rounded display face for titles, scores, combo callouts, and buttons. Use a simpler, highly legible face for small instructions and settings.
- Give buttons and panels a thick ink border, a simple offset shadow, and a flat or lightly graded fill. Use comic bursts and caption shapes for temporary events, not as the default container for every label.
- Enlarge the knight as the menu mascot; use a strong pose and one recognizable comic accent rather than leaving the current small glyph in a large empty field.

## Motion and impact language

The simulation should remain immediate. Resolve moves, captures, score, and deaths in the game rules; use short view-only effects to make those events satisfying and easy to understand. Animate the complete 3D piece objects with brief transform moves; rigging is not required for the first pass. Anchor each attack, slash, impact, and capture burst to the square the player landed on. A successful new player move cancels any unfinished cosmetic animation, impact, particles, score popups, or camera shake from the previous move. Invalid taps do not cancel feedback. Nothing waits on animation playback.

- **Movement signature:** the model makes a brief hop with a thin, tapered gold arc that travels and fades in roughly 180 ms. Do not draw a persistent L, right-angle elbow, or thick outlined route. Resolve the move immediately in the simulation; subsequent input cancels the prior cosmetic hop.
- **Knight capture:** add a quick whole-piece sword-swing cue or lunge, followed by a dramatic slash arc on the target square. Keep the action snappy; the captured piece disappears at impact.
- **Transformed-piece captures:** give each form its own compact signature effect while keeping the same brief rhythm. The rook can pop a fist-shaped impact into the target; the bishop can use a sharp magical burst. Make these one-shot meshes or graphic effects, without requiring rigged limbs.
- **Enemy threat:** use the existing threat display and move-timer bar as anticipation. As the bar approaches full, add a subtle lean, glow, or squash to threatening enemies.
- **Enemy hit / death:** use the current facing view with a strong impact burst, then show game over after a short beat (roughly 150–220 ms). The simulation stays over during this presentation delay.
- **Milestone hierarchy:** ordinary movement gets a trail; capture gets its piece-specific attack and impact; transformation, trap level-up, and boss events get the largest bursts and screen-color accents. Keep effects short and avoid strobing.
- Reuse a small pose set: idle, attack, and hit for key pieces; mirror/rotate shared slash effects for direction instead of drawing every directional variant. Give transformed forms unique attack poses when their identity materially benefits from it.
- Provide a reduced-flash / reduced-shake option. Prefer a single bright impact frame and a quick camera kick over prolonged flashing.

## Ad and store creative readability

Use the same game, board, pieces, and core rules in the ad as in the app. Build a short fail-based creative around a real, legible decision: show the player, an obvious trap or safe move, the mistaken choice, then the satisfying correction. The first seconds should make the mistake understandable without text-heavy explanation. A playable ad should use an actual slice of Trap or Battle rather than inventing a different minigame.

For vertical mobile placements, keep the knight, choice, and impact large in frame. Short captions can add the joke (for example, “One safe square.”), but should not cover the board state.

## Implementation notes

- `Assets/Scripts/View/GameTheme.cs` already exposes sprite slots for pieces, cells, UI panels, buttons, and skins.
- `Tools/Art/import-supplied-chess.cjs` preserves the original sheets, splits their existing alpha, and builds twelve padded 512×512 sprites. Both views use the same scale per piece and base baseline, with centered cell pivots. No black chromakey or lighting mirroring is used.
- Five `.blend` sources are copied into `Assets/Resources/Pieces` for Unity model import: pawn, rook, knight, bishop, and queen. Nightrider reuses the knight model; the Adventure hunter queen reuses the queen model. Traps and explicit player skin overrides retain their sprite rendering.
- `Tools/Art/build-board-tiles.py` runs through Unity's Blender bridge to produce five FBX meshes under `Assets/Resources/BoardModels` and editable source at `ArtSource/Models/BoardTiles/board-tiles.blend`. Ground, wood, stone, fragile stone, and edge curbs use geometric detail with plain materials.
- `Tools/Art/extract-piece-textures.py` extracts the supplied models' packed base colors and measures their actual pedestal footprints. `PieceView` preserves import rotation, centers the pedestal, seats its bottom on the board, and turns it around the board normal. The shared mesh shader provides directional shading in the 2D renderer; soft contact shadows visually anchor bases without the broken Unity 6.5 2D shadow pass.
- The board remains in logical XY coordinates with physical height toward -Z. Imported Y-up models use `BoardView.SurfaceOrientation`; the camera is angled 38 degrees from the board normal. All tap paths intersect the board plane with a camera ray; tall-map swipes account for the projected row height.
- Captures animate the whole player model for a brief, non-blocking strike and pair it with an impact at the landing square: knight slash, rook fist pop, bishop slash-burst, and queen's larger slash-impact. Captured pieces still disappear immediately. Every successful new move cancels the prior model motion, move VFX, impacts, trails, particles, popups, and shake before starting its own feedback; reduced-effects settings shorten and simplify the beat.
- `ComicArt` remains the sprite fallback if a model is unavailable. Previous exploratory sources remain available but are not selected by default when a corresponding 3D model imports successfully.
- `PieceView` preserves the full painted palette for the default skin (save ID `gold`). The `PaintedChess` material selectively recolors saturated body regions for other skins while protecting gold trim, ink, and ivory; custom sprite overrides are respected. The default skin name remains Classic Gold for save/UI compatibility and its gold trim/glow.
- Keep visual animation in the view layer. On each successful move, cancel leftover view effects and camera shake before starting the new move’s presentation. Never block input or simulation on animation playback.
- Convert the remaining pixel look together: runtime fonts and shared `UIFactory` styles, power-up/shop icons, movement VFX, and menu artwork must match the new line-art direction.

## Recommended production order

1. Import and inspect the supplied pawn, rook, knight, bishop, and queen models at actual board size, including both front and back facings; keep the trap as a simple sprite marker.
2. Prototype one complete gameplay beat: short model hop and tapered arc → whole-model attack cue → impact burst → score/combo pop.
3. Apply the palette and art to the board, threat cues, pickups, and HUD; check contrast and touch readability on a phone-sized view.
4. Replace fonts, buttons, icons, menus, VFX, app icon, and store screenshots so the new look is consistent from first impression through gameplay.
5. Review representative Trap, Battle, and Adventure screens at phone size and simplify any art that obscures the next move.

## Source and preview files

- `Tools/Art/import-supplied-chess.cjs`: active sheet importer (Node + sharp); subsequent runs use the preserved originals without requiring clipboard files.
- `ArtSource/SuppliedChess/`: six exact originals, normalized front/back lineup, and crop/scale manifest.
- `Assets/Resources/Comic/supplied-*.png`: twelve active full-color runtime sprites, including the boss king variant.
- `Tools/Art/build-chess-art.cjs` and `ArtSource/GraphicChess/`: earlier procedural exploration and poster reference, retained for reference.
- `Assets/Editor/ComicArtPreview.cs`: Unity menu action to render the actual board without starting ads or purchases.
- `Tools/Art/build-impact-art.cjs`: shared hazard, coin, slash, and impact SVGs and sprites.
- `Tools/Art/write-unity-meta.cjs`: stable metadata for the new scripts and shared material when generating outside Unity.
- Superseded explorations remain under `ArtSource/Comic`, `ArtSource/Faceted`, and `ArtSource/GraphicChess`; the game now loads the `supplied-*` set.

The source-layout preview is an art composition, not a screenshot of Unity. Unity import and in-engine rendering still require a valid Editor license on the machine.

## First implementation pass

The current pass prefers the supplied five 3D piece models, preserves the 2D board, adds model-derived 2D shadow casters for the tile sprites, and retains the comic sprites as a fallback. Nightrider reuses the knight model; the boss queen uses the queen model. The earlier sprite-based art, warm board colors, pooled impact effects and jump trails, short defeat beat, smoother UI typography and panels, and reduced-effects settings remain in place. In-engine model import and gameplay visual review remain pending because the installed Editor reports no valid license.

The remaining art rollout includes the older Adventure flag/shield art, power-up and shop illustrations, optional VFX packs, and final app/store marketing assets.
