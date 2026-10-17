# Concept-aligned pixel army

Authoritative reference: ArtSource/Concepts/CharacterMockups/six-piece-lineup.png.
Seven views: rook/bishop/queen SOUTH front and NORTH rear; pawn SOUTH front only.
Cardinal views have square shoulders and centered head/torso, no diagonal yaw.
Headgear exaggerates rook battlements, bishop diagonal slit, queen crown and pawn ball.
Generated transparent source sheets are retained here. These are raster art;
the earlier procedural Blender models do not reproduce these revised designs.

Final sprites: Assets/Art/PixelArmy/ConceptAligned/{48,64,96}/.
Point filtering, no mipmaps/compression, common foot pivot (.5,.20), full rect.
24 colors per character, shared across front and back; binary alpha.
The packing script crops and normalizes existing art without drawing new features.
The pawn is intentionally smaller; bishop and queen are taller than the rook.
This pass does not replace the live game pieces or add animations.
