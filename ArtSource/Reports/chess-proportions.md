# Chess piece proportions and centering

Source: FIDE Handbook, Standards of Chess Equipment, section 2.2:
https://handbook.fide.com/files/handbook/C02Standards.pdf

Recommended heights are king 9.5 cm, queen 8.5 cm, bishop 7 cm, knight 6 cm, rook 5.5 cm, pawn 5 cm. Bases should be 40–50% of height. The game uses the midpoint, 45%, and a reference square width of 5.5 cm. Nightrider shares the knight model and proportions. There is no separate king model in the current runtime set.

| Model | Reference height | Reference base diameter |
| --- | ---: | ---: |
| Queen | 8.5 cm | 3.825 cm |
| Bishop | 7 cm | 3.15 cm |
| Knight / Nightrider | 6 cm | 2.7 cm |
| Rook | 5.5 cm | 2.475 cm |
| Pawn | 5 cm | 2.25 cm |

The pawn is 58.8% of the queen's height. The stylized meshes are normalized vertically by measured height and horizontally by actual pedestal diameter, keeping the stated heights and base ratios. The former uniform pedestal width and 0.86 height compression no longer control these models. GameManager > Piece Presentation > Piece Scale remains the global multiplier (currently 0.9), and affects all piece sizes together.

## Centering

`Assets/Resources/Pieces/model-metrics.json` is measured from Unity's optimized FBX imports, including the importer rotation and scale. Measurements use actual vertices, with the lowest 10% of the mesh identifying the pedestal. The base's X/Z midpoint is moved to the tile center and its lowest Y to the tile surface before orientation/scaling. This avoids guessing axis signs or centering a bounding box that includes an off-center weapon.

`Assets/Editor/PieceModelMetricsBuilder.cs` provides Tools > Knight's Gauntlet > Rebuild Piece Model Metrics for future model replacements. Regenerate after changing the optimized FBXs. Original Blender meshes and older footprint data are preserved, but the older Blender-space metrics are no longer used for runtime placement.

Shadows and the player's pedestal ring follow the actual base size. Preview: `ArtSource/Previews/chess-proportions-centered.png` (pawn, rook, knight, bishop, queen).

## Verification

All 298 game regression checks passed. Twenty mesh-vertex checks also passed: all five models at both 0°/180° and global scale 0.9/1.1. Each check independently measures pedestal center, contact with the board plane, total height, and base/height ratio. Maximum measured center error was below 0.000001 world units. Results: `chess-proportions-checks.txt`. Unity returned to Edit mode with GameManager selected.
