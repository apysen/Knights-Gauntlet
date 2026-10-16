# Changelog

Changes made to Knight's Gauntlet are recorded here. Dates mark the current project work, not a published release.

## [Unreleased] — 2026-10-15

### Art direction and piece assets

- Established the current art direction in `ART_STYLE.md`: saturated chess-piece colors, bold near-black shadow shapes, sharp ivory highlights, gold trim, clear silhouettes, restrained detail, and consistent upper-left lighting.
- Adopted the supplied front- and back-facing chess-piece artwork and models. Added five Blender models (pawn, rook, knight, bishop, and queen) and twelve front/back runtime sprites, including a king sprite used for the Adventure boss appearance.
- Added model rendering to `PieceView`, with sprite and glyph fallbacks for traps, custom skins, and unavailable models. Nightrider reuses the knight model; the Adventure hunter queen uses the king appearance.
- Added a painted-piece material that preserves the default artwork palette and recolors eligible body regions for alternate skins while protecting the gold trim, dark ink, and ivory highlights.
- Added model-derived 2D shadow casters and lighting so 3D pieces can cast shadows onto the 2D board tiles.
- Added comic-style impact, slash, coin, and trap artwork, plus an in-Editor board preview utility and source/import tools for the supplied art.
- Added a runtime-built 3D Adventure goal flag with a shaded banner and subtle cloth motion.

### Board and world presentation

- Added grass, drawbridge, and castle-stone biome tile sheets, with ten usable variants per biome.
- Randomized tile variants while preventing identical neighboring tiles. Drawbridge edge rails are separate overlays: the left rail appears at the left edge and the right rail at the right edge.
- Kept the 5×10 board and square 2D tiles. Trap and Battle progress from grass to drawbridge to castle stone; Adventure selects its biome by world.
- Added comic-style power-up and interface icons for Chronokinesis, Holy Cross, Pathfinder, cracked tiles, and retry.

### Movement, combat, and feedback

- Added short movement trails and whole-model capture motions, with capture impact effects anchored to the player's landing square. Captures briefly hold the player's attack pose while the board keeps scrolling.
- Added a slash effect to captures in every player form, with angle and scale adjusted by form.
- Kept captured enemies' removal immediate to preserve board readability; the impact effect communicates the capture without a separate enemy death animation.
- Added larger, longer-lived voxel bursts when pieces are eliminated and a brief defeat hit/impact beat. The burst shader draws through board depth so cubes remain visible in the Built-in Render Pipeline.
- Made transient movement effects, attack motions, impacts, particles, popups, and camera shake cancel when the player makes another successful move. Effects remain view-only and do not hold up input or simulation.
- Added reduced-effects and reduced-shake controls, with shorter/simpler combat feedback when reduced effects are enabled.
- Connected movement VFX to successful moves in Tutorial, Adventure, and Battle.

### Shop and rewards

- Removed the loot-box opening flow, odds presentation, randomized cosmetic rewards, related UI/audio behavior, and loot-box references from the shop experience.
- Replaced randomized cosmetic acquisition with direct skin and VFX purchases for in-game shillings. Existing owned skin and VFX unlocks remain supported.
- Kept the separate Shillings purchase screen.

### Documentation and implementation status

- Added `ART_STYLE.md` as the art-direction and production reference, including the piece palette, lighting rules, board treatment, animation principles, and asset workflow.
- Added this changelog to summarize the art, board, feedback, and shop changes currently represented in the project.
- Added the pinned MCP for Unity 10.3.0 package to the Unity project and registered its local stdio server in Codex. The Editor bridge will activate after Unity resolves the package and Codex reloads its MCP configuration.
- Unity model import and in-engine visual review are still pending: the installed Unity Editor reported that it has no valid license. The source assets and implementation are present, but this changelog does not claim a successful in-Editor validation.

