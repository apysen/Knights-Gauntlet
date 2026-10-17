using System.Collections.Generic;
using UnityEngine;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    public partial class GameView
    {
        private readonly List<ComicEffect> _comicEffects = new List<ComicEffect>();
        private readonly List<JumpTrail> _jumpTrails = new List<JumpTrail>();
        private Material _trailMaterial;
        private static Material _comicOverlayMaterial;
        private static Material _captureSlashMaterial;
        private static readonly int SlashProgressId = Shader.PropertyToID("_Progress");
        private static readonly int SlashSeedId = Shader.PropertyToID("_Seed");
        private static readonly int SlashVariantId = Shader.PropertyToID("_Variant");
        private static readonly int SlashComboId = Shader.PropertyToID("_Combo");
        private static readonly int SlashReducedId = Shader.PropertyToID("_ReducedEffects");
        private static Texture2D _slashFlowNoise;
        [Header("Capture slash")]
        [SerializeField] private Color slashWakeColor = new Color(1f, 0.65f, 0.16f);
        [SerializeField] private Color slashEdgeColor = new Color(1f, 0.99f, 0.88f);
        [SerializeField, Range(0f, 3f)] private float slashIntensity = 1.2f;
        [SerializeField, Range(0f, 4f)] private float slashNoiseSpeed = 1.35f;
        [SerializeField, Range(0f, 1f)] private float slashErosion = 0.55f;
        private static readonly string[] CaptureSlashSprites =
            { "comic-slash-crescent", "comic-slash-cut", "comic-slash-sweep" };
        private int _lastCaptureSlash = -1;
        private float _comicDeathUntil;
        private Vector2 _playerAttackDirection = Vector2.up;
        private PieceFacing _playerFacing = PieceFacing.Front;

        private sealed class ComicEffect
        {
            public SpriteRenderer renderer;
            public int col;
            public float row, age, life, size, spriteUnits;
            public float angle, rise;
            public bool slash;
            public bool echo, spark;
            public Vector2 offset, velocity;
            public Color color;
            public MaterialPropertyBlock shaderProperties;
        }

        private sealed class JumpTrail
        {
            public LineRenderer ribbon;
            public int fromCol, toCol;
            public float fromRow, toRow, age;
            public bool knight, active;
        }

        public void ResetComicPresentation()
        {
            _comicDeathUntil = 0f;
            _playerFacing = PieceFacing.Front;
            _playerAttackDirection = Vector2.up;
            _lastCaptureSlash = -1;
            _enemyMotions.Clear();
            CancelTransientAnimations();
        }

        // Presentation never owns input or simulation timing. A fresh successful
        // move clears leftover motion and feedback before starting its own effects.
        public void CancelTransientAnimations()
        {
            _comicDeathUntil = 0f;
            foreach (var piece in _piecePool) piece.ClearPose();
            _player?.ClearPose();
            // Unity's destroyed objects compare equal to null, but ?. only checks
            // the managed reference. Completed effects must not block the next move.
            if (_activeMoveVfx != null) _activeMoveVfx.Cancel();
            _activeMoveVfx = null;
            foreach (var effect in _comicEffects) effect.renderer.gameObject.SetActive(false);
            foreach (var trail in _jumpTrails)
            {
                trail.active = false;
                trail.ribbon.gameObject.SetActive(false);
            }
            foreach (var popup in _popups) { popup.active = false; popup.tmp.gameObject.SetActive(false); }
            foreach (var particle in _particles) { particle.active = false; particle.tr.gameObject.SetActive(false); }
            foreach (var voxel in _voxelParticles) { voxel.active = false; voxel.tr.gameObject.SetActive(false); }
        }

        public void PlayComicMove(int fromCol, float fromY, PieceType form, GameSimulation sim)
        {
            Vector3 visualStart = _player.VisualPosition;
            CancelTransientAnimations();
            Vector3 delta = _board.CellToWorld(sim.PlayerCol, sim.PlayerY) - _board.CellToWorld(fromCol, fromY);
            if (delta.sqrMagnitude > 0.001f)
            {
                _playerAttackDirection = new Vector2(delta.x, delta.y).normalized;
            }
            if (!_theme.comicStyle) return;
            _playerFacing = PieceFacing.Front;
            _player.SetFacing(_playerFacing);
            if (SaveData.IsReducedEffects()) return;
            _player.PlayHop((visualStart - _board.CellToWorld(sim.PlayerCol, sim.PlayerY)) /
                Mathf.Max(0.0001f, _player.transform.lossyScale.x));
            JumpTrail trail = null;
            foreach (var item in _jumpTrails) if (!item.active) { trail = item; break; }
            if (trail == null && _jumpTrails.Count < 8)
            {
                trail = new JumpTrail { ribbon = NewTrailLine("HopRibbon", 6) };
                _jumpTrails.Add(trail);
            }
            // Rapid tapping replaces the oldest visual instead of delaying input.
            if (trail == null) trail = _jumpTrails[0];
            trail.fromCol = fromCol; trail.fromRow = fromY - sim.ScrollCells;
            trail.toCol = sim.PlayerCol; trail.toRow = sim.PlayerY - sim.ScrollCells;
            trail.knight = form == PieceType.Knight || form == PieceType.Nightrider;
            trail.age = 0f; trail.active = true;
            trail.ribbon.gameObject.SetActive(true);
        }

        public void PlayComicCapture(GameSimulation sim, int comboMultiplier = 1)
        {
            if (_theme == null) return;
            // Capture feedback belongs to the landing square, never the source square.
            int col = sim.PlayerCol;
            float y = sim.PlayerY;
            PieceType form = sim.PlayerForm;
            bool reduced = SaveData.IsReducedEffects();
            float comboStrength = Mathf.InverseLerp(1f, 6f, Mathf.Max(1, comboMultiplier));
            // Hold the contact pose briefly for impact. This is a local model pose;
            // simulation and board scrolling continue normally underneath it.
            _player.PlayAttackMotion(form, _playerAttackDirection, reduced ? 0.16f : 0.32f);
            Color accent = UIFactory.Gold;
            float impactSize = (form == PieceType.Queen ? 1.12f : 0.9f) * (1f + comboStrength * 0.24f);
            SpawnComicEffect("comic-impact", col, y, sim.ScrollCells,
                reduced ? 0.10f : 0.20f, impactSize, false, accent);

            // The pixel knight owns its trail instead of stacking the old slash.
            if (_player.UsesPixelKnight) return;

            // Keep the cut on the landing piece, 33% larger than the previous
            // footprint, with only a small combo-size lift. Combo mostly adds ink
            // brightness and duration so the mark stays local around the capture.
            // All variants import to a one-unit square, keeping their scale consistent.
            float slashSize = (0.97f + comboStrength * 0.03f) * 1.33f;
            Color slashAccent = Color.Lerp(new Color(1f, 0.97f, 0.88f), Color.white, comboStrength);
            int slashIndex = Random.Range(0, CaptureSlashSprites.Length - (_lastCaptureSlash >= 0 ? 1 : 0));
            if (_lastCaptureSlash >= 0 && slashIndex >= _lastCaptureSlash) slashIndex++;
            _lastCaptureSlash = slashIndex;
            float slashAngle = Random.Range(0f, 360f);
            var cut = SpawnComicEffect(CaptureSlashSprites[slashIndex], col, y, sim.ScrollCells,
                reduced ? 0.14f : 0.24f + comboStrength * 0.06f,
                slashSize, true, slashAccent, slashAngle,
                strength: comboStrength, variant: slashIndex);
            if (!reduced)
            {
                var echo = SpawnComicEffect(CaptureSlashSprites[slashIndex], col, y, sim.ScrollCells,
                    0.20f + comboStrength * 0.04f, slashSize * 0.94f, true,
                    new Color(1f, 0.72f, 0.3f, 0.22f), slashAngle - 6f,
                    strength: comboStrength, variant: slashIndex);
                echo.echo = true;
                echo.age = -0.028f;
                echo.offset = -_playerAttackDirection * 0.045f;
                echo.renderer.enabled = false;
                echo.renderer.flipY = cut.renderer.flipY;
                echo.renderer.sortingOrder = 199;
                echo.shaderProperties.SetFloat(SlashSeedId, cut.shaderProperties.GetFloat(SlashSeedId));
                echo.renderer.SetPropertyBlock(echo.shaderProperties);

                int sparks = 3 + Mathf.RoundToInt(comboStrength * 3f);
                for (int i = 0; i < sparks; i++)
                {
                    float angle = Mathf.Atan2(_playerAttackDirection.y, _playerAttackDirection.x)
                        + Random.Range(-1.2f, 1.2f) + (i % 2 == 0 ? 0f : Mathf.PI);
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var spark = SpawnComicEffect(null, col, y, sim.ScrollCells,
                        Random.Range(0.12f, 0.19f), Random.Range(0.08f, 0.14f), false,
                        Color.Lerp(slashWakeColor, slashEdgeColor, Random.value), angle * Mathf.Rad2Deg);
                    spark.velocity = direction * Random.Range(1.5f, 2.7f);
                    spark.offset = direction * 0.05f;
                }
            }
        }


        public void PlayComicDefeat(GameSimulation sim)
        {
            if (!_theme.comicStyle) return;
            _comicDeathUntil = Time.unscaledTime + 0.18f;
            _player.PlayHitMotion(0.18f);
            SpawnComicEffect("comic-impact", sim.PlayerCol, sim.PlayerY, sim.ScrollCells, 0.18f, 0.95f, false, UIFactory.Error);
        }

        private void UpdateComicEnemy(PieceView piece, int col, float y, PieceType type, GameSimulation sim)
        {
            piece.RefreshPose();
            if (!_theme.comicStyle || type == PieceType.Trap) return;
            piece.SetFacing(PieceFacing.Front);
            if (_comicDeathUntil > Time.unscaledTime && col == sim.PlayerCol && Mathf.Abs(y - sim.PlayerY) < 0.6f)
                piece.ShowPose(ComicPose.Attack, _comicDeathUntil - Time.unscaledTime);
            else if (sim.Alive && sim.Mode == GameMode.Battle && !sim.Tutorial && !SaveData.IsReducedEffects())
            {
                float tell = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, sim.EnemyMoveProgress));
                piece.transform.localScale = Vector3.Scale(piece.transform.localScale, new Vector3(1f + tell * 0.04f, 1f - tell * 0.035f, 1f));
            }
        }

        private LineRenderer NewTrailLine(string name, int order)
        {
            if (_trailMaterial == null) _trailMaterial = new Material(Shader.Find("Sprites/Default"));
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _trailMaterial;
            line.useWorldSpace = true;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.45f, 1f), new Keyframe(1f, 0f));
            line.widthMultiplier = 0.035f;
            line.sortingOrder = order;
            return line;
        }

        private ComicEffect SpawnComicEffect(string name, int col, float y, float scroll, float life, float size,
            bool slash, Color color, float angle = 0f, float rise = 0f, float strength = 0f, int variant = 0)
        {
            ComicEffect effect = null;
            foreach (var item in _comicEffects) if (!item.renderer.gameObject.activeSelf) { effect = item; break; }
            if (effect == null && _comicEffects.Count < 16)
            {
                var go = new GameObject("ComicImpact");
                go.transform.SetParent(transform, false);
                effect = new ComicEffect { renderer = go.AddComponent<SpriteRenderer>() };
                _comicEffects.Add(effect);
            }
            if (effect == null) effect = _comicEffects[0];
            if (_comicOverlayMaterial == null)
            {
                var shader = Resources.Load<Shader>("ComicEffectOverlay") ?? Shader.Find("KnightsGauntlet/ComicEffectOverlay");
                if (shader != null)
                {
                    _comicOverlayMaterial = new Material(shader) { name = "Comic Combat Overlay" };
                    _comicOverlayMaterial.renderQueue = 3100;
                }
            }
            if (slash && _captureSlashMaterial == null)
            {
                var shader = Resources.Load<Shader>("CaptureSlash") ?? Shader.Find("KnightsGauntlet/CaptureSlash");
                if (shader != null)
                    _captureSlashMaterial = new Material(shader) { name = "Animated Capture Slash", renderQueue = 3100 };
            }
            effect.renderer.sharedMaterial = slash && _captureSlashMaterial != null
                ? _captureSlashMaterial : _comicOverlayMaterial != null ? _comicOverlayMaterial : ComicArt.Material;
            if (slash)
            {
                if (effect.shaderProperties == null) effect.shaderProperties = new MaterialPropertyBlock();
                effect.shaderProperties.Clear();
                effect.shaderProperties.SetFloat(SlashProgressId, 0f);
                effect.shaderProperties.SetFloat(SlashSeedId, Random.Range(0f, 1000f));
                effect.shaderProperties.SetFloat(SlashVariantId, variant);
                effect.shaderProperties.SetFloat(SlashComboId, strength);
                effect.shaderProperties.SetFloat(SlashReducedId, SaveData.IsReducedEffects() ? 1f : 0f);
                effect.shaderProperties.SetColor("_WakeColor", slashWakeColor);
                effect.shaderProperties.SetColor("_EdgeColor", slashEdgeColor);
                effect.shaderProperties.SetFloat("_Intensity", slashIntensity);
                effect.shaderProperties.SetFloat("_NoiseSpeed", slashNoiseSpeed);
                effect.shaderProperties.SetFloat("_Erosion", slashErosion);
                if (_slashFlowNoise == null) _slashFlowNoise = Resources.Load<Texture2D>("SlashFlowNoise");
                if (_slashFlowNoise != null) effect.shaderProperties.SetTexture("_FlowTex", _slashFlowNoise);
                effect.renderer.SetPropertyBlock(effect.shaderProperties);
            }
            else effect.renderer.SetPropertyBlock(null);
            // Combat cuts must sit above board pieces, voxels, and score popups.
            effect.spark = name == null;
            effect.echo = false;
            effect.offset = effect.velocity = Vector2.zero;
            effect.renderer.enabled = true;
            effect.renderer.sortingOrder = effect.spark ? 201 : slash ? 200 : 24;
            effect.renderer.sprite = effect.spark ? BoardView.UnitSquare() : ComicArt.Load(name);
            effect.spriteUnits = slash && effect.renderer.sprite != null
                ? Mathf.Max(0.001f, Mathf.Max(effect.renderer.sprite.bounds.size.x, effect.renderer.sprite.bounds.size.y))
                : 1f;
            effect.renderer.flipX = false;
            effect.renderer.flipY = slash && Random.value < 0.5f;
            effect.col = col; effect.row = y - scroll;
            effect.age = 0f; effect.life = life; effect.size = size;
            effect.slash = slash; effect.color = color; effect.angle = angle; effect.rise = rise;
            effect.renderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            effect.renderer.transform.position = _board.CellToWorld(col, y);
            effect.renderer.transform.localScale = Vector3.one *
                (_board.cellSize * size * (slash ? 1.02f : 0.55f) / effect.spriteUnits);
            effect.renderer.color = color;
            effect.renderer.gameObject.SetActive(true);
            if (effect.spark)
                effect.renderer.transform.localScale = new Vector3(size, size * 0.16f, 1f) * _board.cellSize;
            return effect;
        }

        private void AnimateComicEffects(GameSimulation sim, float dt)
        {
            foreach (var effect in _comicEffects)
            {
                if (!effect.renderer.gameObject.activeSelf) continue;
                effect.age += dt;
                bool reduced = SaveData.IsReducedEffects();
                if (reduced && (effect.echo || effect.spark))
                {
                    effect.renderer.gameObject.SetActive(false);
                    continue;
                }
                if (effect.age < 0f) continue;
                effect.renderer.enabled = true;
                float t = effect.age / effect.life;
                if (t >= 1f) { effect.renderer.gameObject.SetActive(false); continue; }
                Vector3 position = _board.CellToWorld(effect.col, effect.row + sim.ScrollCells);
                Vector2 offset = effect.offset + effect.velocity * (effect.life * (t - 0.35f * t * t));
                position += new Vector3(offset.x, offset.y, 0f) * _board.cellSize;
                position.y += effect.rise * (1f - Mathf.Pow(1f - t, 3f)) * _board.cellSize;
                effect.renderer.transform.position = position;
                if (effect.spark)
                {
                    float length = effect.size * Mathf.Lerp(1f, 0.3f, t) * _board.cellSize;
                    effect.renderer.transform.localScale = new Vector3(length, length * 0.16f, 1f);
                    var sparkColor = effect.color;
                    sparkColor.a *= 1f - Mathf.SmoothStep(0.25f, 1f, t);
                    effect.renderer.color = sparkColor;
                    continue;
                }
                float scale;
                scale = effect.size * (effect.slash
                    ? Mathf.Lerp(1.02f, 1.16f, 1f - Mathf.Pow(1f - Mathf.Min(1f, t / 0.28f), 3f))
                    : Mathf.Lerp(0.55f, 1.02f, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 2.4f))));
                if (SaveData.IsReducedEffects()) scale *= 0.78f;
                effect.renderer.transform.localScale = Vector3.one * (_board.cellSize * scale / effect.spriteUnits);
                if (effect.slash)
                {
                    effect.shaderProperties.SetFloat(SlashProgressId, t);
                    effect.shaderProperties.SetFloat(SlashReducedId, reduced ? 1f : 0f);
                    effect.renderer.SetPropertyBlock(effect.shaderProperties);
                    float turn = SaveData.IsReducedEffects() ? 0f : Mathf.Lerp(-9f, 4f, Mathf.SmoothStep(0f, 1f, t));
                    effect.renderer.transform.localRotation = Quaternion.Euler(0f, 0f, effect.angle + turn);
                }
                var color = effect.color;
                color.a *= reduced ? 0.72f : effect.slash ? 1f : 1f - Mathf.SmoothStep(0.62f, 1f, t);
                effect.renderer.color = color;
            }
            foreach (var trail in _jumpTrails)
            {
                if (!trail.active) continue;
                trail.age += dt;
                float t = trail.age / (PieceView.HopDuration + 0.025f);
                if (t >= 1f || SaveData.IsReducedEffects())
                {
                    trail.active = false;
                    trail.ribbon.gameObject.SetActive(false);
                    continue;
                }
                var from = _board.CellToWorld(trail.fromCol, trail.fromRow + sim.ScrollCells);
                var to = _board.CellToWorld(trail.toCol, trail.toRow + sim.ScrollCells);
                // A short traveling arc follows the hop, then tapers away. It never
                // draws an elbow across other cells or leaves a persistent route.
                float head = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.75f));
                float tail = Mathf.Max(0f, head - 0.48f * (1f - t));
                SetTrailPoints(trail.ribbon, from, to, tail, head, trail.knight ? 0.30f : 0.12f);
                trail.ribbon.widthMultiplier = _board.cellSize * 0.035f;
                var gold = new Color(0.91f, 0.73f, 0.32f, (1f - t) * 0.65f);
                trail.ribbon.startColor = new Color(gold.r, gold.g, gold.b, 0f);
                trail.ribbon.endColor = gold;
            }
        }

        private void SetTrailPoints(LineRenderer line, Vector3 from, Vector3 to, float tail, float head, float height)
        {
            const int count = 12;
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float progress = Mathf.Lerp(tail, head, i / (float)(count - 1));
                Vector3 position = Vector3.Lerp(from, to, progress);
                position.z -= (0.025f + Mathf.Sin(progress * Mathf.PI) * height) * _board.cellSize;
                line.SetPosition(i, position);
            }
        }

        private void OnDestroy()
        {
            DisposeBoardSpriteMaterial();
            if (_trailMaterial == null) return;
            if (Application.isPlaying) Destroy(_trailMaterial);
            else DestroyImmediate(_trailMaterial);
        }
    }
}
