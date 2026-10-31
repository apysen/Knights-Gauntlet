using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    public partial class GameView
    {
        private Material _boardSpriteMaterial;
        private readonly Dictionary<SpriteRenderer, Material> _boardSpriteDefaults = new Dictionary<SpriteRenderer, Material>();

        private void ClipBoardSprite(SpriteRenderer sprite)
        {
            sprite.localBounds = new Bounds(Vector3.zero, Vector3.one * 40f);
            if (!_boardSpriteDefaults.ContainsKey(sprite)) _boardSpriteDefaults.Add(sprite, sprite.sharedMaterial);
            if (_boardSpriteMaterial == null)
            {
                var shader = Resources.Load<Shader>("BoardClipSprite");
                if (shader == null) return;
                _boardSpriteMaterial = new Material(shader) { name = "Board edge mask" };
            }
            sprite.sharedMaterial = _legacy2D ? _boardSpriteDefaults[sprite] : _boardSpriteMaterial;
        }

        // Tile overlays (threats, hints, best-move and path markers) ride the rolling
        // board. The clip shader bends vertices, but a plain sprite is one quad, so a tile
        // straddling the curve would stay a flat chord. These share the board shader and
        // swap to a copy of their sprite subdivided into a grid that can follow the curve.
        private const int BendGrid = 8;
        private static readonly Dictionary<Sprite, Sprite> BendableSprites = new Dictionary<Sprite, Sprite>();

        private void BendWithBoard(SpriteRenderer sprite)
        {
            sprite.sprite = Bendable(sprite.sprite);
            ClipBoardSprite(sprite);
        }

        private static Sprite Bendable(Sprite source)
        {
            if (source == null) return null;
            if (BendableSprites.TryGetValue(source, out var bent) && bent != null) return bent;
            if (BendableSprites.ContainsValue(source)) return source; // already subdivided
            try
            {
                var rect = source.rect;
                bent = Sprite.Create(source.texture, rect, new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height),
                    source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
                bent.name = source.name + " (bendable)";
                var vertices = new Vector2[(BendGrid + 1) * (BendGrid + 1)];
                for (int y = 0; y <= BendGrid; y++)
                    for (int x = 0; x <= BendGrid; x++)
                        vertices[y * (BendGrid + 1) + x] = new Vector2(rect.width * x / BendGrid, rect.height * y / BendGrid);
                var triangles = new ushort[BendGrid * BendGrid * 6];
                int t = 0;
                for (int y = 0; y < BendGrid; y++)
                    for (int x = 0; x < BendGrid; x++)
                    {
                        ushort a = (ushort)(y * (BendGrid + 1) + x), b = (ushort)(a + 1);
                        ushort c = (ushort)(a + BendGrid + 1), d = (ushort)(c + 1);
                        triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                        triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                    }
                bent.OverrideGeometry(vertices, triangles);
            }
            catch (System.Exception)
            {
                bent = source; // e.g. an atlas-packed theme sprite: keep the flat quad
            }
            BendableSprites[source] = bent;
            return bent;
        }

        private void RefreshBoardSpriteClipping()
        {
            foreach (var pair in _boardSpriteDefaults)
                if (pair.Key != null) pair.Key.sharedMaterial = _legacy2D ? pair.Value : _boardSpriteMaterial;
        }

        private void DisposeBoardSpriteMaterial()
        {
            if (_boardSpriteMaterial == null) return;
            if (Application.isPlaying) Destroy(_boardSpriteMaterial);
            else DestroyImmediate(_boardSpriteMaterial);
        }
    }
}
