# Optimization pass — 2026-10-15

## Models and textures

| Model | Original triangles | Runtime triangles |
| --- | ---: | ---: |
| Knight | 525,439 | 11,999 |
| Rook | 487,960 | 6,500 |
| Bishop | 573,914 | 8,000 |
| Queen | 436,860 | 10,000 |
| Pawn | 378,811 | 4,999 |

Built through the Blender bridge using `Tools/Art/optimize-pieces.py`, preserving UVs and painted materials. Original sources remain in `ArtSource/Models/ChessPieces`. Their duplicate Resources copies were removed only after SHA-256 equality checks; no scene/asset GUID references to those copies were found. Runtime models now live in `Assets/Resources/Pieces/Optimized`.

Inspected the side-by-side Unity render `ArtSource/Previews/model-optimization-comparison.png`: originals above, runtime models below. Small surface differences remain, but silhouettes, weapons, and color details are retained at gameplay size.

Board models were audited: tiles are 192–432 triangles, hazard plaque 584, scenery 3,616–9,272. They already fit the scene's needs. Mesh imports disable CPU readability, animation, lights, cameras, and colliders; vertex/index optimization is enabled. Piece color textures now use 1024px, mipmaps, opaque texture import, and ASTC 6×6 on Android/iOS.

## Runtime changes

- Cache a piece's model variants so repeated transformations and pool reuse stop destroying/recreating them. Meshes and materials remain shared.
- Retain one movement VFX instance per GameView; one-shot callers still support destruction. Reuse capture property blocks and cap debris at 48 cubes.
- Only resend floor tint property blocks when the biome or tint changes.
- Remove redundant enemy facing history and unused contact-shadow GameObjects.
- Replace two exponentials per piece per floor pixel with a compact soft polynomial shadow falloff.
- Flag cloth uses conservative fixed animation bounds instead of recalculating them every frame.
- Cache enemy timer colors instead of parsing hex values every update.
- Fix the HUD biome label to update on biome changes without requiring a screen resize.
- Escape search uses integer-pair keys and predecessor links. It constructs only the winning path rather than copying routes at every visited square.

Existing UI change detection, nonallocating move-hint buffers, 60 Hz frame cap, and streaming menu audio were inspected and retained.

## Animation and feedback

- Hop reduced from 150 ms to 120 ms, with immediate ease-out departure and a lower 0.24-cell arc.
- Gold ribbon lasts 145 ms; it clears on a new move and does not delay input.
- Capture attack reduced to 140 ms with gold accents. Rook's oversized fist replaced with a compact impact.
- Capture debris reduced from 20 cubes to 12, lasts 220–340 ms rather than 380–520 ms, with smaller shards. Reduced-effects mode uses four short-lived cubes.

## Measurements

Same editor, warmed 1,000-update synthetic 20-piece board; includes BoardView.SyncBackground and GameView.Sync, excludes actual rendering and UI:

| Metric | Before | After |
| --- | ---: | ---: |
| CPU time per presentation update | 0.0522 ms | 0.0320 ms |
| Active GameView mesh triangles | 8,099,030 | 160,331 |
| Escape search across 1,000 seeded boards | 134.1 ms | 29.7 ms |

The escape solver produced the identical route/outcome checksum `-8058822757117587928` across all 1,000 fixtures (657 forward escapes, 343 sealed boards). The regression harness now verifies that checksum, model budgets, model reuse, and VFX reuse as well as gameplay regressions.

The managed allocation counter returned zero even for an explicit 1 MiB allocation probe in this Unity runtime, so it cannot substantiate a zero-allocation claim. Timing numbers are single editor samples, not device FPS or GPU measurements.

## Verification and remaining work

Final regression run: **298 passed, 0 failed, 0 game runtime exceptions**. See `game-regression.md` for individual assertions. Visual review includes the model comparison and in-game board capture. Phone GPU/frame pacing, battery, thermal behavior, and platform build size still require a device build. Live ads, billing, and leaderboard writes are outside these tests. Unrelated Unity AI `NoSubscription` editor errors remain. Unity was returned to Edit mode after verification.
