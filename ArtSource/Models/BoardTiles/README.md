# Royal chess set presentation

The painted chess models establish the palette: navy enamel, sculpted gold trim,
and saturated jewel colors. Quiet tile centers keep routes and threats readable.

- Garden: emerald surfaces, bronze tile edges, jade plants in gold urns.
- Bridge: walnut planks, blue moat inlays, navy piers and gold handrails.
- Castle: moonstone surfaces, navy turrets and gold parapet fillets.
- Accessories: garnet hazard tokens, gold move rings, navy royal goal banner.
- UI: navy chamfered panels, ivory text, gold fillets and matching line icons.

Run `Tools/Art/build-board-tiles.py` inside Blender through the Unity bridge to
regenerate the FBXs. The editable Blender scene is saved beside this file.
The source models use XY ground and positive Z height; the imported Unity models
are Y-up and `BoardView.SurfaceOrientation` stands them on the simulation's XY board.

The project uses a 2D renderer. `ChessPiece.shader` supplies warm key lighting,
cool fill, enamel highlights and soft projected pedestal shadows without changing
the render pipeline. These shadows approximate the pieces' footprints rather than
their full silhouettes. Scenery sits outside the selectable 5 by 10 grid.

Biome changes are recorded against incoming world rows by `BoardBiomeFlow`.
The boundary travels down the board with the tiles, including when scroll rewinds.
Scenery is clipped to matching vertical regions while the common gold frame stays
stable. Tile fragments are clipped in both ground and projected camera coordinates
so slab sidewalls cannot protrude below the perimeter.

Chess pedestals occupy 74% of a tile's width. Selected cosmetic colors recolor
their gold accents through per-renderer shader properties; the source enamel and
enemy materials remain independent.
