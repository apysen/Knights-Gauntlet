using System.Collections.Generic;
using UnityEngine;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // The War Horn "rally": the on-board half of the powerup's animation.
    //
    // Timeline (seconds since the horn sounds):
    //   0.00  horn blast: white-gold flash on the knight, three shockwave rings roll out
    //         across the board, a sunburst swells behind the knight, embers scatter.
    //   0.30  the multiplier starts counting back up from where it fell, accelerating.
    //   1.05  the count lands on the run's peak: SLAM. A second flash, two more rings,
    //         a second ember burst and a kick in the sunburst.
    //   1.75  everything has faded out.
    //
    // The clock lives here (driven by Sync's dt) so the board effects, the HUD banner
    // (UIManager.Rally) and the sim's slow-motion (RallyTimeScale) all read one source.
    public partial class GameView
    {
        public const float RallyDuration = 1.75f;
        public const float RallyCountStart = .30f, RallyCountEnd = 1.05f;

        private float _rallyT = -1f;
        private int _rallyFrom, _rallyTo, _rallyShown;
        private bool _rallySlammed, _rallyReduced;
        private float _rallyLastTick;
        private Vector3 _rallyOrigin;
        private System.Action<int, float> _onRallyTick;
        private System.Action _onRallySlam;

        private Transform _rallyRoot;
        private SpriteRenderer _rallyRays, _rallyRaysInner, _rallyFlash;
        private readonly SpriteRenderer[] _rallyRings = new SpriteRenderer[5];
        private readonly Vector3[] _rallyRingOrigin = new Vector3[5];
        private static readonly float[] RallyRingStart = { 0f, .11f, .22f, RallyCountEnd, RallyCountEnd + .09f };
        private const float RallyRingLife = .72f;
        private readonly List<Ember> _embers = new List<Ember>();
        // gravity: cells/s^2 upward (negative falls); sway: side-to-side drift (cells/s) for floating motes;
        // pull: drag strength (embers decelerate fast, falling light motes barely at all).
        private class Ember { public SpriteRenderer sr; public Vector3 vel; public float life, maxLife, size, flicker, gravity = 1.6f, sway, pull = 3.2f; public Color color; }

        private static readonly Color RallyGold = new Color(1f, .80f, .30f, 1f);
        private static readonly Color RallyHot = new Color(1f, .96f, .82f, 1f);
        private static readonly Color RallyEmber = new Color(1f, .55f, .16f, 1f);
        private static Sprite _rallyRingSprite, _rallyRaysSprite;

        public bool RallyPlaying => _rallyT >= 0f;
        public float RallyTime => _rallyT;
        public int RallyFrom => _rallyFrom;
        public int RallyTo => _rallyTo;
        public int RallyShown => _rallyShown; // the multiplier the count-up is showing right now

        // Slow motion for the simulation while the horn plays: a near-freeze hit-stop on the
        // blast, then it eases back to full speed just after the slam. Gives the moment room
        // to breathe (and the player a beat to line up the next capture).
        public float RallyTimeScale
        {
            get
            {
                if (_rallyT < 0f) return 1f;
                if (_rallyT < .14f) return .05f;
                float u = Mathf.InverseLerp(.14f, RallyCountEnd + .15f, _rallyT);
                return Mathf.Lerp(.25f, 1f, u * u * (3f - 2f * u));
            }
        }

        // Start the rally. onTick fires each time the shown multiplier steps up (value,
        // count progress 0..1); onSlam fires once when it lands on the peak.
        public void PlayWarHornRally(GameSimulation sim, int from, int to,
                                     System.Action<int, float> onTick, System.Action onSlam)
        {
            if (_board == null) return;
            EnsureRallyObjects();
            _rallyT = 0f;
            _rallyFrom = from; _rallyTo = Mathf.Max(from, to); _rallyShown = from;
            _rallySlammed = false; _rallyLastTick = -1f;
            _rallyReduced = SaveData.IsReducedEffects();
            _onRallyTick = onTick; _onRallySlam = onSlam;
            _rallyOrigin = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
            for (int i = 0; i < _rallyRingOrigin.Length; i++) _rallyRingOrigin[i] = _rallyOrigin;
            _rallyRoot.gameObject.SetActive(true);
            if (!_rallyReduced) SpawnEmbers(_rallyOrigin, 34, 1f);
        }

        // Hard stop (run reset / restart): the rally and every powerup effect in flight.
        public void StopRally()
        {
            EndRally();
            for (int i = 0; i < _embers.Count; i++) { _embers[i].life = 0f; _embers[i].sr.gameObject.SetActive(false); }
            StopPowerupFx();
        }

        // The rally finished on its own: leave any sparks still in the air to fade out.
        private void EndRally()
        {
            _rallyT = -1f; _onRallyTick = null; _onRallySlam = null;
            if (_rallyRoot != null) _rallyRoot.gameObject.SetActive(false);
        }

        // Called at the end of Sync, after the player has been positioned.
        private void AnimateRally(GameSimulation sim, float dt)
        {
            AnimateEmbers(dt);
            AnimatePowerupFx(sim, dt);
            if (_rallyT < 0f) return;
            _rallyT += dt;
            float t = _rallyT;
            float cell = _board.cellSize;
            Vector3 knight = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);

            // ── Count-up ─────────────────────────────────────────────────────
            float p = Mathf.InverseLerp(RallyCountStart, RallyCountEnd, t);
            float eased = p * p * (1.3f - .3f * p); // ease in: the count accelerates into the slam
            int shown = p >= 1f ? _rallyTo : _rallyFrom + Mathf.FloorToInt((_rallyTo - _rallyFrom) * eased);
            if (shown != _rallyShown)
            {
                _rallyShown = shown;
                // Throttle the tick feedback so a huge jump doesn't machine-gun the shake.
                if (shown < _rallyTo && t - _rallyLastTick >= .045f)
                {
                    _rallyLastTick = t;
                    _onRallyTick?.Invoke(shown, p);
                }
            }
            if (!_rallySlammed && p >= 1f)
            {
                _rallySlammed = true;
                _rallyRingOrigin[3] = _rallyRingOrigin[4] = knight;
                if (!_rallyReduced) SpawnEmbers(knight, 28, 1.25f);
                _onRallySlam?.Invoke();
            }
            float sinceSlam = _rallySlammed ? t - RallyCountEnd : 99f;

            // ── Sunburst behind the knight (follows it if it moves) ──────────
            bool rays = !_rallyReduced;
            _rallyRays.gameObject.SetActive(rays);
            _rallyRaysInner.gameObject.SetActive(rays);
            if (rays)
            {
                float grow = EaseOutBack(Mathf.Clamp01(t / .38f));
                float kick = sinceSlam < .5f ? .9f * Mathf.Exp(-sinceSlam * 7f) : 0f;
                float alpha = Mathf.Clamp01(t / .12f) * (1f - Mathf.Clamp01((t - 1.25f) / .45f));
                _rallyRays.transform.position = knight;
                _rallyRays.transform.localScale = Vector3.one * cell * (1.2f + 2.6f * grow + kick);
                _rallyRays.transform.localRotation = Quaternion.Euler(0f, 0f, t * 38f);
                _rallyRays.color = WithAlpha(RallyGold, .80f * alpha);
                _rallyRaysInner.transform.position = knight;
                _rallyRaysInner.transform.localScale = Vector3.one * cell * (.8f + 1.5f * grow + kick * .7f);
                _rallyRaysInner.transform.localRotation = Quaternion.Euler(0f, 0f, 11f - t * 64f);
                _rallyRaysInner.color = WithAlpha(RallyHot, .55f * alpha);
            }

            // ── Flash on the blast and again on the slam ─────────────────────
            float flashA = 0f, flashS = 1f;
            if (!_rallyReduced)
            {
                if (t < .34f) { float u = t / .34f; flashA = 1f - u; flashS = 1.4f + 3.2f * u; }
                if (sinceSlam < .30f) { float u = sinceSlam / .30f; flashA = Mathf.Max(flashA, 1f - u); flashS = 1.6f + 3.6f * u; }
            }
            _rallyFlash.gameObject.SetActive(flashA > 0f);
            if (flashA > 0f)
            {
                _rallyFlash.transform.position = knight;
                _rallyFlash.transform.localScale = Vector3.one * cell * flashS;
                _rallyFlash.color = WithAlpha(Color.Lerp(RallyGold, RallyHot, flashA), flashA * flashA);
            }

            // ── Shockwave rings rolling out across the board ─────────────────
            for (int i = 0; i < _rallyRings.Length; i++)
            {
                var ring = _rallyRings[i];
                float u = (t - RallyRingStart[i]) / RallyRingLife;
                bool slamRing = i >= 3;
                bool on = u >= 0f && u < 1f && (!slamRing || _rallySlammed) && (!_rallyReduced || i == 0 || i == 3);
                ring.gameObject.SetActive(on);
                if (!on) continue;
                float e = 1f - (1f - u) * (1f - u) * (1f - u); // ease out cubic
                float radius = Mathf.Lerp(.35f, slamRing ? 5.2f : 4.2f, e);
                ring.transform.position = _rallyRingOrigin[i];
                ring.transform.localScale = Vector3.one * cell * radius * 2f;
                float a = Mathf.Pow(1f - u, 1.4f) * (slamRing ? 1f : .85f - i * .14f);
                ring.color = WithAlpha(Color.Lerp(RallyHot, RallyGold, u), a);
            }

            if (_rallyT >= RallyDuration) EndRally();
        }

        private void SpawnEmbers(Vector3 at, int count, float power)
        {
            float cell = _board.cellSize;
            for (int i = 0; i < count; i++)
            {
                var em = GetEmber();
                float ang = Random.value * Mathf.PI * 2f;
                float speed = cell * Random.Range(2.2f, 6.2f) * power;
                em.vel = new Vector3(Mathf.Cos(ang) * speed, Mathf.Sin(ang) * speed + cell * 1.2f, 0f);
                em.maxLife = em.life = Random.Range(.55f, 1.15f);
                em.size = cell * Random.Range(.10f, .22f);
                em.flicker = Random.value * 10f;
                float heat = Random.value;
                em.color = heat < .35f ? RallyHot : Color.Lerp(RallyGold, RallyEmber, Random.value);
                em.gravity = 1.6f; em.sway = 0f; em.pull = 3.2f;
                em.sr.transform.position = at + (Vector3)(Random.insideUnitCircle * cell * .25f);
                em.sr.gameObject.SetActive(true);
            }
        }

        private void AnimateEmbers(float dt)
        {
            if (_embers.Count == 0) return;
            float cell = _board != null ? _board.cellSize : 1f;
            for (int i = 0; i < _embers.Count; i++)
            {
                var em = _embers[i];
                if (em.life <= 0f) continue;
                em.life -= dt;
                if (em.life <= 0f) { em.sr.gameObject.SetActive(false); continue; }
                em.vel *= Mathf.Exp(-em.pull * dt);
                em.vel.y += cell * em.gravity * dt; // embers drift upward like sparks off a brazier
                Vector3 drift = em.sway != 0f ? new Vector3(Mathf.Sin(em.flicker + em.life * 3.1f) * em.sway * cell, 0f, 0f) : Vector3.zero;
                em.sr.transform.position += (em.vel + drift) * dt;
                float k = em.life / em.maxLife;
                float flicker = .75f + .25f * Mathf.Sin(em.flicker + (1f - k) * 40f);
                em.sr.transform.localScale = Vector3.one * em.size * (.4f + .6f * k);
                em.sr.color = WithAlpha(em.color, Mathf.Clamp01(k * 1.6f) * flicker);
            }
        }

        private Ember GetEmber()
        {
            for (int i = 0; i < _embers.Count; i++) if (_embers[i].life <= 0f) return _embers[i];
            var go = new GameObject("RallyEmber");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = UISprites.SoftGlow();
            sr.sortingOrder = 22;
            var em = new Ember { sr = sr };
            _embers.Add(em);
            return em;
        }

        private void EnsureRallyObjects()
        {
            if (_rallyRoot != null) return;
            _rallyRoot = new GameObject("WarHornRally").transform;
            _rallyRoot.SetParent(transform, false);
            // Rings and rays lie on the board (under the pieces, bent with its curve);
            // the flash sits over everything.
            for (int i = 0; i < _rallyRings.Length; i++)
            {
                _rallyRings[i] = RallySprite("Shockwave" + i, RallyRing(), 7);
                BendWithBoard(_rallyRings[i]);
            }
            _rallyRays = RallySprite("Sunburst", RallyRays(), 7);
            _rallyRaysInner = RallySprite("SunburstInner", RallyRays(), 7);
            ClipBoardSprite(_rallyRays); ClipBoardSprite(_rallyRaysInner);
            _rallyFlash = RallySprite("HornFlash", UISprites.SoftGlow(), 23);
        }

        private SpriteRenderer RallySprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_rallyRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingOrder = order; sr.color = Color.clear;
            go.SetActive(false);
            return sr;
        }

        // A shockwave: a bright crisp outer rim with a soft glow trailing inward.
        private static Sprite RallyRing()
        {
            if (_rallyRingSprite != null) return _rallyRingSprite;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            float c = (n - 1) * .5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (n * .5f); // 0..1 at the rim
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(r - .93f) / .05f);
                    float trail = r < .93f ? Mathf.Pow(Mathf.Clamp01((r - .55f) / .38f), 2.2f) * .45f : 0f;
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(rim, trail)));
                }
            tex.SetPixels(px); tex.Apply();
            _rallyRingSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n); // 1 world unit
            return _rallyRingSprite;
        }

        // A sunburst of 14 tapering rays, faded at the centre and the tips.
        internal static Sprite RallyRays()
        {
            if (_rallyRaysSprite != null) return _rallyRaysSprite;
            const int n = 192, rays = 14;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            float c = (n - 1) * .5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (n * .5f);
                    float ang = Mathf.Atan2(dy, dx);
                    float beam = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * rays * .5f)), 6f);
                    // Narrow the beams toward the tips so they read as rays, not a star.
                    beam = Mathf.Clamp01((beam - r * .35f) / (1f - r * .35f));
                    float radial = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r / .12f);
                    px[y * n + x] = new Color(1f, 1f, 1f, beam * radial * radial);
                }
            tex.SetPixels(px); tex.Apply();
            _rallyRaysSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
            return _rallyRaysSprite;
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float m = x - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }
    }
}
