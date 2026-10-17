# Pixel army art

Seven original static views: rook, bishop and queen front/back; pawn front only.
The red rook carries a tower shield and hammer; the violet bishop has a mitre
and staff; the magenta queen has a crown and rapier; the teal pawn is a hooded scout.

The editable `pixel-army.blend` contains separate rigid body/head/arm/leg groups
for future animation. These are static art assets, not completed animation rigs.
Native 48/64/96 px renders use the knight camera, stepped shading, binary alpha,
one-pixel outline and a common (.5,.22) pivot. Height differences are intentional.
`sprites.json` records bounds, palette and import settings for every view.

Run `build_army.py` in Blender and call `build_all()`, then run `pack_army.py`.
Final PNG assets are in `Assets/Art/PixelArmy/`; review sheets are beside this file.
The existing gameplay pieces have not been replaced by this art pass.
