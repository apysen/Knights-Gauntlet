# Displays above the board

Implemented the agreed mode displays in `RoyalBoardCrest`.

- Adventure: three gold stars, live rating using the level's existing ThreeStar/TwoStar limits, and moves remaining until the next drop. A lost star expands, rises, and fades over 320 ms with six gold sparks, leaving a dim outline. Reduced effects skips the pop. Restart/new level restores all stars; pause preserves the current rating.
- Trap: progress toward the incoming Drawbridge at 60 seconds, then Castle at 90 seconds. Castle shows survival time and personal best. Best survival time is stored per difficulty, with a separate daily record, when a real completed run is finalized. Previous versions did not store survival-time records, so this begins tracking them now.
- Battle: live score multiplier, three capture-progress pips, and the original HTML chess glyphs (♜, ♝, ♞, ♛, ♘) with their original piece colors. While transformed, the icon changes to the current form and gets a draining gold ring and seconds remaining. Multiplier increases and transformation activation give a short scale pulse. No piece name is displayed.

Removed the duplicate transformation badge/timer and Battle capture pips from their former locations. Adventure transformation pips still work. Restricted pieces and move hints to the board's visible row range to prevent incoming objects crowding the new display.

UI children are allocated once, strings update on changed values, and decorative graphics do not intercept input. Placement uses camera projection and available space above the board.

## Checks

15 focused runtime checks passed (`mode-hud-checks.txt`): both inclusive star boundaries, both star-drop triggers, completed pop, restart, pause/resume, biome progress, castle clock, multiplier, capture count, current form icon, countdown, and expiry. Rendered and inspected Adventure, Trap, and transformed Battle previews. Full game regression results are in `game-regression.md`.

Previews: `ArtSource/Previews/adventure-stars-hud.png`, `trap-progress-hud.png`, and `battle-transformation-hud.png`.
