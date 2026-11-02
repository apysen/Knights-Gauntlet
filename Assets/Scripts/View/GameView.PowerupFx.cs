using System.Collections.Generic;
using UnityEngine;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // On-board activation effects for Chronokinesis, Divine Intervention and Pathfinder,
    // plus their lingering "auras" while active. Same toolkit as the War Horn rally
    // (GameView.Rally): one-shot sprites (rings, flashes, spinning dials, light beams,
    // travelling sparks) from a small pool, and the shared spark/mote particles.
    //
    // Chronokinesis "Time Bends": a clock face spins backwards out of the knight while two
    //   rings collapse inward and sand is pulled back toward it (time running in reverse).
    //   While active, a faint dial turns slowly backwards under the knight; when it runs out
    //   the dial snaps shut.
    // Divine Intervention "Blessed": a shaft of light drops onto the knight, a halo and soft
    //   rays bloom, motes of light drift down, and the recommended square is pinged. While
    //   active, the green marker breathes and sheds rising sparkles.
    // Pathfinder "Path Revealed": a compass dial whirls and settles, a spark runs from the
    //   knight to the next step and lands with a ripple.
    public partial class GameView
    {
        private enum FxKind { Ring, RingIn, Flash, Dial, Beam, Travel }
        private class FxSprite
        {
            public SpriteRenderer sr; public bool onBoard, active, followKnight;
            public FxKind kind; public float t, delay, life, r0, r1, spin, alpha;
            public Vector3 a, b; public Color c0, c1;
        }
        private readonly List<FxSprite> _fx = new List<FxSprite>();
        private Material _fxSpriteDefault;
        private static Sprite _dialSprite, _beamSprite;

        private float _hitStop;             // seconds of near-freeze left (activation punch)
        private SpriteRenderer _chronoDial;  // lingering backwards clock under the knight
        private float _chronoWas, _chronoMoteTimer, _divineSparkleTimer, _auraT;

        // GameManager writes these each frame: seconds left on each timed powerup.
        public float ChronoAura { get; set; }
        public float DivineAura { get; set; }
        public float FuryAura { get; set; }

        // Berserker Draught: fire colours, from white-hot core to deep red.
        private static readonly Color FuryCore = new Color(1f, .93f, .6f, 1f);
        private static readonly Color FuryFlame = new Color(1f, .45f, .12f, 1f);
        private static readonly Color FuryBlood = new Color(.9f, .12f, .08f, 1f);
        private SpriteRenderer _furyGlow;
        private float _furyEmberTimer;

        private static readonly Color ChronoCyan = new Color(.45f, .9f, 1f, 1f);
        private static readonly Color ChronoSand = new Color(.98f, .82f, .5f, 1f);
        private static readonly Color HolyWhite = new Color(1f, .98f, .9f, 1f);
        private static readonly Color HolyGold = new Color(1f, .86f, .45f, 1f);
        private static readonly Color HolyGreen = new Color(.36f, 1f, .56f, 1f);
        private static readonly Color MapInk = new Color(.95f, .78f, .42f, 1f);

        // The sim's clock multiplier from every powerup effect: the War Horn's slow-mo and
        // the brief hit-stop that punctuates the other activations.
        public float PowerupTimeScale => Mathf.Min(RallyTimeScale, _hitStop > 0f ? .08f : 1f);

        // ── Activations ──────────────────────────────────────────────────────
        public void PlayChronoFx(GameSimulation sim)
        {
            if (_board == null) return;
            Vector3 k = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
            bool full = !SaveData.IsReducedEffects();
            _hitStop = .16f;
            Spawn(FxKind.Flash, k, ChronoCyan, HolyWhite, 0f, .35f, 1.2f, 4f, 0f, 1f, false, true);
            Spawn(FxKind.Dial, k, ChronoCyan, ChronoCyan, 0f, 1.4f, .6f, 3.4f, -540f, .9f, true, true);
            Spawn(FxKind.RingIn, k, HolyWhite, ChronoCyan, 0f, .65f, 4.6f, .3f, 0f, .9f, true, false);
            if (full)
            {
                Spawn(FxKind.RingIn, k, HolyWhite, ChronoCyan, .16f, .6f, 3.8f, .3f, 0f, .7f, true, false);
                Spawn(FxKind.Ring, k, ChronoCyan, ChronoCyan, .78f, .6f, .3f, 3.6f, 0f, .7f, true, false);
                SpawnInwardMotes(k, 34, 3.6f);
            }
        }

        public void PlayDivineFx(GameSimulation sim, Vector3? best)
        {
            if (_board == null) return;
            Vector3 k = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
            bool full = !SaveData.IsReducedEffects();
            _hitStop = .14f;
            Spawn(FxKind.Beam, k, HolyWhite, HolyGold, 0f, 1.3f, 1.5f, 3.4f, 0f, .75f, false, true);
            Spawn(FxKind.Flash, k, HolyWhite, HolyGold, .05f, .45f, 1.4f, 4.2f, 0f, 1f, false, true);
            Spawn(FxKind.Ring, k, HolyGold, HolyWhite, .08f, .7f, .4f, 3.4f, 0f, .85f, true, false);
            if (full)
            {
                Spawn(FxKind.Dial, k, HolyGold, HolyGold, .02f, 1.5f, .8f, 3f, 70f, .55f, true, true, RallyRays());
                SpawnFallingMotes(k, 26);
            }
            if (best.HasValue)
            {
                // Point the player at the answer: a spark arcs over and the square rings.
                Spawn(FxKind.Travel, k, HolyWhite, HolyGreen, .3f, .38f, .5f, .5f, 0f, 1f, false, false, null, best.Value);
                Spawn(FxKind.Flash, best.Value, HolyGreen, HolyWhite, .66f, .4f, .8f, 2.4f, 0f, .9f, false, false);
                Spawn(FxKind.Ring, best.Value, HolyWhite, HolyGreen, .66f, .55f, .3f, 1.6f, 0f, 1f, true, false);
            }
        }

        public void PlayPathFx(GameSimulation sim, Vector3? next)
        {
            if (_board == null) return;
            Vector3 k = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
            bool full = !SaveData.IsReducedEffects();
            Spawn(FxKind.Flash, k, MapInk, HolyWhite, 0f, .35f, 1f, 3f, 0f, .9f, false, true);
            Spawn(FxKind.Dial, k, MapInk, MapInk, 0f, 1.1f, .5f, 2.4f, 900f, .9f, true, true);
            Spawn(FxKind.Ring, k, HolyWhite, MapInk, 0f, .6f, .3f, 3.2f, 0f, .8f, true, false);
            if (full) SpawnEmbersColored(k, 18, .8f, MapInk, HolyWhite);
            if (next.HasValue)
            {
                Spawn(FxKind.Travel, k, HolyWhite, HolyGreen, .45f, .4f, .55f, .55f, 0f, 1f, false, false, null, next.Value);
                Spawn(FxKind.Flash, next.Value, HolyGreen, HolyWhite, .85f, .4f, .8f, 2.2f, 0f, .9f, false, false);
                Spawn(FxKind.Ring, next.Value, HolyWhite, HolyGreen, .85f, .5f, .3f, 1.5f, 0f, 1f, true, false);
            }
        }

        // Berserker Draught: the knight ignites. A pillar of flame, a fiery shockwave,
        // a spinning burst of red rays and a fountain of embers.
        public void PlayFuryFx(GameSimulation sim)
        {
            if (_board == null) return;
            Vector3 k = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
            bool full = !SaveData.IsReducedEffects();
            _hitStop = .16f;
            Spawn(FxKind.Flash, k, FuryCore, FuryBlood, 0f, .4f, 1.4f, 4.6f, 0f, 1f, false, true);
            Spawn(FxKind.Beam, k, FuryCore, FuryFlame, 0f, .9f, 1.3f, 1.4f, 0f, .85f, false, true);
            Spawn(FxKind.Ring, k, FuryCore, FuryBlood, .04f, .6f, .3f, 4f, 0f, 1f, true, false);
            if (full)
            {
                Spawn(FxKind.Ring, k, FuryFlame, FuryBlood, .14f, .65f, .3f, 3.2f, 0f, .7f, true, false);
                Spawn(FxKind.Dial, k, FuryFlame, FuryBlood, 0f, 1.1f, .9f, 3.6f, 260f, .7f, true, true, RallyRays());
                SpawnEmbersColored(k, 40, 1.3f, FuryCore, FuryFlame);
                SpawnEmbersColored(k, 14, .9f, FuryFlame, FuryBlood);
            }
        }

        // A capture made while the draught burns: a small fire burst on the taken piece.
        public void PlayFuryCapture(int col, float y)
        {
            if (_board == null) return;
            Vector3 at = _board.CellToWorld(col, y);
            Spawn(FxKind.Ring, at, FuryCore, FuryBlood, 0f, .45f, .25f, 1.6f, 0f, .9f, true, false);
            Spawn(FxKind.Flash, at, FuryCore, FuryFlame, 0f, .3f, .6f, 2f, 0f, .8f, false, false);
            if (!SaveData.IsReducedEffects()) SpawnEmbersColored(at, 12, .8f, FuryCore, FuryFlame, .8f);
        }

        public void StopPowerupFx()
        {
            for (int i = 0; i < _fx.Count; i++) { _fx[i].active = false; _fx[i].sr.gameObject.SetActive(false); }
            _hitStop = 0f; ChronoAura = 0f; DivineAura = 0f; FuryAura = 0f; _chronoWas = 0f;
            if (_furyGlow != null) _furyGlow.gameObject.SetActive(false);
            if (_chronoDial != null) _chronoDial.gameObject.SetActive(false);
        }

        // ── Per frame (from AnimateRally, which runs at the end of Sync) ─────
        private void AnimatePowerupFx(GameSimulation sim, float dt)
        {
            if (_board == null) return;
            if (_hitStop > 0f) _hitStop -= Time.unscaledDeltaTime;
            _auraT += dt;
            Vector3 knight = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
            float cell = _board.cellSize;

            for (int i = 0; i < _fx.Count; i++)
            {
                var f = _fx[i];
                if (!f.active) continue;
                f.t += dt;
                float u = (f.t - f.delay) / f.life;
                if (u >= 1f) { f.active = false; f.sr.gameObject.SetActive(false); continue; }
                bool on = u >= 0f;
                if (f.sr.gameObject.activeSelf != on) f.sr.gameObject.SetActive(on);
                if (!on) continue;
                if (f.followKnight) f.a = knight;
                float outCubic = 1f - (1f - u) * (1f - u) * (1f - u);
                float size, alpha;
                Vector3 pos = f.a;
                float rot = 0f;
                Vector3 scale;
                switch (f.kind)
                {
                    case FxKind.Ring:
                        size = Mathf.Lerp(f.r0, f.r1, outCubic); alpha = Mathf.Pow(1f - u, 1.4f);
                        scale = Vector3.one * size * 2f; break;
                    case FxKind.RingIn:
                        size = Mathf.Lerp(f.r0, f.r1, u * u); alpha = Mathf.Clamp01(u * 5f) * (1f - u * u);
                        scale = Vector3.one * size * 2f; break;
                    case FxKind.Flash:
                        size = Mathf.Lerp(f.r0, f.r1, outCubic); alpha = (1f - u) * (1f - u);
                        scale = Vector3.one * size; break;
                    case FxKind.Dial:
                        size = Mathf.LerpUnclamped(f.r0, f.r1, EaseOutBack(Mathf.Clamp01(u / .3f)));
                        alpha = Mathf.Clamp01(u / .12f) * Mathf.Clamp01((1f - u) / .35f);
                        rot = f.spin * outCubic; scale = Vector3.one * size; break;
                    case FxKind.Beam:
                        // Slams down narrow and bright, widens, then thins away.
                        float w = Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.15f));
                        alpha = Mathf.Clamp01(u * 8f) * Mathf.Clamp01((1f - u) / .4f);
                        scale = new Vector3(f.r0 * (.35f + .65f * w), f.r1 * Mathf.Min(1f, u * 6f), 1f); break;
                    default: // Travel: a spark arcing from a to b
                        float e = u * u * (3f - 2f * u);
                        pos = Vector3.Lerp(f.a, f.b, e) + Vector3.up * cell * .9f * Mathf.Sin(Mathf.PI * e);
                        alpha = Mathf.Clamp01((1f - u) / .2f);
                        scale = Vector3.one * f.r0 * (1f + .3f * Mathf.Sin(u * 30f)); break;
                }
                f.sr.transform.position = pos;
                f.sr.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
                f.sr.transform.localScale = scale * cell;
                f.sr.color = WithAlpha(Color.Lerp(f.c0, f.c1, u), alpha * f.alpha);
                // Sparks leave a short trail of motes.
                if (f.kind == FxKind.Travel && Random.value < .7f)
                    SpawnEmbersColored(pos, 1, .15f, f.c0, f.c1, .35f);
            }

            AnimateChronoAura(sim, knight, cell, dt);
            AnimateDivineAura(dt);
            AnimateFuryAura(knight, cell, dt);
        }

        // While the draught burns: a pulsing red glow under the knight and flames licking
        // up off it. In the last 3 seconds the glow flickers faster as a warning.
        private void AnimateFuryAura(Vector3 knight, float cell, float dt)
        {
            float left = FuryAura;
            bool on = left > 0f;
            if (on && _furyGlow == null) { _furyGlow = FxRenderer("FuryAura", UISprites.SoftGlow(), 7); ClipBoardSprite(_furyGlow); }
            if (_furyGlow == null) return;
            if (_furyGlow.gameObject.activeSelf != on) _furyGlow.gameObject.SetActive(on);
            if (!on) return;
            float fade = Mathf.Clamp01(left / .6f);
            float rate = left < 3f ? 14f : 5f;
            float pulse = .5f + .5f * Mathf.Sin(_auraT * rate);
            _furyGlow.transform.position = knight;
            _furyGlow.transform.localScale = Vector3.one * cell * (2.1f + .25f * pulse);
            _furyGlow.color = WithAlpha(Color.Lerp(FuryBlood, FuryFlame, pulse), (.30f + .2f * pulse) * fade);
            if (SaveData.IsReducedEffects()) return;
            _furyEmberTimer -= dt;
            while (_furyEmberTimer <= 0f)
            {
                _furyEmberTimer += .055f;
                var em = GetEmber();
                em.sr.transform.position = knight + new Vector3(Random.Range(-.32f, .32f) * cell, Random.Range(-.3f, .1f) * cell, 0f);
                em.vel = new Vector3(Random.Range(-.2f, .2f) * cell, cell * Random.Range(.6f, 1.3f), 0f);
                em.gravity = 1.1f; em.sway = .3f; em.pull = 1.2f;
                em.maxLife = em.life = Random.Range(.35f, .7f);
                em.size = cell * Random.Range(.09f, .18f);
                em.flicker = Random.value * 10f;
                float heat = Random.value;
                em.color = heat < .3f ? FuryCore : heat < .75f ? FuryFlame : FuryBlood;
                em.sr.gameObject.SetActive(true);
            }
        }

        private void AnimateChronoAura(GameSimulation sim, Vector3 knight, float cell, float dt)
        {
            float left = ChronoAura;
            bool on = left > 0f;
            if (on && _chronoDial == null)
            {
                _chronoDial = FxRenderer("ChronoAura", DialSprite(), 7);
                BendWithBoard(_chronoDial);
            }
            if (_chronoDial != null)
            {
                if (_chronoDial.gameObject.activeSelf != on) _chronoDial.gameObject.SetActive(on);
                if (on)
                {
                    float fade = Mathf.Clamp01(left / 1f) * Mathf.Clamp01((Powerups.Chronokinesis.Duration - left) / .8f);
                    _chronoDial.transform.position = knight;
                    _chronoDial.transform.localRotation = Quaternion.Euler(0f, 0f, _auraT * 30f); // slowly backwards (CCW)
                    _chronoDial.transform.localScale = Vector3.one * cell * (1.9f + .06f * Mathf.Sin(_auraT * 2.2f));
                    _chronoDial.color = WithAlpha(ChronoCyan, .32f * fade);
                }
            }
            // Expired: the dial snaps shut onto the knight.
            if (_chronoWas > 0f && !on && left <= 0f && _chronoWas < .5f)
            {
                Spawn(FxKind.RingIn, knight, ChronoCyan, HolyWhite, 0f, .3f, 1.8f, .2f, 0f, .8f, true, false);
                Spawn(FxKind.Flash, knight, ChronoCyan, HolyWhite, .25f, .25f, .6f, 1.8f, 0f, .7f, false, true);
            }
            _chronoWas = left;
            // Sand keeps trickling back toward the knight while time is bent.
            if (on && !SaveData.IsReducedEffects())
            {
                _chronoMoteTimer -= dt;
                if (_chronoMoteTimer <= 0f) { _chronoMoteTimer = .22f; SpawnInwardMotes(knight, 2, 2.4f); }
            }
        }

        private void AnimateDivineAura(float dt)
        {
            if (_bestMoveFill == null || DivineAura <= 0f) return;
            // The green marker breathes rather than sitting static.
            float pulse = .5f + .5f * Mathf.Sin(_auraT * 4.2f);
            _bestMoveFill.color = new Color(.235f, .863f, .471f, .22f + .22f * pulse);
            _bestMoveBorder.color = new Color(Mathf.Lerp(.235f, .75f, pulse), Mathf.Lerp(.863f, 1f, pulse), Mathf.Lerp(.471f, .8f, pulse), .95f);
            if (!_bestMoveFill.gameObject.activeSelf || SaveData.IsReducedEffects()) return;
            _divineSparkleTimer -= dt;
            if (_divineSparkleTimer > 0f) return;
            _divineSparkleTimer = .16f;
            var em = GetEmber();
            float cell = _board.cellSize;
            em.sr.transform.position = _bestMoveFill.transform.position +
                new Vector3(Random.Range(-.42f, .42f) * cell, Random.Range(-.4f, .1f) * cell, 0f);
            em.vel = new Vector3(0f, cell * Random.Range(.4f, .9f), 0f);
            em.gravity = .3f; em.sway = .25f; em.pull = .5f;
            em.maxLife = em.life = Random.Range(.6f, 1f);
            em.size = cell * Random.Range(.08f, .15f);
            em.flicker = Random.value * 10f;
            em.color = Random.value < .5f ? HolyGreen : HolyWhite;
            em.sr.gameObject.SetActive(true);
        }

        // ── Particles ────────────────────────────────────────────────────────
        private void SpawnEmbersColored(Vector3 at, int count, float power, Color a, Color b, float lifeScale = 1f)
        {
            float cell = _board.cellSize;
            for (int i = 0; i < count; i++)
            {
                var em = GetEmber();
                float ang = Random.value * Mathf.PI * 2f;
                float speed = cell * Random.Range(1.6f, 5f) * power;
                em.vel = new Vector3(Mathf.Cos(ang) * speed, Mathf.Sin(ang) * speed + cell * .8f * power, 0f);
                em.gravity = 1.2f; em.sway = 0f; em.pull = 3.2f;
                em.maxLife = em.life = Random.Range(.5f, 1f) * lifeScale;
                em.size = cell * Random.Range(.08f, .18f);
                em.flicker = Random.value * 10f;
                em.color = Color.Lerp(a, b, Random.value);
                em.sr.transform.position = at;
                em.sr.gameObject.SetActive(true);
            }
        }

        // Sand drawn in from a ring around the knight, arriving as it fades.
        private void SpawnInwardMotes(Vector3 center, int count, float radiusCells)
        {
            float cell = _board.cellSize;
            for (int i = 0; i < count; i++)
            {
                var em = GetEmber();
                float ang = Random.value * Mathf.PI * 2f;
                float r = cell * radiusCells * Random.Range(.7f, 1f);
                var offset = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * r;
                em.maxLife = em.life = Random.Range(.55f, .9f);
                em.sr.transform.position = center + offset;
                em.vel = -offset / em.life; // arrives at the knight as it fades
                em.gravity = 0f; em.sway = 0f; em.pull = 0f;
                em.size = cell * Random.Range(.07f, .15f);
                em.flicker = Random.value * 10f;
                em.color = Random.value < .6f ? ChronoSand : ChronoCyan;
                em.sr.gameObject.SetActive(true);
            }
        }

        // Motes of light drifting down around the knight, swaying like falling feathers.
        private void SpawnFallingMotes(Vector3 center, int count)
        {
            float cell = _board.cellSize;
            for (int i = 0; i < count; i++)
            {
                var em = GetEmber();
                em.sr.transform.position = center + new Vector3(Random.Range(-2.2f, 2.2f) * cell, Random.Range(.5f, 3.5f) * cell, 0f);
                em.vel = new Vector3(0f, -cell * Random.Range(.3f, .8f), 0f);
                em.gravity = -.25f; em.sway = Random.Range(.35f, .7f); em.pull = .3f;
                em.maxLife = em.life = Random.Range(1.3f, 2.2f);
                em.size = cell * Random.Range(.09f, .2f);
                em.flicker = Random.value * 10f;
                em.color = Random.value < .5f ? HolyWhite : HolyGold;
                em.sr.gameObject.SetActive(true);
            }
        }

        // ── Pool ─────────────────────────────────────────────────────────────
        private void Spawn(FxKind kind, Vector3 at, Color c0, Color c1, float delay, float life,
                           float r0, float r1, float spin, float alpha, bool onBoard, bool followKnight,
                           Sprite sprite = null, Vector3 to = default)
        {
            FxSprite f = null;
            for (int i = 0; i < _fx.Count; i++) if (!_fx[i].active) { f = _fx[i]; break; }
            if (f == null)
            {
                f = new FxSprite { sr = FxRenderer("PowerupFx", null, 7) };
                if (_fxSpriteDefault == null) _fxSpriteDefault = f.sr.sharedMaterial;
                _fx.Add(f);
            }
            if (sprite == null) sprite = (kind == FxKind.Ring || kind == FxKind.RingIn ? RallyRing()
                             : kind == FxKind.Dial ? DialSprite()
                             : kind == FxKind.Beam ? BeamSprite() : UISprites.SoftGlow());
            f.sr.sprite = sprite;
            // On-board pieces (rings, dials) lie under the pieces and bend with the board;
            // the rest float over everything.
            f.onBoard = onBoard;
            if (onBoard) BendWithBoard(f.sr);
            else if (_fxSpriteDefault != null) f.sr.sharedMaterial = _fxSpriteDefault;
            f.sr.sortingOrder = onBoard ? 7 : kind == FxKind.Beam ? 21 : 23;
            f.kind = kind; f.a = at; f.b = to; f.c0 = c0; f.c1 = c1;
            f.t = 0f; f.delay = delay; f.life = life; f.r0 = r0; f.r1 = r1; f.spin = spin; f.alpha = alpha;
            f.followKnight = followKnight; f.active = true;
            f.sr.color = Color.clear;
            f.sr.gameObject.SetActive(false);
        }

        private SpriteRenderer FxRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingOrder = order; sr.color = Color.clear;
            go.SetActive(false);
            return sr;
        }

        // ── Procedural sprites ───────────────────────────────────────────────
        // A clock/compass dial: a rim, 12 hour ticks (the quarters longer) and an inner ring.
        private static Sprite DialSprite()
        {
            if (_dialSprite != null) return _dialSprite;
            const int n = 160;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            float c = (n - 1) * .5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (n * .5f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(r - .93f) / .035f);              // outer rim
                    a = Mathf.Max(a, .6f * Mathf.Clamp01(1f - Mathf.Abs(r - .62f) / .02f)); // inner ring
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float step = Mathf.Repeat(ang + 15f, 30f) - 15f;                       // nearest hour
                    int hour = Mathf.RoundToInt(Mathf.Repeat(ang - step, 360f) / 30f) % 12;
                    float inner = hour % 3 == 0 ? .7f : .78f;
                    float halfWidth = (hour % 3 == 0 ? 2.6f : 1.6f) / (r * n * .5f + 1f) * Mathf.Rad2Deg;
                    if (r > inner && r < .89f) a = Mathf.Max(a, Mathf.Clamp01(1f - Mathf.Abs(step) / halfWidth));
                    // A single hand pointing at twelve, so the spin reads as a clock turning.
                    if (r < .55f && Mathf.Abs(dx) < 2.2f && dy > 0f) a = Mathf.Max(a, .85f);
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            tex.SetPixels(px); tex.Apply();
            _dialSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
            return _dialSprite;
        }

        // A shaft of light: soft-edged across, brightest at the bottom, fading upward.
        // Pivot at the bottom so it stands on the knight.
        private static Sprite BeamSprite()
        {
            if (_beamSprite != null) return _beamSprite;
            const int w = 64, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float across = 1f - Mathf.Abs((x + .5f) / w * 2f - 1f);
                    float core = Mathf.Pow(across, 1.6f);
                    float up = (y + .5f) / h;
                    float a = core * Mathf.Pow(1f - up, 1.3f) * Mathf.Clamp01(up / .04f + .4f);
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            _beamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, 0f), w); // 1 unit wide, 4 tall
            return _beamSprite;
        }
    }
}
