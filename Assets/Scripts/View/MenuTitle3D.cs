using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // The main menu's 3D title (Resources/TitleModels/GameTitle, built in Blender from
    // ArtSource/Title/GameTitle.blend): gold Cinzel lettering with an ink backing and a
    // longsword through the middle. Rendered by its own camera into a wide texture, the
    // same offscreen-stage approach as PowerupModelIcon.
    //
    // Each "Letter" child is animated on its own:
    //   - a slow wave rolls along the letters (a gentle bob)
    //   - every few seconds a shine sweeps left to right, brightening each letter's gold
    //   - PlayEntrance drops the letters in one after another, landing with a squash
    // The whole title also rocks very slightly so the gold catches the light.
    [RequireComponent(typeof(RawImage))]
    public sealed class MenuTitle3D : MonoBehaviour
    {
        private const int TextureWidth = 1024, TextureHeight = 448;
        private const int StageLayer = 31;
        private const float ShinePeriod = 7f;      // seconds between shine sweeps
        private const float ShineDuration = 1.1f;  // seconds one sweep takes to cross
        private const float WaveHeight = .035f;    // letter bob, in model units
        private const float DropTime = .42f, DropStagger = .045f, DropHeight = 1.6f;
        private static int _stages;

        private sealed class Letter
        {
            public Transform t; public Renderer r; public Vector3 rest; public float x01; public int goldSlot = -1;
            public MaterialPropertyBlock block; public Color gold;
        }

        private readonly List<Letter> _letters = new List<Letter>();
        private GameObject _stage;
        private Transform _pivot;
        private Camera _camera;
        private RenderTexture _texture;
        private float _time, _entranceT = -1f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public bool Initialize(string resourcePath = "TitleModels/GameTitle")
        {
            var asset = Resources.Load<GameObject>(resourcePath);
            if (asset == null) return false;
            _stage = new GameObject("TitleStage");
            _stage.transform.position = new Vector3(-1000f - 20f * _stages++, -60f, 0f);
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_stage.transform, false);
            var model = Instantiate(asset, _pivot, false);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = StageLayer;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) PowerupModelIcon.Convert(r);
            // Imported Z-up/-Y-front like the powerup models: turn the front to the camera.
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var bounds = WorldBounds(model);
            model.transform.position -= bounds.center - _pivot.position;
            bounds = WorldBounds(model);

            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith("Letter")) continue;
                // Every letter's mesh is modelled in place around the model origin, so give it
                // a pivot at its own base: it can then bob and squash where it stands.
                var rb = r.bounds;
                var pivot = new GameObject(r.name + "Pivot").transform;
                pivot.SetParent(r.transform.parent, false);
                pivot.position = new Vector3(rb.center.x, rb.min.y, rb.center.z);
                pivot.gameObject.layer = StageLayer;
                r.transform.SetParent(pivot, true);
                var l = new Letter { t = pivot, r = r, rest = pivot.localPosition, block = new MaterialPropertyBlock() };
                // Note x order from the camera's view (the model is turned 180 degrees).
                l.x01 = Mathf.InverseLerp(bounds.max.x, bounds.min.x, r.bounds.center.x);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.StartsWith("TitleGold")) { l.goldSlot = i; l.gold = mats[i].GetColor(BaseColorId); }
                _letters.Add(l);
            }
            _letters.Sort((a, b) => a.x01.CompareTo(b.x01));

            var camObject = new GameObject("TitleCamera", typeof(Camera));
            camObject.transform.SetParent(_stage.transform, false);
            camObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            _camera = camObject.GetComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.aspect = (float)TextureWidth / TextureHeight;
            _camera.nearClipPlane = .1f; _camera.farClipPlane = 20f;
            _camera.cullingMask = 1 << StageLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.allowHDR = false; _camera.allowMSAA = false;
            // Fit the width (the title is wide), with headroom for the bob and the drop's squash.
            float halfW = bounds.extents.x * 1.04f;
            _camera.orthographicSize = Mathf.Max(bounds.extents.y * 1.15f, halfW / _camera.aspect);

            _texture = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.ARGB32)
                { name = "Menu title", antiAliasing = 4 };
            _texture.Create();
            _camera.targetTexture = _texture;
            var image = GetComponent<RawImage>();
            image.texture = _texture; image.color = Color.white; image.raycastTarget = false;
            return true;
        }

        // Letters fall in one after another, then the shine sweeps straight away.
        public void PlayEntrance()
        {
            if (SaveData.IsReducedEffects()) { _entranceT = -1f; return; }
            _entranceT = 0f;
            _time = ShinePeriod - DropTime - DropStagger * _letters.Count - .1f; // shine right after landing
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            float dt = Time.unscaledDeltaTime;
            _time += dt;
            bool reduced = SaveData.IsReducedEffects();
            if (_entranceT >= 0f) _entranceT += dt;

            // A slight rock so the metal catches the light.
            _pivot.localRotation = reduced ? Quaternion.identity
                : Quaternion.Euler(3f * Mathf.Sin(_time * .9f), 6f * Mathf.Sin(_time * .55f), 0f);

            float sweep = Mathf.Repeat(_time, ShinePeriod) / ShineDuration; // 0..1 while sweeping
            for (int i = 0; i < _letters.Count; i++)
            {
                var l = _letters[i];
                Vector3 p = l.rest;
                float sy = 1f, sx = 1f;
                if (!reduced) p.y += WaveHeight * Mathf.Sin(_time * 2.4f - l.x01 * 5f);
                if (_entranceT >= 0f)
                {
                    float u = (_entranceT - i * DropStagger) / DropTime;
                    if (u < 0f) { p.y += DropHeight; }
                    else if (u < 1f)
                    {
                        // Fall with gravity, land, squash and recover.
                        float fall = Mathf.Clamp01(u / .6f);
                        p.y += DropHeight * (1f - fall * fall);
                        if (u > .6f)
                        {
                            float k = Mathf.Sin((u - .6f) / .4f * Mathf.PI);
                            sy = 1f - .22f * k; sx = 1f + .12f * k;
                        }
                    }
                }
                l.t.localPosition = p;
                l.t.localScale = new Vector3(sx, sy, 1f);

                if (l.goldSlot >= 0)
                {
                    float d = sweep - l.x01 * .8f;
                    float shine = reduced ? 0f : Mathf.Clamp01(1f - Mathf.Abs(d - .1f) / .12f);
                    l.block.SetColor(BaseColorId, Color.Lerp(l.gold, new Color(1f, .97f, .85f, 1f), shine * .75f));
                    l.r.SetPropertyBlock(l.block, l.goldSlot);
                }
            }
            if (_entranceT > DropTime + DropStagger * _letters.Count + .2f) _entranceT = -1f;
            _camera.Render();
        }

        private static Bounds WorldBounds(GameObject root)
        {
            var rs = root.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(root.transform.position, Vector3.one);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private void OnDestroy()
        {
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
            if (_stage != null) Destroy(_stage);
        }
    }
}
