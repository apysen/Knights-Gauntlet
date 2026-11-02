using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // A skin's icon: the real 3D player knight (the production PieceView, so skin
    // materials, the Nightfall model and any future skin art all show up exactly as
    // in game) rendered into a small texture. It is drawn once each time the icon is
    // shown instead of every frame, so a full grid of skins costs next to nothing.
    // Same offscreen-stage approach as MenuKnightPreview and PowerupModelIcon.
    [RequireComponent(typeof(RawImage))]
    public sealed class SkinKnightIcon : MonoBehaviour
    {
        private const int TextureSize = 160;
        private const float Fill = .86f;     // share of the icon the knight fills
        private const int StageLayer = 31;   // shared with the other UI stages; each sits far apart
        private static int _stages;

        private GameObject _stage;
        private PieceView _piece;
        private Camera _camera;
        private RenderTexture _texture;
        private bool _dirty = true;

        // referenceCamera (optional) supplies the menu's renderer settings, as the menu
        // knight portrait does. Returns false if the knight could not be built.
        public bool Initialize(GameTheme theme, string skinId, Camera referenceCamera = null)
        {
            if (theme == null) return false;
            _stage = new GameObject("SkinIconStage");
            _stage.transform.position = new Vector3(3000f + 40f * _stages++, 0f, 0f);
            var model = new GameObject("Knight", typeof(SpriteRenderer), typeof(PieceView));
            model.transform.SetParent(_stage.transform, false);
            _piece = model.GetComponent<PieceView>();
            _piece.enabled = false; // icon pieces must not add shadows to the gameplay board
            _piece.spriteRenderer = model.GetComponent<SpriteRenderer>();
            _piece.Apply(theme, PieceType.Knight, true, skinId);
            _piece.SetModelRotation(0f);
            foreach (var t in _stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = StageLayer;
            // Production shaders may clip to board coordinates; this stage has no board.
            foreach (var r in _piece.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetFloat("_PieceClip", 0f);
                r.SetPropertyBlock(block);
            }

            var cameraObject = new GameObject("SkinIconCamera", typeof(Camera));
            cameraObject.transform.SetParent(_stage.transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            if (referenceCamera != null) _camera.CopyFrom(referenceCamera);
            _camera.enabled = false; // rendered by hand, only when shown
            _camera.cullingMask = 1 << StageLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.orthographic = true;
            _camera.aspect = 1f;
            _camera.orthographicSize = 1f;
            _camera.nearClipPlane = .01f; _camera.farClipPlane = 50f;
            _camera.allowHDR = false; _camera.allowMSAA = false;

            _texture = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32)
                { name = "Skin icon " + skinId, antiAliasing = 4 };
            _texture.Create();
            _camera.targetTexture = _texture;

            // Models stand along -Z on the board; looking along +Y shows them upright,
            // the same view as the menu portrait. Then frame the knight's real extent.
            var angle = Quaternion.Euler(-90f, 0f, 0f);
            _camera.transform.SetPositionAndRotation(_stage.transform.position - angle * Vector3.forward * 15f, angle);
            Rect bounds = MenuKnightPreview.ProjectedRect(_piece, _camera);
            float half = TextureSize * .5f;
            Vector3 offset = _camera.transform.right * (bounds.center.x - half) +
                             _camera.transform.up * (bounds.center.y - half);
            _camera.transform.position += offset * (_camera.orthographicSize * 2f / TextureSize);
            _camera.orthographicSize *= Mathf.Max(bounds.width, bounds.height) / (TextureSize * Fill);

            var image = GetComponent<RawImage>();
            image.texture = _texture;
            image.raycastTarget = false;
            _dirty = true;
            return true;
        }

        // Render textures can lose their contents (app backgrounded, device reset), so
        // redraw whenever the icon comes back on screen.
        private void OnEnable() => _dirty = true;
        private void OnApplicationPause(bool paused) { if (!paused) _dirty = true; }

        // The rendered icon, for sharing one render between many images (SkinIconCache).
        public Texture Texture => _texture;

        private void LateUpdate()
        {
            if (!_dirty || _camera == null) return;
            _dirty = false;
            if (!_texture.IsCreated()) _texture.Create();
            Render();
        }

        // Public so editor tools can capture the icon without entering Play mode.
        public void Render()
        {
            if (_camera != null) _camera.Render();
        }

        private void OnDestroy()
        {
            if (_texture != null) { _texture.Release(); Discard(_texture); }
            if (_stage != null) Discard(_stage);
        }

        private static void Discard(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }

    // One shared icon per skin, for lists where the same skins repeat many times (the
    // leaderboard). Each skin is rendered once, then every row's RawImage shows that same
    // texture, so a long leaderboard costs at most one small render per skin.
    public static class SkinIconCache
    {
        private static GameObject _root;
        private static GameTheme _theme;
        private static readonly Dictionary<string, SkinKnightIcon> Icons = new Dictionary<string, SkinKnightIcon>();

        // Null if the icon can't be built (no theme, model failed to load).
        public static Texture Get(GameTheme theme, string skinId)
        {
            if (theme == null || string.IsNullOrEmpty(skinId)) return null;
            // Domain reload is off, so entries from an earlier Play session are destroyed
            // objects; a missing root (or a new theme) means start over.
            if (_root == null || _theme != theme) Clear();
            _theme = theme;
            if (Icons.TryGetValue(skinId, out var cached) && cached != null) return cached.Texture;
            if (_root == null) _root = new GameObject("SkinIconCache");
            var go = new GameObject("Icon " + skinId, typeof(RectTransform), typeof(RawImage), typeof(SkinKnightIcon));
            go.transform.SetParent(_root.transform, false);
            var icon = go.GetComponent<SkinKnightIcon>();
            if (!icon.Initialize(theme, skinId, Camera.main))
            {
                Object.Destroy(go);
                return null;
            }
            Icons[skinId] = icon;
            return icon.Texture;
        }

        private static void Clear()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            Icons.Clear();
        }
    }
}
