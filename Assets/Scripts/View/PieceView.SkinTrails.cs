using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    // Skin trails: what each premium skin leaves behind along a hop.
    //   Phantom      fading ghost copies of the piece
    //   Nightfall    black shadow copies with red eyes, lingering a little longer
    //   Golden Gilded spinning shillings that pop out and bounce on the board
    //   Stained Glass jewel-coloured glass shards that scatter and glint
    //   Queensbane   red embers that drift up off the path
    //   Starforged   twinkling stardust, and now and then a shooting star
    // Everything lives under this piece in its local units (Z- is up off the board), so
    // it rides the scrolling board. When the piece jumps again, live bits are shifted
    // back by the jump so they stay where they were dropped. Pooled; no per-hop allocation
    // once warm. PlayHop is skipped entirely under Reduced Effects.
    public partial class PieceView
    {
        private const int MaxEchoes = 4;
        private Transform[] _echoes;
        private Renderer[][] _echoRenderers;
        private string _echoKey;
        private int _echoCount, _echoFx;
        private float _echoAge = float.MaxValue, _echoLife = 1f;
        private MaterialPropertyBlock _echoProperties;

        private enum TrailBitKind { Coin, Shard, Ember, Dust, Streak }
        private sealed class TrailBit
        {
            public TrailBitKind kind;
            public Transform tr;
            public Renderer renderer;
            public SpriteRenderer sprite;
            public bool active;
            public Vector3 vel, spinAxis;
            public float spin, life, maxLife, size, seed;
            public Color color;
        }
        private readonly List<TrailBit> _trailBits = new List<TrailBit>();
        private MaterialPropertyBlock _bitProperties;
        private static Mesh _shardMesh;
        private static Material _shardMaterial;
        private const float TrailGravity = 9f; // piece units/s², toward the board (+Z)

        private static readonly Color[] Jewels =
        {
            new Color(.95f, .16f, .24f), new Color(.2f, .45f, 1f), new Color(.12f, .85f, .45f),
            new Color(1f, .72f, .15f), new Color(.7f, .28f, 1f),
        };

        private void SpawnSkinTrail(Vector3 fromOffset)
        {
            if (_skinFx == SkinFx.None || fromOffset.sqrMagnitude < .0001f) return;
            ShiftTrail(fromOffset);
            switch (_skinFx)
            {
                case SkinFx.Phantom: SpawnEchoes(fromOffset, 3, .3f); break;
                case SkinFx.Nightfall: SpawnEchoes(fromOffset, 4, .42f); break;
                case SkinFx.Gilded: SpawnCoins(fromOffset); break;
                case SkinFx.StainedGlass: SpawnShards(fromOffset); break;
                case SkinFx.Queensbane: SpawnEmbers(fromOffset); break;
                case SkinFx.Starforged: SpawnStardust(fromOffset); break;
            }
        }

        // The piece just moved by -fromOffset in its own space; keep earlier bits in place.
        private void ShiftTrail(Vector3 fromOffset)
        {
            foreach (var bit in _trailBits) if (bit.active) bit.tr.localPosition += fromOffset;
            if (_echoes != null && _echoAge < _echoLife)
                foreach (var e in _echoes) if (e != null && e.gameObject.activeSelf) e.localPosition += fromOffset;
        }

        private void UpdateSkinTrail(float dt)
        {
            if (_echoAge < _echoLife) { _echoAge += dt; UpdateEchoes(); }
            if (_trailBits.Count > 0) UpdateTrailBits(dt);
        }

        private void HideSkinTrail()
        {
            HideEchoes();
            foreach (var bit in _trailBits) Deactivate(bit);
        }

        private void OnDestroy() => DestroyEchoes();

        // ── Afterimages (Phantom, Nightfall) ─────────────────────────────────

        private void SpawnEchoes(Vector3 fromOffset, int count, float life)
        {
            if (_echoes == null || _echoKey != _modelKey) BuildEchoes();
            if (_echoes == null) return;
            _echoCount = Mathf.Min(count, MaxEchoes);
            _echoLife = life;
            _echoFx = _skinFx;
            _echoAge = 0f;
            for (int i = 0; i < MaxEchoes; i++)
            {
                var echo = _echoes[i];
                bool on = i < _echoCount;
                echo.gameObject.SetActive(on);
                if (!on) continue;
                // Spaced from the start square toward the piece.
                echo.localPosition = _modelRestPosition + fromOffset * (1f - i / (float)_echoCount);
                echo.localRotation = _modelRestRotation;
                echo.localScale = _modelRestScale;
            }
            UpdateEchoes();
        }

        private void BuildEchoes()
        {
            DestroyEchoes();
            if (_modelRoot == null) return;
            _echoKey = _modelKey;
            _echoes = new Transform[MaxEchoes];
            _echoRenderers = new Renderer[MaxEchoes][];
            for (int i = 0; i < MaxEchoes; i++)
            {
                var copy = Instantiate(_modelRoot.gameObject, transform, false);
                copy.name = "SkinEcho" + i;
                _echoes[i] = copy.transform;
                _echoRenderers[i] = copy.GetComponentsInChildren<Renderer>(true);
                foreach (var r in _echoRenderers[i])
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    // The vertex shader rolls meshes outside their original bounds.
                    if (r is MeshRenderer) r.localBounds = new Bounds(Vector3.zero, Vector3.one * 60f);
                }
                copy.SetActive(false);
            }
        }

        private void UpdateEchoes()
        {
            if (_echoes == null) return;
            if (_echoAge >= _echoLife || _skinFx != _echoFx) { HideEchoes(); return; }
            var block = _echoProperties ?? (_echoProperties = new MaterialPropertyBlock());
            float life = 1f - _echoAge / _echoLife;
            for (int i = 0; i < _echoCount; i++)
            {
                // The copy nearest the start square is faintest.
                float along = _echoCount > 1 ? i / (float)(_echoCount - 1) : 1f;
                float fade = _echoFx == SkinFx.Nightfall
                    ? life * Mathf.Lerp(.35f, .8f, along)          // dense shadows that thin out
                    : life * life * Mathf.Lerp(.28f, .6f, along);  // airy ghosts
                block.Clear();
                block.SetFloat("_AccentStrength", 0f);
                block.SetFloat("_SkinFX", _echoFx);
                block.SetFloat("_SkinFade", fade);
                block.SetFloat("_SkinScale", _skinScaleSent);
                foreach (var r in _echoRenderers[i]) if (r != null) r.SetPropertyBlock(block);
            }
        }

        private void HideEchoes()
        {
            _echoAge = float.MaxValue;
            if (_echoes == null) return;
            foreach (var e in _echoes) if (e != null) e.gameObject.SetActive(false);
        }

        private void DestroyEchoes()
        {
            if (_echoes != null) foreach (var e in _echoes) if (e != null) Destroy(e.gameObject);
            _echoes = null;
            _echoRenderers = null;
            _echoKey = null;
        }

        // ── Physical bits (Gilded coins, Stained Glass shards, Queensbane embers) ──

        // Floor of the board in this piece's local space (the model's base).
        private float TrailFloor => _modelRoot != null ? _modelRestPosition.z : 0f;

        private void SpawnCoins(Vector3 fromOffset)
        {
            const int count = 5;
            for (int k = 0; k < count; k++)
            {
                var bit = GetTrailBit(TrailBitKind.Coin);
                if (bit == null) return;
                float t = (k + .5f) / count;
                Vector2 jitter = Random.insideUnitCircle * .08f;
                bit.tr.localPosition = fromOffset * t + new Vector3(jitter.x, jitter.y, TrailFloor - Random.Range(.15f, .32f));
                Vector2 kick = Random.insideUnitCircle * .9f;
                bit.vel = new Vector3(kick.x, kick.y, -Random.Range(1.8f, 2.5f));
                bit.tr.localRotation = Random.rotation;
                bit.spinAxis = Random.onUnitSphere;
                bit.spin = Random.Range(540f, 900f);
                bit.maxLife = bit.life = Random.Range(.5f, .65f);
                bit.size = Random.Range(.095f, .115f); // about 0.17-0.21 across
                Activate(bit);
            }
        }

        private void SpawnShards(Vector3 fromOffset)
        {
            const int count = 9;
            var block = _bitProperties ?? (_bitProperties = new MaterialPropertyBlock());
            for (int k = 0; k < count; k++)
            {
                var bit = GetTrailBit(TrailBitKind.Shard);
                if (bit == null) return;
                float t = (k + .5f) / count;
                Vector2 jitter = Random.insideUnitCircle * .1f;
                bit.tr.localPosition = fromOffset * t + new Vector3(jitter.x, jitter.y, TrailFloor - Random.Range(.12f, .4f));
                Vector2 kick = Random.insideUnitCircle.normalized * Random.Range(1.1f, 2.1f);
                bit.vel = new Vector3(kick.x, kick.y, -Random.Range(.9f, 1.7f));
                bit.tr.localRotation = Random.rotation;
                bit.spinAxis = Random.onUnitSphere;
                bit.spin = Random.Range(720f, 1260f);
                bit.maxLife = bit.life = Random.Range(.4f, .55f);
                bit.size = Random.Range(.15f, .22f);
                block.Clear();
                // Over-bright so the lit shard still reads as glowing glass.
                block.SetColor("_BaseColor", Jewels[Random.Range(0, Jewels.Length)] * 1.7f);
                bit.renderer.SetPropertyBlock(block);
                Activate(bit);
            }
        }

        private void SpawnEmbers(Vector3 fromOffset)
        {
            const int count = 14;
            for (int k = 0; k < count; k++)
            {
                var bit = GetTrailBit(TrailBitKind.Ember);
                if (bit == null) return;
                float t = Random.value;
                Vector2 jitter = Random.insideUnitCircle * .12f;
                bit.tr.localPosition = fromOffset * t + new Vector3(jitter.x, jitter.y, TrailFloor - Random.Range(.05f, .35f));
                // Rise off the board and drift up the screen.
                bit.vel = new Vector3(Random.Range(-.3f, .3f), Random.Range(.3f, .65f), -Random.Range(.5f, 1f));
                bit.tr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
                bit.spin = Random.Range(-240f, 240f);
                bit.maxLife = bit.life = Random.Range(.55f, .9f);
                bit.size = Random.Range(.06f, .1f);
                Activate(bit);
            }
        }

        private static readonly Color[] StarColors =
        {
            Color.white, new Color(.7f, .85f, 1f), new Color(.82f, .62f, 1f), new Color(1f, .75f, .92f),
        };

        private void SpawnStardust(Vector3 fromOffset)
        {
            const int count = 16;
            for (int k = 0; k < count; k++)
            {
                var bit = GetTrailBit(TrailBitKind.Dust);
                if (bit == null) return;
                Vector2 jitter = Random.insideUnitCircle * .14f;
                bit.tr.localPosition = fromOffset * Random.value + new Vector3(jitter.x, jitter.y, TrailFloor - Random.Range(.05f, .45f));
                Vector2 drift = Random.insideUnitCircle * .25f;
                bit.vel = new Vector3(drift.x, drift.y + .1f, -Random.Range(.05f, .3f));
                bit.tr.localRotation = Quaternion.Euler(0f, 0f, 45f); // diamond sparkle
                bit.maxLife = bit.life = Random.Range(.6f, 1f);
                bit.size = Random.Range(.035f, .065f);
                bit.seed = Random.Range(0f, 100f);
                bit.color = StarColors[Random.Range(0, StarColors.Length)];
                Activate(bit);
            }
            // Sometimes a shooting star streaks along the jump, past the piece.
            if (Random.value < .4f)
            {
                var star = GetTrailBit(TrailBitKind.Streak);
                if (star == null) return;
                Vector3 dir = -fromOffset.normalized;
                star.tr.localPosition = fromOffset * 1.25f + new Vector3(0f, 0f, TrailFloor - .55f);
                star.vel = dir * 7f;
                star.tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                star.maxLife = star.life = .32f;
                star.size = .05f;
                star.color = new Color(.88f, .92f, 1f);
                Activate(star);
            }
        }

        private void UpdateTrailBits(float dt)
        {
            float floor = TrailFloor;
            foreach (var bit in _trailBits)
            {
                if (!bit.active) continue;
                bit.life -= dt;
                if (bit.life <= 0f) { Deactivate(bit); continue; }
                float k = bit.life / bit.maxLife; // 1 -> 0
                var p = bit.tr.localPosition;
                if (bit.kind == TrailBitKind.Dust)
                {
                    bit.vel *= 1f - 1.2f * dt;
                    bit.tr.localPosition = p + bit.vel * dt;
                    var c = bit.color;
                    float twinkle = .55f + .45f * Mathf.Sin((bit.maxLife - bit.life) * 26f + bit.seed);
                    c.a = Mathf.Clamp01(k / .35f) * twinkle;
                    bit.sprite.color = c;
                    float s = bit.size * (.7f + .3f * twinkle);
                    bit.tr.localScale = new Vector3(s, s, 1f);
                    continue;
                }
                if (bit.kind == TrailBitKind.Streak)
                {
                    bit.tr.localPosition = p + bit.vel * dt;
                    var c = bit.color; c.a = Mathf.Clamp01(k / .5f);
                    bit.sprite.color = c;
                    // A long thin streak that shortens as it burns out.
                    bit.tr.localScale = new Vector3(bit.size * Mathf.Lerp(3f, 10f, k), bit.size * .5f, 1f);
                    continue;
                }
                if (bit.kind == TrailBitKind.Ember)
                {
                    bit.vel *= 1f - 1.5f * dt; // drag
                    p += bit.vel * dt;
                    p.x += Mathf.Sin((bit.maxLife - bit.life) * 11f + bit.spin) * .12f * dt; // sway
                    bit.tr.localPosition = p;
                    bit.tr.localRotation = Quaternion.Euler(0f, 0f, bit.spin * dt) * bit.tr.localRotation;
                    // White-hot to deep red, then out.
                    float age = 1f - k;
                    var c = age < .4f
                        ? Color.Lerp(new Color(1f, .9f, .5f), new Color(1f, .35f, .1f), age / .4f)
                        : Color.Lerp(new Color(1f, .35f, .1f), new Color(.7f, .05f, .03f), (age - .4f) / .6f);
                    c.a = Mathf.Clamp01(k / .3f);
                    bit.sprite.color = c;
                    float s = bit.size * Mathf.Lerp(.4f, 1f, k);
                    bit.tr.localScale = new Vector3(s, s, 1f);
                    continue;
                }
                bit.vel.z += TrailGravity * dt;
                p += bit.vel * dt;
                if (p.z > floor && bit.vel.z > 0f)
                {
                    // Land on the board with a small bounce.
                    p.z = floor;
                    bit.vel.z *= -.35f;
                    bit.vel.x *= .55f; bit.vel.y *= .55f;
                    bit.spin *= .5f;
                }
                bit.tr.localPosition = p;
                bit.tr.localRotation = Quaternion.AngleAxis(bit.spin * dt, bit.spinAxis) * bit.tr.localRotation;
                float scale = bit.size * Mathf.Clamp01(k / .3f); // shrink away at the end
                bit.tr.localScale = new Vector3(scale, scale, scale);
            }
        }

        private TrailBit GetTrailBit(TrailBitKind kind)
        {
            foreach (var bit in _trailBits) if (!bit.active && bit.kind == kind) return bit;
            if (_trailBits.Count >= 64) return null;
            var item = new TrailBit { kind = kind };
            int layer = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
            int order = spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
            if (kind == TrailBitKind.Ember || kind == TrailBitKind.Dust || kind == TrailBitKind.Streak)
            {
                var go = new GameObject("Skin" + kind);
                go.transform.SetParent(transform, false);
                item.sprite = go.AddComponent<SpriteRenderer>();
                item.sprite.sprite = BoardView.UnitSquare();
                item.sprite.sortingLayerID = layer;
                item.sprite.sortingOrder = order + 1;
                item.renderer = item.sprite;
                item.tr = go.transform;
            }
            else
            {
                MeshRenderer renderer;
                if (kind == TrailBitKind.Coin) renderer = CoinMesh.Create("SkinCoin", transform, true);
                else
                {
                    var go = new GameObject("SkinShard", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = ShardMesh();
                    renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = ShardMaterial();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 60f);
                }
                renderer.sortingLayerID = layer;
                renderer.sortingOrder = order;
                item.renderer = renderer;
                item.tr = renderer.transform;
            }
            item.tr.gameObject.SetActive(false);
            _trailBits.Add(item);
            return item;
        }

        private static void Activate(TrailBit bit)
        {
            bit.active = true;
            bit.tr.gameObject.SetActive(true);
        }

        private static void Deactivate(TrailBit bit)
        {
            bit.active = false;
            if (bit.tr != null) bit.tr.gameObject.SetActive(false);
        }

        // A thin, double-sided triangular sliver of glass, about one unit across.
        private static Mesh ShardMesh()
        {
            if (_shardMesh != null) return _shardMesh;
            var a = new Vector3(0f, .62f, 0f);
            var b = new Vector3(-.42f, -.4f, 0f);
            var c = new Vector3(.5f, -.3f, 0f);
            _shardMesh = new Mesh { name = "Glass shard" };
            _shardMesh.vertices = new[] { a, b, c, a, c, b };
            _shardMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward };
            _shardMesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            _shardMesh.RecalculateBounds();
            return _shardMesh;
        }

        private static Material ShardMaterial()
        {
            if (_shardMaterial != null) return _shardMaterial;
            var shader = Resources.Load<Shader>("Pieces/ChessPiece");
            if (shader == null) return null;
            _shardMaterial = new Material(shader) { name = "Glass shard" };
            _shardMaterial.SetFloat("_Smoothness", 1f);
            _shardMaterial.SetFloat("_PieceClip", 1f);
            return _shardMaterial;
        }
    }
}
