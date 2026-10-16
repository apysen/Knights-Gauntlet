# Capture slash artwork

Generated using the built-in image generation tool. Original transparent PNGs are preserved here, with runtime copies under `Assets/Resources/Comic/`.

Saved runtime assets:

- `Assets/Resources/Comic/comic-slash-crescent.png`
- `Assets/Resources/Comic/comic-slash-cut.png`
- `Assets/Resources/Comic/comic-slash-sweep.png`

Shader: `Assets/Resources/CaptureSlash.shader`. The Unity-rendered phase preview is `ArtSource/Previews/capture-slash-shader-phases.png`.

All three variants are imported as full-rectangle sprites with alpha transparency. Runtime sizing normalizes sprite bounds so their footprint stays consistent. Captures choose a new variant without immediately repeating the previous one, and independently choose a rotation across the full 360-degree range.


## Crescent

Use case: stylized-concept. Asset type: production 2D game VFX sprite, one single sword-slash mark. Draw a crisp hand-inked manga crescent slash, moving lower-left to upper-right. A thin razor-sharp white blade core with restrained warm cream highlights, confident near-black brush outline, two short tapered trailing speed strokes. Elegant aggressive curving arc with needle points, not a weapon or object. Isolated centered sprite occupying about 80% of a square canvas with generous transparent padding. Designed to read at only 64 pixels on a chess game capture. Flat high-contrast graphic ink, no soft glow, no gradients, no shadow, no background, no scenery, no text, no symbols, no starburst, no sword, no blood. Genuine alpha transparent background. The entire slash must remain within the canvas. Only one coherent slash effect.


## Straight cut

Use case: stylized-concept. Asset type: transparent 2D sword-slash VFX sprite for a manga-style game. One centered clean diagonal razor cut lower-left to upper-right, a mostly straight slightly bowed white blade ribbon tapering to exceptionally sharp points, bold near-black ink contour and a narrow warm cream strip, with two short parallel razor speed streaks. Compact coherent mark occupying central 75% of square with padding on all sides; all stroke endpoints fully within frame. Drawn confidently like manga combat ink, readable at 64px. Flat high contrast, no soft glow, no gradients. Not crescent shaped: this variation is a straight fast cutting stroke. True transparent alpha background, no panel, no weapon, no sword object, no text, no blood, no explosion, no starburst, no environment.


## Swept ribbon (replaces the rejected split brush)

Use case: stylized-concept. Asset type: one transparent 2D manga sword-trail sprite for a chess game's capture effect. Draw ONE single continuous swept ribbon tracing one curved sword swing from lower-left to upper-right. The silhouette is a curved tapered wedge: a hair-thin long trailing tail at lower-left gradually broadens into a strong clean leading blade near upper-right, ending in a sharp tip. Both boundaries describe the same smooth flowing arc. White interior with a restrained warm cream edge, crisp near-black manga ink outline. One connected silhouette only. This is the afterimage of a blade's swept path, not two swords or two parallel cuts. No split, fork, double slash, disconnected streaks, stars, shards, sparks or extra marks. Slightly bowed diagonal sweeping shape, central 78 percent of a square canvas, generous transparent padding, all tips within frame. Strong simple silhouette readable at64px, flat graphic high contrast, no glow, no gradient, no cast shadow. No sword object, character, scenery, blood, text, panels or background. Genuine transparent alpha.
