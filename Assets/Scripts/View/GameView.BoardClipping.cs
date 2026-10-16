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
