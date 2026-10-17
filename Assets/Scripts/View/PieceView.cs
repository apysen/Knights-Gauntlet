using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Goes on the piece prefab. Reads the theme and configures itself for a given
    // PieceType. It prefers imported 3D models, while retaining sprite/glyph fallback
    // paths for traps, custom skins, and projects where a model has not imported.
    //
    // Prefab layout (suggested):
    //   PieceView (this, + SpriteRenderer)   <- the piece sprite
    //   └─ Backing  (SpriteRenderer)         <- optional translucent square
    //   └─ Glyph    (TextMeshPro)            <- optional, for the no-sprite fallback
    [RequireComponent(typeof(SpriteRenderer))]
    public class PieceView : MonoBehaviour
    {
        public SpriteRenderer spriteRenderer;
        public SpriteRenderer backing;   // optional
        public TextMeshPro glyphLabel;   // optional (world-space TMP)

        // Cache of the last-applied appearance inputs. Sync() calls Apply() every frame
        // for every pooled piece, but the look only depends on these four values, so we
        // skip the (TMP mesh-rebuilding) rework unless one of them changed.
        private GameTheme _aTheme;
        private PieceType _aType;
        private bool _aIsPlayer;
        private bool _aBoss;
        private string _aSkinId;
        private bool _aValid;
        private Sprite _idleSprite, _attackSprite, _hitSprite;
        private Sprite _frontIdle, _frontAttack, _frontHit, _backIdle, _backAttack, _backHit;
        private PieceFacing _facing;
        private float _modelYaw;
        private ComicPose _pose;
        private Material _defaultMaterial;
        private float _poseUntil;
        private GameObject _pixelKnight;
        private PixelKnightSlash _pixelSlash;
        public bool UsesPixelKnight => _pixelKnight != null && _pixelKnight.activeSelf;
        private Transform _modelRoot;
        private Renderer[] _modelRenderers;
        private string _modelKey;
        private Vector3 _modelRestPosition, _modelRestScale;
        private Quaternion _modelRestRotation;
        private Vector2 _attackDirection = Vector2.up;
        private PieceType _motionType;
        private float _motionTime, _motionDuration;
        private bool _motionActive;
        private bool _hopActive;
        private Vector3 _hopOffset;
        private float _hopTime, _hopDuration;
        private sealed class CachedModel
        {
            public Transform root;
            public Renderer[] renderers;
        }
        private readonly Dictionary<string, CachedModel> _models = new Dictionary<string, CachedModel>();
        private MaterialPropertyBlock _accentProperties;
        private bool _modelIsCeramic, _aCeramic;
        private bool _modelIsPorcelain, _aPorcelain;
        private static readonly Dictionary<EntityId, Material> ModelMaterials = new Dictionary<EntityId, Material>();
        [Serializable] private sealed class ModelMetricsFile { public ModelMetrics[] items; }
        [Serializable] private sealed class ModelMetrics
        {
            public string name;
            public float height, diameter, centerX, centerZ, bottom;
        }
        private static ModelMetricsFile _metrics;
        // FIDE reference heights on a 5.5 cm square; bases use the midpoint of
        // the recommended 40–50% of height. GameManager's scale remains global.
        private static float ReferenceHeight(PieceType type) => type switch
        {
            PieceType.Queen => 8.5f / 5.5f,
            PieceType.Bishop => 7f / 5.5f,
            PieceType.Knight or PieceType.Nightrider => 6f / 5.5f,
            PieceType.Rook => 5.5f / 5.5f,
            PieceType.Pawn => 5f / 5.5f,
            _ => 6f / 5.5f
        };
        private static readonly List<PieceView> ActivePieces = new List<PieceView>();
        public Vector3 VisualPosition => _modelRoot != null ? _modelRoot.position : transform.position;
        public float BaseDiameter => _aType == PieceType.Trap ? .64f : (_modelIsCeramic || _modelIsPorcelain) ? .68f : ReferenceHeight(_aType) * .45f;

        private void OnEnable() { if (!ActivePieces.Contains(this)) ActivePieces.Add(this); }
        private void OnDisable() { ActivePieces.Remove(this); }

        // Soft projected pedestal shadows work with the project's 2D renderer.
        public static int WriteBoardShadows(Vector4[] destination)
        {
            int count = 0;
            foreach (var piece in ActivePieces)
            {
                if (piece == null || piece._aType == PieceType.Trap || piece._modelRoot == null ||
                    !piece._modelRoot.gameObject.activeInHierarchy) continue;
                var position = piece._modelRoot.position;
                if (Mathf.Abs(position.y) > 5.6f) continue;
                destination[count++] = new Vector4(position.x, position.y, piece.BaseDiameter * .5f * piece.transform.lossyScale.x, 1f);
                if (count == destination.Length) break;
            }
            return count;
        }

        // A held illustration; input and simulation never wait on this timer.
        public void ShowPose(ComicPose pose, float duration = 0.095f)
        {
            if (spriteRenderer == null || !spriteRenderer.enabled) return;
            var sprite = pose == ComicPose.Attack ? _attackSprite : _hitSprite;
            if (sprite == null) return;
            spriteRenderer.sprite = sprite;
            _pose = pose;
            _poseUntil = Time.unscaledTime + duration;
        }

        public void RefreshPose()
        {
            if (_poseUntil > 0f && Time.unscaledTime >= _poseUntil)
            {
                _poseUntil = 0f;
                _pose = ComicPose.Idle;
                spriteRenderer.sprite = _idleSprite;
            }
        }

        public void ClearPose()
        {
            if (UsesPixelKnight) _pixelSlash.StopCapture();
            _poseUntil = 0f;
            _pose = ComicPose.Idle;
            if (spriteRenderer != null) spriteRenderer.sprite = _idleSprite;
            CancelMotion();
        }

        public void PlayAttackMotion(PieceType form, Vector2 direction, float duration = 0.32f)
        {
            if (UsesPixelKnight) { _pixelSlash.PlayCapture(); return; }
            if (_modelRoot == null || !_modelRoot.gameObject.activeInHierarchy)
            {
                ShowPose(ComicPose.Attack, duration);
                return;
            }
            if (!_hopActive) CacheModelRestPose();
            _motionType = form;
            _attackDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            _motionTime = 0f;
            _motionDuration = Mathf.Max(0.08f, duration);
            _motionActive = true;
        }

        public void PlayHitMotion(float duration = 0.18f)
        {
            if (_modelRoot == null || !_modelRoot.gameObject.activeInHierarchy)
            {
                ShowPose(ComicPose.Hit, duration);
                return;
            }
            if (!_hopActive) CacheModelRestPose();
            _motionType = PieceType.Knight;
            _attackDirection = Vector2.down;
            _motionTime = 0f;
            _motionDuration = Mathf.Max(0.08f, duration);
            _motionActive = true;
        }

        public void CancelMotion()
        {
            _motionActive = false;
            _hopActive = false;
            if (_modelRoot == null) return;
            _modelRoot.localPosition = _modelRestPosition;
            _modelRoot.localRotation = _modelRestRotation;
            _modelRoot.localScale = _modelRestScale;
        }

        private void CacheModelRestPose()
        {
            _modelRestPosition = _modelRoot.localPosition;
            _modelRestRotation = _modelRoot.localRotation;
            _modelRestScale = _modelRoot.localScale;
        }

        public const float HopDuration = 0.12f;
        public void PlayHop(Vector3 fromOffset, float duration = HopDuration)
        {
            if (_modelRoot == null || !_modelRoot.gameObject.activeInHierarchy) return;
            CancelMotion();
            _hopOffset = fromOffset;
            _hopTime = 0f;
            _hopDuration = duration;
            _hopActive = true;
            _modelRoot.localPosition = _modelRestPosition + fromOffset;
        }

        private void LateUpdate()
        {
            if (UsesPixelKnight && Camera.main != null)
                _pixelKnight.transform.rotation = Camera.main.transform.rotation;
            if (_modelRoot == null) return;
            Vector3 hop = Vector3.zero;
            bool wasHopping = _hopActive;
            if (_hopActive)
            {
                _hopTime += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(_hopTime / _hopDuration);
                // Immediate departure, a quick readable arc, then a soft landing.
                float remaining = 1f - progress;
                hop = _hopOffset * (remaining * remaining) +
                    Vector3.back * (Mathf.Sin(progress * Mathf.PI) * 0.24f);
                if (progress >= 1f) _hopActive = false;
            }
            if (!_motionActive)
            {
                if (wasHopping) _modelRoot.localPosition = _modelRestPosition + hop;
                return;
            }
            _motionTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_motionTime / _motionDuration);
            // Draw back, snap into contact, hold through a short impact stun, then
            // recover. This only poses the model child, so the board keeps scrolling.
            float strike = t < 0.14f
                ? Mathf.Lerp(0f, -0.22f, Mathf.SmoothStep(0f, 1f, t / 0.14f))
                : t < 0.36f
                    ? Mathf.Lerp(-0.22f, 1.08f, Mathf.SmoothStep(0f, 1f, (t - 0.14f) / 0.22f))
                    : t < 0.62f
                        ? 1.08f
                        : 1.08f * (1f - Mathf.SmoothStep(0f, 1f, (t - 0.62f) / 0.38f));
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.36f, t)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, t)));

            float lean = (_motionType == PieceType.Knight || _motionType == PieceType.Nightrider) ? 22f :
                _motionType == PieceType.Rook ? 12f : _motionType == PieceType.Bishop ? 9f :
                _motionType == PieceType.Queen ? 14f : 7f;
            Vector3 axis = Vector3.Cross(Vector3.back, new Vector3(_attackDirection.x, _attackDirection.y, 0f));
            float reach = _motionType == PieceType.Rook ? 0.13f : 0.09f;
            _modelRoot.localPosition = _modelRestPosition + hop +
                new Vector3(_attackDirection.x, _attackDirection.y, 0f) * (reach * strike);
            _modelRoot.localRotation = Quaternion.AngleAxis(lean * envelope, axis) * _modelRestRotation;

            float squash = _motionType == PieceType.Rook ? 0.06f : 0.035f;
            float scalePulse = squash * envelope;
            _modelRoot.localScale = Vector3.Scale(_modelRestScale,
                new Vector3(1f + scalePulse, 1f - scalePulse, 1f + scalePulse));
            if (t >= 1f) CancelMotion();
        }

        public void SetModelRotation(float degrees)
        {
            if (Mathf.Approximately(_modelYaw, degrees)) return;
            _modelYaw = degrees;
            SetFacing(_facing);
        }

        public void SetFacing(PieceFacing facing)
        {
            _facing = facing;
            bool back = facing == PieceFacing.Back && _backIdle != null;
            _idleSprite = back ? _backIdle : _frontIdle;
            _attackSprite = back ? _backAttack : _frontAttack;
            _hitSprite = back ? _backHit : _frontHit;
            if (_modelRoot != null)
            {
                _modelRestRotation = BoardView.SurfaceOrientation *
                    Quaternion.Euler(0f, _modelYaw + (facing == PieceFacing.Back ? 180f : 0f), 0f);
                _modelRoot.localRotation = _modelRestRotation;
                if (!_motionActive && !_hopActive) CacheModelRestPose();
            }
            if (spriteRenderer == null || !spriteRenderer.enabled) return;
            spriteRenderer.sprite = _pose == ComicPose.Attack ? (_attackSprite ?? _idleSprite) :
                _pose == ComicPose.Hit ? (_hitSprite ?? _idleSprite) : _idleSprite;
        }

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Configure this piece's appearance from the theme.
        //   isPlayer + skinId: pass the equipped skin for the player's own piece
        //   so its tint (and optional sprite override) follow the active skin.
        public void InvalidateAppearance() => _aValid = false;

        public void Apply(GameTheme theme, PieceType type, bool isPlayer = false, string skinId = null, bool boss = false)
        {
            // Nothing that affects the look changed since the last Apply, so skip the
            // sprite/glyph rework (and, for traps, the per-frame ForceMeshUpdate).
            if (_aValid && _aTheme == theme && _aType == type && _aIsPlayer == isPlayer && _aSkinId == skinId && _aBoss == boss && _aCeramic == theme.ceramicPiecePrototype && _aPorcelain == theme.inkPorcelainPieces)
                return;
            _aTheme = theme; _aType = type; _aIsPlayer = isPlayer; _aSkinId = skinId; _aBoss = boss; _aValid = true;
            _aCeramic = theme.ceramicPiecePrototype;
            _aPorcelain = theme.inkPorcelainPieces;

            var v = theme.GetPiece(type, boss);
            Sprite sprite = v.sprite;
            Color tint = v.paintedColors ? Color.white : v.tint;
            _attackSprite = v.attackSprite;
            _hitSprite = v.hitSprite;
            _backIdle = v.backSprite;
            _backAttack = v.backAttackSprite;
            _backHit = v.backHitSprite;
            bool palette = v.comicPalette;
            bool painted = v.paintedColors;
            bool forceSprite = false;

            if (isPlayer)
            {
                // The player keeps its skin colour for whatever form it's wearing.
                tint = painted && (string.IsNullOrEmpty(skinId) || skinId == "gold")
                    ? Color.white : theme.GetSkinTint(skinId ?? "gold");
                if (theme.TryGetSkin(skinId, out var skin) && skin.overrideSprite != null)
                {
                    forceSprite = true;
                    sprite = skin.overrideSprite;
                    _attackSprite = _hitSprite = null;
                    _backIdle = _backAttack = _backHit = null;
                    palette = false;
                    painted = false;
                    tint = theme.GetSkinTint(skinId ?? "gold");
                }
            }
            _idleSprite = sprite;
            _frontIdle = sprite;
            _frontAttack = _attackSprite;
            _frontHit = _hitSprite;
            _poseUntil = 0f;
            _pose = ComicPose.Idle;

            if (_pixelKnight != null) _pixelKnight.SetActive(false);
            if (isPlayer && theme.comicStyle && PlayerPrefs.GetInt("kg_pixel_knight_preview", 0) != 0 &&
                (type == PieceType.Knight || type == PieceType.Nightrider))
            {
                if (_pixelKnight == null)
                {
                    var prefab = Resources.Load<GameObject>("PixelKnightGameplay");
                    if (prefab != null)
                    {
                        _pixelKnight = Instantiate(prefab,transform,false);
                        _pixelSlash = _pixelKnight.GetComponent<PixelKnightSlash>();
                    }
                }
                if (_pixelKnight != null)
                {
                    _pixelKnight.SetActive(true);
                    if (_modelRoot != null) _modelRoot.gameObject.SetActive(false);
                    spriteRenderer.enabled = false;
                    if (glyphLabel != null) glyphLabel.enabled = false;
                    if (backing != null) backing.enabled = false;
                    return;
                }
            }

            // Prefer modeled pieces and compact modeled hazard plaques.
            if (theme.comicStyle && !forceSprite && ConfigureModel(type, boss, theme.ceramicPiecePrototype, theme.inkPorcelainPieces))
            {
                var properties = _accentProperties ?? (_accentProperties = new MaterialPropertyBlock());
                bool skinAccent = isPlayer && !string.IsNullOrEmpty(skinId) && skinId != "gold";
                properties.SetFloat("_AccentStrength", skinAccent ? 1f : 0f);
                var accent = skinAccent ? tint : Color.white;
                properties.SetColor("_AccentColor", accent);
                foreach (var renderer in _modelRenderers) renderer.SetPropertyBlock(properties);
                spriteRenderer.enabled = false;
                if (glyphLabel != null) glyphLabel.enabled = false;
                if (backing != null) backing.enabled = false;
                return;
            }
            if (_modelRoot != null) _modelRoot.gameObject.SetActive(false);
            _modelIsCeramic = false;
            _modelIsPorcelain = false;

            // Translucent backing square behind the piece. Enemies get a uniform faint
            // cell (NOT their own colour, so the board stays readable); traps are red.
            // The PLAYER gets NO square. Its "shine" is the radial glow alone.
            if (backing != null)
            {
                if (isPlayer || theme.comicStyle)
                {
                    backing.enabled = false;
                }
                else
                {
                    backing.enabled = true;
                    if (theme.pieceBackingSprite != null) backing.sprite = theme.pieceBackingSprite;
                    Color bc = type == PieceType.Trap
                        ? new Color(0.784f, 0.196f, 0.196f, 0.2f)   // muted red cell (web is .35, but our X reads heavier)
                        : new Color(0.314f, 0.314f, 0.706f, 0.35f);  // web rgba(80,80,180,.35)
                    backing.color = bc;
                }
            }

            if (sprite != null)
            {
                // Art path: render the sprite, hide the glyph.
                spriteRenderer.enabled = true;
                if (_defaultMaterial == null) _defaultMaterial = spriteRenderer.sharedMaterial;
                spriteRenderer.sharedMaterial = painted && ComicArt.PaintedMaterial != null ? ComicArt.PaintedMaterial :
                    palette && ComicArt.Material != null ? ComicArt.Material : _defaultMaterial;
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = tint;
                SetFacing(_facing);
                if (glyphLabel != null) glyphLabel.enabled = false;
            }
            else
            {
                // Fallback path: render the chess glyph (the current look).
                spriteRenderer.enabled = false;
                if (glyphLabel != null)
                {
                    glyphLabel.enabled = true;
                    glyphLabel.color = tint;
                    var rt = glyphLabel.rectTransform;
                    rt.localPosition = Vector3.zero;

                    if (type == PieceType.Trap)
                    {
                        // Trap marker: the "X" from the Thaleah pixel font (matches the
                        // UI), a bit smaller so a field of traps isn't overwhelming. The
                        // pixel X reads much heavier than the web's thin ✕, so drop its
                        // opacity so a field of traps stays translucent like the web.
                        glyphLabel.color = new Color(tint.r, tint.g, tint.b, 1f);
                        glyphLabel.text = "X";
                        glyphLabel.font = theme.comicStyle ?
                            (theme.uiFont != null ? theme.uiFont : theme.glyphFont) :
                            (UIFactory.PixelFont() ?? theme.glyphFont);
                        float s = 0.6f;
                        rt.localScale = new Vector3(s, s, 1f);
                        // Center the glyph's ink in the cell regardless of the font's
                        // ascent/descent (Thaleah caps aren't centered by TMP alone).
                        glyphLabel.ForceMeshUpdate();
                        var c = glyphLabel.textBounds.center;
                        rt.localPosition = new Vector3(-c.x * s, -c.y * s, 0f);
                    }
                    else
                    {
                        // Chess pieces: the pixel chess font, ~65% so they sit small in
                        // the cell (matches the web look) with generous margins.
                        glyphLabel.text = GameConfig.Glyph(type);
                        if (theme.glyphFont != null) glyphLabel.font = theme.glyphFont;
                        rt.localScale = new Vector3(0.65f, 0.65f, 1f);
                    }
                }
            }
        }

        private bool ConfigureModel(PieceType type, bool boss, bool ceramicPrototype, bool porcelain)
        {
            string modelName = type == PieceType.Nightrider ? "knight" : type.ToString().ToLowerInvariant();
            _modelIsPorcelain = porcelain && type != PieceType.Trap;
            _modelIsCeramic = !_modelIsPorcelain && ceramicPrototype && (type == PieceType.Knight || type == PieceType.Nightrider ||
                type == PieceType.Rook || type == PieceType.Bishop);
            string key = type == PieceType.Trap ? "BoardModels/HazardPlaque" :
                (_modelIsPorcelain ? "Pieces/InkPorcelain/" : _modelIsCeramic ? "Pieces/CeramicPrototype/" : "Pieces/Optimized/") + modelName;
            if (_modelRoot != null && _modelKey == key)
            {
                _modelRoot.gameObject.SetActive(true);
                SetFacing(_facing);
                return true;
            }

            if (_models.TryGetValue(key, out var cached))
            {
                CancelMotion();
                if (_modelRoot != null) _modelRoot.gameObject.SetActive(false);
                _modelRoot = cached.root;
                _modelRenderers = cached.renderers;
                _modelKey = key;
                _modelRoot.gameObject.SetActive(true);
                SetFacing(_facing);
                CacheModelRestPose();
                return true;
            }
            var modelAsset = Resources.Load<GameObject>(key);
            if (modelAsset == null) return false;

            CancelMotion();
            if (_modelRoot != null) _modelRoot.gameObject.SetActive(false);
            // Keep the model import rotation (Blender's up axis differs from Unity's).
            // Facing and animation rotate a separate pivot, never the imported root.
            _modelRoot = new GameObject("3D_" + modelName).transform;
            _modelRoot.SetParent(transform, false);
            var instance = Instantiate(modelAsset, _modelRoot, false);
            _modelKey = key;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            _modelRenderers = renderers;
            if (renderers.Length == 0)
            {
                Destroy(_modelRoot.gameObject);
                _modelRoot = null;
                _modelKey = null;
                return false;
            }

            var combined = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var renderer in renderers)
            {
                combined.Encapsulate(renderer.bounds);
                renderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
                renderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
                if (renderer is MeshRenderer meshRenderer) MakeMaterialsUsable(meshRenderer, modelName, _modelIsCeramic, _modelIsPorcelain);
            }
            Vector3 localCenter = _modelRoot.InverseTransformPoint(combined.center);
            Vector3 localBottom = _modelRoot.InverseTransformPoint(new Vector3(combined.center.x, combined.min.y, combined.center.z));
            // Size by the circular pedestal footprint, not the piece's height.
            // Normalize in local units so changing the Inspector scale cannot
            // change a newly instantiated model's pedestal size relative to cached ones.
            var parentScale = _modelRoot.lossyScale;
            float diameter = Mathf.Max(combined.size.x / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
                combined.size.z / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
            var metrics = Metrics(modelName);
            if (_modelIsCeramic || _modelIsPorcelain)
            {
                instance.transform.localPosition -= new Vector3(localCenter.x, localBottom.y, localCenter.z);
                _modelRoot.localScale = Vector3.one * (.68f / Mathf.Max(.0001f, diameter));
            }
            else if (type != PieceType.Trap && metrics != null && metrics.height > 0f && metrics.diameter > 0f)
            {
                // Metrics are in the imported instance's parent space. Center
                // the actual pedestal, not the bounding box of weapons/robes.
                instance.transform.localPosition -= new Vector3(metrics.centerX, metrics.bottom, metrics.centerZ);
                float height = ReferenceHeight(type);
                float widthScale = height * .45f / metrics.diameter;
                _modelRoot.localScale = new Vector3(widthScale, height / metrics.height, widthScale);
            }
            else
            {
                instance.transform.localPosition -= new Vector3(localCenter.x, localBottom.y, localCenter.z);
                float scale = diameter > .0001f ? (type == PieceType.Trap ? .64f : .74f) / diameter : 1f;
                _modelRoot.localScale = new Vector3(scale, scale, scale);
            }
            SetFacing(_facing);
            CacheModelRestPose();
            _models[key] = new CachedModel { root = _modelRoot, renderers = _modelRenderers };
            return true;
        }

        private static ModelMetrics Metrics(string name)
        {
            if (_metrics == null)
            {
                var data = Resources.Load<TextAsset>("Pieces/model-metrics");
                _metrics = data != null ? JsonUtility.FromJson<ModelMetricsFile>(data.text) : new ModelMetricsFile();
            }
            if (_metrics.items != null)
                foreach (var item in _metrics.items) if (item.name == name) return item;
            return null;
        }

        private static void MakeMaterialsUsable(MeshRenderer renderer, string modelName, bool ceramic, bool porcelain)
        {
            // The vertex shader rolls incoming meshes outside their original bounds.
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 60f);
            var sourceMaterials = renderer.sharedMaterials;
            var replacements = new Material[sourceMaterials.Length];
            var unlit = (porcelain ? Resources.Load<Shader>("Pieces/InkPorcelain/InkPorcelain") : ceramic ? Resources.Load<Shader>("Pieces/CeramicPrototype/MatteCeramic") : null) ?? Resources.Load<Shader>("Pieces/ChessPiece") ??
                Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                var source = sourceMaterials[i];
                if (source == null || unlit == null) { replacements[i] = source; continue; }
                var sourceId = source.GetEntityId();
                // Play-mode teardown can destroy cached materials when domain reload is disabled.
                // Also rebuild when the presentation shader changes between art modes.
                if (!ModelMaterials.TryGetValue(sourceId, out var replacement) || replacement == null || replacement.shader != unlit)
                {
                    replacement = new Material(unlit) { name = source.name + " (Piece Unlit)" };
                    replacement.color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
                    if (replacement.HasProperty("_PieceClip")) replacement.SetFloat("_PieceClip", 1f);
                    var texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
                    // Unity's .blend-to-FBX import omits packed Blender images.
                    // The bridge extraction preserves the supplied painted palette.
                    if (texture == null && !ceramic && !porcelain)
                    {
                        texture = Resources.Load<Texture2D>("Pieces/Textures/" + modelName + "-basecolor");
                        if (texture != null) replacement.SetColor("_BaseColor", Color.white);
                    }
                    if (texture != null)
                    {
                        if (replacement.HasProperty("_BaseMap")) replacement.SetTexture("_BaseMap", texture);
                        else if (replacement.HasProperty("_MainTex")) replacement.SetTexture("_MainTex", texture);
                    }
                    if (porcelain)
                    {
                        bool paint = source.name.Contains("Paint");
                        bool gold = source.name.Contains("Gold");
                        bool ink = source.name.Contains("InkPorcelain_Ink");
                        replacement.SetFloat("_SkinRegion", paint ? 1f : 0f);
                        replacement.SetFloat("_Surface", gold ? 1f : ink ? .08f : .55f);
                        Color palette = gold ? new Color32(0xD4,0xA2,0x4F,255) :
                            ink ? new Color32(0x1B,0x26,0x30,255) :
                            source.name.Contains("PaintBlue") ? new Color32(0x24,0x48,0x8B,255) :
                            source.name.Contains("PaintTeal") ? new Color32(0x43,0x8F,0x87,255) :
                            source.name.Contains("PaintClay") ? new Color32(0xAB,0x63,0x43,255) :
                            source.name.Contains("PaintJade") ? new Color32(0x45,0x81,0x68,255) :
                            source.name.Contains("PaintPlum") ? new Color32(0x76,0x3B,0x61,255) :
                            new Color32(0xEE,0xE1,0xC4,255);
                        replacement.SetColor("_BaseColor", palette);
                    }
                    else if (ceramic)
                    {
                        replacement.SetFloat("_SkinRegion", source.name.Contains("UnglazedFoot") || source.name.Contains("Eye") ? 0 : 1);
                        Color clay = source.name.Contains("UnglazedFoot") ? new Color32(0xD8,0xCD,0xB3,255) :
                            source.name.Contains("Eye") ? new Color32(0x26,0x34,0x48,255) :
                            modelName == "knight" ? new Color32(0x50,0x79,0xB6,255) :
                            modelName == "rook" ? new Color32(0xB8,0x70,0x55,255) : new Color32(0x60,0xA4,0x9F,255);
                        replacement.SetColor("_BaseColor", clay);
                    }
                    ModelMaterials[sourceId] = replacement;
                }
                replacements[i] = replacement;
            }
            renderer.sharedMaterials = replacements;
        }

    }
}
