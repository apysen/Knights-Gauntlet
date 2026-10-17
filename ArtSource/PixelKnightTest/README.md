# Pixel knight animation proof

Open `preview.html` to play, pause, and step through each pixel resolution.
`board-animation.gif` shows all three resolutions on the current production board.
Left to right: 48, 64, and 96 pixels per square frame, at the same displayed idle height.

This is an art-direction test from behind, with the knight facing his opponents.
It uses a simplified original model, a rigid-part animation control hierarchy,
four three-tone material ramps, a one-pixel outline, and a constant foot pivot.
It has eight idle poses (one second) and three capture poses (250 ms):
raised above the right shoulder for 70 ms, diagonal cut for 35 ms, and a low
finish on the left for 145 ms, with the sword tip pointing toward the ground.
The arm has separate shoulder, elbow and wrist controls.

The slash is a separate Unity GPU animation, active from 70 to 290 ms.
A cubic curve reveals an ivory head, gold wake and eroding tail on a fixed
48/64/96 pixel grid. Point-filtered per-instance render textures match the
body frame and foot pivot. The effect sorts below the knight and above the
board, so the cape occludes the middle of the opponent-facing arc.
There is no baked slash in the body sprite atlas.

`PixelKnightSlash.PlayCapture()` starts the animation on an optional prefab.
The component follows the Capture animator state, hides the effect outside its
220 ms interval and releases its temporary GPU resources when disabled.
The review controllers also cycle automatically for inspection.

The HTML and GIF previews use actual Unity shader captures in `shader-review/`.
There are 21 review samples, but only three body drawings in the attack.
`review-timeline.json` records their timing; GIF rounds to 10 ms increments.
It includes no Meshy geometry, skeletal skinning, walk cycles, or alternate facings.

## Files

- `pixel-knight-animation.blend`: editable animated parts, stepped shading, native-resolution camera.
- `render_knight.py`: recreate the Blender proof from `../Models/EnchantedKnight/enchanted-knight.blend`.
- `pack_sprites.py`: palette/alpha/outline conversion, atlas packing, GIF and HTML export.
- `renders/`: original transparent native Blender frames.
- `validation.txt`: frame bounds and palette checks from the packer.
- `pose-validation.json`: measured sword-tip/grip positions validating the cut.
- `attack-three-poses.png`: the raised, cutting, and finishing poses side by side.
- `knight-capture-slow.gif`: a slowed review of the three attack poses.
- `../../Assets/Art/KnightPixelTest/`: atlases, clips, demo controllers, and optional prefabs.

## Rebuild

1. Run `render_knight.py` in Blender, call `setup()`, then `render_frames(48)`, `render_frames(64)`, and `render_frames(96)`.
2. Run `python ArtSource/PixelKnightTest/pack_sprites.py` from the project root.
3. In Unity, choose **Knight's Gauntlet > Art > Build Pixel Knight Animation Proof**.
4. Choose **Knight's Gauntlet > Art > Capture Pixel Knight Board Proof**, then rerun the packer to update the shader-composited previews and board GIF.

Prefab controllers loop idle and capture automatically for review. Gameplay does
not reference these prefabs; the production theme, pieces, and scene are unchanged.
