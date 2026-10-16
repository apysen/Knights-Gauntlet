# Gameplay QA findings — October 15, 2026

## Fixed

- A completed movement VFX leaves a destroyed Unity object reference. C# null
  conditional calls bypass Unity's null comparison, causing the next move and
  restart to throw before completing. Cancellation now uses Unity-aware checks.
- Player and enemy models switched to the back pose on upward moves. Models now
  retain their front orientation; rapid moves continue from the visible position.
- Chess models reduced from 84% to 74% of tile width while retaining centered feet.
- Scrolling tile sidewalls could project past the board perimeter. Clipping now
  accounts for slab depth and the tilted camera.
- Whole-board biome switches replaced by persistent incoming row boundaries.
  Scenery transitions down the sides with the same boundary. Rewinds preserve it.
- Adventure scenery now persists while paused. New rounds reset terrain history.
- Adventure flags, coaching markers and boss HUD could leak between modes.
- Pathfinder's cheap route validation checked collapsed landing cells but missed
  intervening collapsed cells during sliding moves in Apotheosis. It now validates
  those cells and requests a collapse-aware route. Unsafe fallback hints removed.
- Dead simulations reject moves. Tutorial setup clears scrolling adventure state.
- Equipped cosmetic colors now affect the 3D player's metal accents independently
  of the source enamel and enemy materials.

## Verification

See `game-regression.md`: 287 checks passed, zero failed, zero game runtime
exceptions. Includes expired effects, eight repeated hops, rapid movement,
death/restart and mode switching across all eight survival configurations, all 37
adventure layouts, replayed puzzle and boss-phase solutions, fractional-scroll
render checks, projected cell targeting, cosmetics and major UI navigation.

Rerun in Play mode from **Tools > Knight's Gauntlet > Run Play Mode Regression
Checks**, or invoke `GameRegressionChecks.Start()` through Unity MCP. QA runs
suppress publishing scores and awarding currency; puzzle solutions use isolated
simulations. Live billing, ad delivery and online leaderboard submission were not
exercised. Daily seeded rules are checked without consuming daily attempts.

## Remaining observations

- Source piece meshes are heavy for mobile. Imported triangle counts: knight
  525,439; rook 487,960; bishop 573,914; queen 436,860; pawn 378,811. A visual LOD
  and device profiling pass is advisable. These figures measure source complexity,
  not device frame rates.
- The Unity AI editor package logged `NoSubscription` errors from
  `generators.ai.unity.com`. These originate in the editor package, outside the
  game's scripts. No subscription, plugin or account configuration was changed.
