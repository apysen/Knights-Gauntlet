# Environment pixel filter

Enabled by GameManager on the main camera for modern presentation.
Adjust Target Width (default 320), Outline Strength (.20), Highlight Strength (.08),
Edge Threshold (.12), or Filter Enabled on EnvironmentPixelFilter during Play mode.
Target width is approximate: actual raster uses a whole-number display pixel scale.

Existing tiles and environment assets render through the offscreen forward renderer
at index 1 in UniversalRP; the original 2D renderer remains index 0 for the main camera.
A render-graph pass preserves low-resolution environment depth for correct rolling
crest/scenery occlusion. The composite samples with point filtering, with restrained
color-boundary darkening and highlights. No color posterization or dither is used.
Characters, combat effects, pickups, move markers and UI retain their original rendering.
Camera position, input mapping, board geometry, textures and materials are unchanged.
Screen-space pixel blocks are fixed; normal low-resolution silhouette stepping remains
as the perspective board scrolls. This is not object-space pixel snapping.

Reserved presentation layers: 27 foreground, 28 composite. On disable, original layers
and camera masks are restored and temporary targets released. Legacy 2D bypasses filter.

Verified in Editor: grass/bridge/castle, crest occlusion, scrolling plus capture VFX,
portrait/landscape target resize, dynamic renderer routing, toggle and disable/re-enable,
and legacy bypass. No new script/shader/render errors in final validation; unrelated
Unity AI subscription errors existed previously. Device GPU profiling remains untested.

before-after.png: same frozen board, original and filtered.
scroll-and-slash.gif: actual Unity captures with castle scenery, scrolling and slash.
