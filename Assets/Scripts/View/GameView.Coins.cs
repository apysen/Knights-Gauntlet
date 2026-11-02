using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    public partial class GameView
    {
        // ── 3D shilling pickups ──────────────────────────────────────────────
        // Sizes are in board cells. The coin stands on its edge, spins about the
        // board's up axis and bobs gently, like a classic pickup.
        private const float Coin3DDiameter = .5f;
        private const float Coin3DHover = .08f;      // gap between the coin and its tile
        private const float Coin3DBob = .035f;       // bob height
        private const float Coin3DSpinSpeed = 140f;  // degrees per second

        // ── Collect burst ────────────────────────────────────────────────────
        // Layered on the exact frame of the pickup: the coin pops (scale punch),
        // whips round and rises off the tile while gold glints burst and a ring
        // flashes on the tile. With the HUD counter showing, the same coin is then
        // handed to the UI at its spot on screen and flies into the counter; without
        // it, the coin winks out. GameManager adds the sound and "+N" popup.
        private const float BurstDuration = .42f;
        private const float BurstHandoff = .5f;      // share of the burst before the coin leaves for the HUD
        private const float BurstRise = .75f;        // cells the coin climbs
        private const float BurstPunch = .45f;       // extra scale at the peak of the pop
        private const float BurstExtraSpin = 720f;   // degrees added over the burst
        private const int BurstGlints = 8;
        private const float BurstGlintReach = .7f;   // cells the glints fly out

        private static readonly Color CoinShadow = new Color(0f, 0f, 0f, .85f);
        private static readonly Color GlintColor = new Color(1f, .93f, .62f, 1f);

        private sealed class CoinPickup { public MeshRenderer coin; public SpriteRenderer shadow; }
        private sealed class CoinBurst
        {
            public MeshRenderer coin;
            public SpriteRenderer ring;
            public readonly SpriteRenderer[] glints = new SpriteRenderer[BurstGlints];
            public readonly Vector3[] directions = new Vector3[BurstGlints];
            public Vector3 tile;
            public float spin, age;
            public bool active;
            public System.Action<Vector3, float, float> handoff; // (world centre, world diameter, face-on 0..1)
        }

        private readonly List<CoinPickup> _coin3DPool = new List<CoinPickup>();
        private readonly List<CoinBurst> _coinBursts = new List<CoinBurst>();

        private float CoinScale => Coin3DDiameter * _board.cellSize / CoinMesh.Diameter;
        private float CoinSpinAngle(int col) => _coinAnimT * Coin3DSpinSpeed + col * 47f;

        // Coin axis is its local +Y. Lay that along the board so the coin stands on
        // its edge, then spin it about the board normal (-Z points up off the board).
        private static Quaternion CoinRotation(float spin) =>
            Quaternion.AngleAxis(spin, Vector3.back) * Quaternion.Euler(0f, 0f, 90f);

        private void PlaceCoin3D(int index, int col, float y)
        {
            while (index >= _coin3DPool.Count)
            {
                var pickup = new CoinPickup { coin = CoinMesh.Create("Shilling3D", _pickupsRoot, true) };
                pickup.coin.sortingOrder = 7;
                var shadowGo = new GameObject("ShillingShadow");
                shadowGo.transform.SetParent(_pickupsRoot, false);
                pickup.shadow = shadowGo.AddComponent<SpriteRenderer>();
                pickup.shadow.sprite = UISprites.SoftGlow(); // fades to zero, no square edge
                pickup.shadow.color = CoinShadow;
                pickup.shadow.sortingOrder = 6;
                ClipBoardSprite(pickup.shadow);
                _coin3DPool.Add(pickup);
            }
            var p = _coin3DPool[index];
            p.coin.gameObject.SetActive(true);
            p.shadow.gameObject.SetActive(true);

            Vector3 tile = _board.CellToWorld(col, y);
            float bob = Mathf.Sin(_coinAnimT * 2.6f + col * 1.3f) * Coin3DBob;
            Vector3 center = tile;
            center.z -= (Coin3DDiameter * .5f + Coin3DHover + bob) * _board.cellSize;
            p.coin.transform.SetPositionAndRotation(center, CoinRotation(CoinSpinAngle(col)));
            p.coin.transform.localScale = Vector3.one * CoinScale;
            // The contact shadow tightens as the coin dips, which sells the bob.
            p.shadow.transform.position = tile;
            p.shadow.transform.localScale = Vector3.one * (_board.cellSize * (.6f - bob * 2f));
        }

        private void HideCoins3D(int from)
        {
            for (int i = from; i < _coin3DPool.Count; i++)
            {
                _coin3DPool[i].coin.gameObject.SetActive(false);
                _coin3DPool[i].shadow.gameObject.SetActive(false);
            }
        }

        // Called from GameManager on the frame a shilling is collected. With a handoff,
        // the rising coin is passed on (and hidden here) once its pop has played.
        // False when no burst plays (reduced effects).
        public bool PlayCoinCollect(int col, float y, System.Action<Vector3, float, float> handoff = null)
        {
            if (SaveData.IsReducedEffects() || _board == null) return false;
            var burst = GetCoinBurst();
            burst.active = true;
            burst.age = 0f;
            burst.handoff = handoff;
            burst.tile = _board.CellToWorld(col, y);
            burst.spin = CoinSpinAngle(col);
            for (int i = 0; i < BurstGlints; i++)
            {
                // Evenly fanned with a little jitter, angled up off the board.
                float a = (i + Random.Range(-.3f, .3f)) / BurstGlints * Mathf.PI * 2f;
                burst.directions[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), -Random.Range(.4f, 1f)).normalized;
            }
            burst.coin.gameObject.SetActive(true);
            burst.ring.gameObject.SetActive(true);
            foreach (var g in burst.glints) g.gameObject.SetActive(true);
            AnimateCoinBurst(burst, 0f);
            return true;
        }

        private void AnimateCoinBursts(float dt)
        {
            for (int i = 0; i < _coinBursts.Count; i++)
                if (_coinBursts[i].active) AnimateCoinBurst(_coinBursts[i], dt);
        }

        private void AnimateCoinBurst(CoinBurst b, float dt)
        {
            b.age += dt;
            float t = Mathf.Clamp01(b.age / BurstDuration);
            if (t >= 1f)
            {
                b.active = false;
                b.coin.gameObject.SetActive(false);
                b.ring.gameObject.SetActive(false);
                foreach (var g in b.glints) g.gameObject.SetActive(false);
                return;
            }
            float cell = _board.cellSize;
            float easeOut = 1f - (1f - t) * (1f - t) * (1f - t);

            // Coin: pop, whip round, rise, then shrink away over the last 40%.
            float punch = 1f + BurstPunch * Mathf.Sin(Mathf.Min(t / .45f, 1f) * Mathf.PI);
            float shrink = t < .6f ? 1f : 1f - Mathf.Pow((t - .6f) / .4f, 2f);
            Vector3 center = b.tile;
            center.z -= (Coin3DDiameter * .5f + Coin3DHover + BurstRise * easeOut) * cell;
            b.coin.transform.SetPositionAndRotation(center, CoinRotation(b.spin + BurstExtraSpin * easeOut));
            b.coin.transform.localScale = Vector3.one * (CoinScale * punch * (b.handoff != null ? 1f : shrink));
            if (b.handoff != null && t >= BurstHandoff)
            {
                // The same coin continues as the HUD's flying coin from exactly here.
                // How much of its face the camera sees (it is mid-spin), so the UI coin
                // can start at the same apparent width and finish turning to face us.
                var view = Camera.main;
                float faceOn = view != null ? Mathf.Abs(Vector3.Dot(b.coin.transform.up, view.transform.forward)) : 1f;
                b.handoff(center, CoinScale * punch * CoinMesh.Diameter, faceOn);
                b.handoff = null;
                b.coin.gameObject.SetActive(false);
            }

            // Ring on the tile: expands and fades over the first 60%.
            float rt = Mathf.Clamp01(t / .6f);
            b.ring.transform.position = b.tile;
            b.ring.transform.localScale = Vector3.one * (cell * Mathf.Lerp(.35f, 1.25f, 1f - (1f - rt) * (1f - rt)));
            b.ring.color = new Color(GlintColor.r, GlintColor.g, GlintColor.b, .8f * (1f - rt));

            // Glints burst from where the coin is, facing the camera.
            var cam = Camera.main;
            Quaternion facing = cam != null ? cam.transform.rotation : Quaternion.identity;
            float size = cell * .3f * (1f - t * t);
            for (int i = 0; i < BurstGlints; i++)
            {
                var g = b.glints[i];
                g.transform.SetPositionAndRotation(center + b.directions[i] * (BurstGlintReach * cell * easeOut), facing);
                g.transform.localScale = Vector3.one * size;
                g.color = new Color(GlintColor.r, GlintColor.g, GlintColor.b, 1f - t * t);
            }
        }

        private CoinBurst GetCoinBurst()
        {
            for (int i = 0; i < _coinBursts.Count; i++) if (!_coinBursts[i].active) return _coinBursts[i];
            var b = new CoinBurst { coin = CoinMesh.Create("ShillingBurst", _pickupsRoot, true) };
            b.coin.sortingOrder = 16; // over the knight, which is usually standing on the tile
            var ringGo = new GameObject("ShillingRing");
            ringGo.transform.SetParent(_pickupsRoot, false);
            b.ring = ringGo.AddComponent<SpriteRenderer>();
            b.ring.sprite = UISprites.MoveRing();
            b.ring.sortingOrder = 6;
            ClipBoardSprite(b.ring);
            for (int i = 0; i < BurstGlints; i++)
            {
                var go = new GameObject("ShillingGlint");
                go.transform.SetParent(_pickupsRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = UISprites.Star();
                sr.sortingOrder = 17;
                b.glints[i] = sr;
            }
            _coinBursts.Add(b);
            return b;
        }
    }
}
