# In-game HUD rework

Trap, Battle, Daily, Adventure, boss and tutorial presentations share a fixed header and footer. The original gameplay viewport is preserved: modern header allowance 108 units plus the original safe offset, bottom allowance max(76 units, safe inset), identical in all modes including tutorials. The HUD fits within these original allowances. Trap shows score, difficulty, controls and powerups; survival time, biome name and biome progress are removed. Tutorial instructions use the existing header, and lesson actions use the existing footer. The modern HUD no longer creates the camera-dependent floating crest.

## Verification

- 49 HUD states per layout: all 37 adventure levels, eight mode/difficulty combinations, both tutorials, a transformed Nightrider with a large score/multiplier, and the Daily header without consuming an attempt.
- Checked at 720x1280, 720x1600, 1536x2048, and the original notched 1284x2778 simulator. No text overflow or controls outside header/footer containers.
- Pause/resume retains Adventure and Tutorial context. All 10 tutorial captions fit.
- Final game regression suite: 289 passed, 9 failed. The failures match the previous run: eight biome/scenery assertions and one board-coordinate round-trip assertion. No game runtime exceptions.
- Unity compilation and whitespace checks passed.
- Follow-up: 49 HUD states matched the original gameplay viewport exactly, with no layout failures. All 10 tutorial captions fit the compact header.
- Battle follow-up: score, multiplier, chess indicator and controls now share one compact row. Transformation uses the chess icon and radial timer; capture progress uses three dots. Removed the form-name/seconds and advice text. Rechecked all 49 states against the original viewport; full/half/expired radial states and icon reset passed. Preview: hud-battle-radial.png.

## Rendered previews

- hud-rework-trap.png
- hud-rework-battle.png
- hud-rework-adventure.png
- hud-rework-boss.png
- hud-rework-tutorial.png

Legacy presentation remains available. The original Simulator/Game view configuration was restored and the temporary resolution removed. The game is at the menu with normal updates enabled.
