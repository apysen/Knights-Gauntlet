# Ceramic trio exploration

Three actual 3D prototypes: a cornflower knight, terracotta rook and sea-glass bishop.
They use broad silhouettes, a short shared pedestal, a narrow unglazed foot and a
matte shader. The bishop's point and slit are exaggerated for the overhead camera.
The knight retains its profile in both facings; the rook has a square crown.

## Review

- `board-comparison.png`: the supplied models on the left and the prototypes on the
  right, including both facings on the production board at the production piece scale.
- `ceramic-lineup.png`: a closer view of the three prototypes on the same board.
- `gameplay-prototype.png`: the prototype rendered in the actual game HUD.

In Unity, choose **Knight's Gauntlet > Art > Capture Ceramic Trio Comparison** to
regenerate the first two images. This creates an isolated additive scene and closes
it after rendering; the active gameplay scene is restored.

Choose **Knight's Gauntlet > Art > Use Ceramic Trio Prototype**, then press Play,
to try the trio in the game. The checked menu item enables
`GameTheme.ceramicPiecePrototype`. The saved default is off. Queen, pawn and traps
continue to use their existing models in this three-piece exploration. Nightrider
uses the prototype knight. Custom sprite overrides and legacy 2D retain their paths.
Player skins recolor the ceramic body while preserving the foot and recessed details.

## Sources

- `Tools/Art/build-ceramic-prototypes.py`: repeatable Blender mesh construction/export.
- `ArtSource/Models/CeramicPrototype/ceramic-trio.blend`: editable standalone source.
- `ArtSource/Models/CeramicPrototype/build-report.json`: exported mesh counts.
- `Assets/Resources/Pieces/CeramicPrototype/`: FBX models and matte shader.

Run the Python source through the existing Unity Blender bridge. It creates a
separate scene, exports the models, writes the standalone source and restores the
previous Blender scene. The supplied model files are preserved.
