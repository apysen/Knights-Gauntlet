using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    // Hop trails for the second wave of premium finishes (Mother of Pearl, Stormcaller,
    // Runestone, Jade Dynasty, Clockwork, Heartwood), plus the capture flash that makes
    // Runestone runes and Stormcaller bolts flare. Shares the pooled TrailBit system in
    // PieceView.SkinTrails.cs. The little sprites are baked once at runtime.
    public partial class PieceView
    {
        // ── Capture flash ────────────────────────────────────────────────────

        private float _skinFlash;
        private const float SkinFlashTime = .6f;

        private void TriggerSkinFlash()
        {
            if (_skinFx != SkinFx.Runestone && _skinFx != SkinFx.Storm) return;
            _skinFlash = 1f;
            PushSkinFlash();
        }

        private void UpdateSkinFlash(float dt)
        {
            if (_skinFlash <= 0f) return;
            _skinFlash = Mathf.Max(0f, _skinFlash - dt / SkinFlashTime);
            PushSkinFlash();
        }

        private void PushSkinFlash()
        {
            if (_modelRenderers == null || _accentProperties == null) return;
            _accentProperties.SetFloat("_SkinFlash", _skinFlash * _skinFlash);
            foreach (var r in _modelRenderers) if (r != null) r.SetPropertyBlock(_accentProperties);
        }

        // ── Spawners ─────────────────────────────────────────────────────────

        private void SpawnBubbles(Vector3 fromOffset)
        {
            for (int k = 0; k < 14; k++)
            {
                var bit = GetTrailBit(TrailBitKind.Bubble);
                if (bit == null) return;
                Vector2 j = Random.insideUnitCircle * .12f;
                bit.tr.localPosition = fromOffset * Random.value + new Vector3(j.x, j.y, TrailFloor - Random.Range(.05f, .3f));
                Vector2 d = Random.insideUnitCircle * .15f;
                bit.vel = new Vector3(d.x, d.y + .12f, -Random.Range(.35f, .7f));
                bit.tr.localRotation = Quaternion.identity;
                bit.maxLife = bit.life = Random.Range(.9f, 1.4f);
                bit.size = Random.Range(.1f, .18f);
                bit.seed = Random.value;
                Activate(bit);
            }
        }

        private void SpawnLightning(Vector3 fromOffset)
        {
            // Two jagged bolts from the start square to the piece: a white-hot core and a
            // bluer one beside it. Each segment is a stretched soft bar.
            const int segs = 7;
            float z = TrailFloor - .28f;
            Vector3 side = Vector3.Cross(fromOffset, Vector3.forward).normalized;
            for (int arc = 0; arc < 2; arc++)
            {
                Vector3 prev = fromOffset + new Vector3(0f, 0f, z);
                for (int i = 1; i <= segs; i++)
                {
                    float t = i / (float)segs;
                    Vector3 next = fromOffset * (1f - t) + new Vector3(0f, 0f, z);
                    if (i < segs) next += side * Random.Range(-.17f, .17f) + Vector3.forward * Random.Range(-.08f, .08f);
                    var bit = GetTrailBit(TrailBitKind.Bolt);
                    if (bit == null) return;
                    Vector3 d = next - prev;
                    bit.tr.localPosition = (prev + next) * .5f;
                    bit.tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    bit.size = new Vector2(d.x, d.y).magnitude;
                    bit.spin = arc == 0 ? .11f : .075f; // bar width
                    bit.maxLife = bit.life = .36f + arc * .08f;
                    bit.color = arc == 0 ? new Color(.88f, .96f, 1f) : new Color(.45f, .75f, 1f);
                    Activate(bit);
                    prev = next;
                }
            }
            SpawnSparks(fromOffset, 5, new Color(.6f, .85f, 1f));
            SpawnSparks(12, new Color(.6f, .85f, 1f));
        }

        // Sparks thrown off where the piece lands.
        private void SpawnSparks(int count, Color color) => SpawnSparks(Vector3.zero, count, color);
        private void SpawnSparks(Vector3 at, int count, Color color)
        {
            for (int k = 0; k < count; k++)
            {
                var bit = GetTrailBit(TrailBitKind.Spark);
                if (bit == null) return;
                bit.tr.localPosition = at + new Vector3(0f, 0f, TrailFloor - Random.Range(.1f, .35f));
                Vector2 kick = Random.insideUnitCircle.normalized * Random.Range(1.2f, 2.4f);
                bit.vel = new Vector3(kick.x, kick.y, -Random.Range(.2f, .8f));
                bit.tr.localRotation = Quaternion.identity;
                bit.maxLife = bit.life = Random.Range(.35f, .55f);
                bit.size = Random.Range(.07f, .11f);
                bit.color = color;
                Activate(bit);
            }
        }

        private void SpawnRunes(Vector3 fromOffset)
        {
            const int count = 8;
            for (int k = 0; k < count; k++)
            {
                var bit = GetTrailBit(TrailBitKind.Rune);
                if (bit == null) return;
                bit.sprite.sprite = TrailSprite(TrailBitKind.Rune, Random.Range(0, RuneVariants));
                Vector2 j = Random.insideUnitCircle * .1f;
                bit.tr.localPosition = fromOffset * ((k + .5f) / count) + new Vector3(j.x, j.y, TrailFloor - Random.Range(.1f, .3f));
                bit.vel = new Vector3(Random.Range(-.08f, .08f), Random.Range(.1f, .2f), -Random.Range(.25f, .45f));
                bit.tr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-15f, 15f));
                bit.maxLife = bit.life = Random.Range(1.1f, 1.5f);
                bit.size = Random.Range(.18f, .24f);
                bit.seed = Random.Range(0f, 100f);
                Activate(bit);
            }
        }

        private static readonly Color[] PetalColors =
        {
            new Color(1f, .82f, .9f), new Color(1f, .93f, .95f), new Color(.98f, .7f, .82f),
        };
        private static readonly Color[] LeafColors =
        {
            new Color(.5f, .85f, .25f), new Color(.75f, .85f, .2f), new Color(1f, .55f, .15f),
            new Color(1f, .8f, .25f), new Color(.9f, .3f, .12f),
        };

        // Heartwood: a gust of leaves. Some are kicked up off the path by the jump and
        // tumble back down; the rest rain from above the start square.
        private void SpawnLeaves(Vector3 fromOffset)
        {
            for (int k = 0; k < 16; k++)
            {
                var bit = GetTrailBit(TrailBitKind.Leaf);
                if (bit == null) return;
                bool kicked = k < 9;
                Vector2 j = Random.insideUnitCircle * .18f;
                float along = kicked ? Random.value : Random.Range(.5f, 1.1f);
                float height = kicked ? Random.Range(.05f, .25f) : Random.Range(.6f, 1.05f);
                bit.tr.localPosition = fromOffset * along + new Vector3(j.x, j.y, TrailFloor - height);
                Vector2 kick = Random.insideUnitCircle.normalized * (kicked ? Random.Range(.6f, 1.2f) : Random.Range(.05f, .2f));
                bit.terminal = Random.Range(.5f, .75f);
                bit.vel = new Vector3(kick.x, kick.y, kicked ? -Random.Range(1.6f, 2.4f) : bit.terminal);
                bit.tr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                bit.spin = Random.Range(-320f, 320f);
                bit.maxLife = bit.life = Random.Range(1.2f, 1.6f);
                bit.size = Random.Range(.17f, .23f);
                bit.seed = Random.Range(0f, 100f);
                bit.color = LeafColors[Random.Range(0, LeafColors.Length)];
                Activate(bit);
            }
        }

        // Petals or leaves released above the path that flutter down and settle.
        private void SpawnFalling(Vector3 fromOffset, TrailBitKind kind, int count)
        {
            bool leaf = kind == TrailBitKind.Leaf;
            for (int k = 0; k < count; k++)
            {
                var bit = GetTrailBit(kind);
                if (bit == null) return;
                Vector2 j = Random.insideUnitCircle * .15f;
                bit.tr.localPosition = fromOffset * Random.value + new Vector3(j.x, j.y, TrailFloor - Random.Range(.55f, .95f));
                bit.vel = new Vector3(Random.Range(-.15f, .15f), Random.Range(-.05f, .15f), Random.Range(.45f, .7f));
                bit.terminal = bit.vel.z;
                bit.tr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                bit.spin = Random.Range(-200f, 200f);
                bit.maxLife = bit.life = Random.Range(1.3f, 1.8f);
                bit.size = leaf ? Random.Range(.15f, .21f) : Random.Range(.11f, .15f);
                bit.seed = Random.Range(0f, 100f);
                var palette = leaf ? LeafColors : PetalColors;
                bit.color = palette[Random.Range(0, palette.Length)];
                Activate(bit);
            }
        }

        private static readonly Color[] BrassColors =
        {
            new Color(.95f, .72f, .35f), new Color(.82f, .56f, .25f), new Color(.75f, .45f, .25f),
        };

        private void SpawnCogs(Vector3 fromOffset)
        {
            const int count = 10;
            for (int k = 0; k < count; k++)
            {
                var bit = GetTrailBit(TrailBitKind.Cog);
                if (bit == null) return;
                Vector2 j = Random.insideUnitCircle * .08f;
                bit.tr.localPosition = fromOffset * ((k + .5f) / count) + new Vector3(j.x, j.y, TrailFloor - Random.Range(.15f, .3f));
                Vector2 kick = Random.insideUnitCircle * 1.3f;
                bit.vel = new Vector3(kick.x, kick.y, -Random.Range(2f, 2.8f));
                bit.tr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                bit.spin = (Random.value < .5f ? -1f : 1f) * Random.Range(480f, 900f);
                bit.maxLife = bit.life = Random.Range(.8f, 1.05f);
                // A mix of big drive gears and little pinions.
                bit.size = k % 3 == 0 ? Random.Range(.2f, .25f) : Random.Range(.11f, .15f);
                bit.color = BrassColors[Random.Range(0, BrassColors.Length)];
                Activate(bit);
            }
            // Sparks grind out along the path and burst where the piece lands.
            SpawnSparks(fromOffset, 5, new Color(1f, .8f, .4f));
            SpawnSparks(fromOffset * .5f, 4, new Color(1f, .72f, .32f));
            SpawnSparks(10, new Color(1f, .72f, .32f));
        }

        // ── Per-frame motion for the kinds above; false for the older kinds ───

        private bool UpdateNewTrailBit(TrailBit bit, float k, float dt, float floor)
        {
            var p = bit.tr.localPosition;
            float age = bit.maxLife - bit.life;
            switch (bit.kind)
            {
                case TrailBitKind.Bubble:
                {
                    bit.vel *= 1f - .8f * dt;
                    p += bit.vel * dt;
                    p.x += Mathf.Sin(age * 7f + bit.seed * 20f) * .08f * dt;
                    bit.tr.localPosition = p;
                    float pop = Mathf.Clamp01((.12f - k) / .12f); // swells and vanishes at the end
                    var c = Color.HSVToRGB(Mathf.Repeat(bit.seed + age * .6f, 1f), .35f, 1f);
                    c.a = Mathf.Clamp01(age / .1f) * (1f - pop);
                    bit.sprite.color = c;
                    float s = bit.size * (1f + pop * .5f);
                    bit.tr.localScale = new Vector3(s, s, 1f);
                    return true;
                }
                case TrailBitKind.Bolt:
                {
                    var c = bit.color;
                    c.a = Mathf.Clamp01(k / .4f) * (Random.value < .25f ? .3f : 1f); // crackle
                    bit.sprite.color = c;
                    bit.tr.localScale = new Vector3(bit.size * 1.12f, bit.spin * (.6f + .4f * k), 1f);
                    return true;
                }
                case TrailBitKind.Spark:
                {
                    bit.vel *= Mathf.Max(0f, 1f - 4f * dt);
                    bit.vel.z += 3f * dt;
                    bit.tr.localPosition = p + bit.vel * dt;
                    var c = bit.color; c.a = k;
                    bit.sprite.color = c;
                    float s = bit.size * (.5f + .5f * k);
                    bit.tr.localScale = new Vector3(s, s, 1f);
                    return true;
                }
                case TrailBitKind.Rune:
                {
                    bit.vel *= 1f - dt;
                    bit.tr.localPosition = p + bit.vel * dt;
                    float glow = 1f + .15f * Mathf.Sin(age * 18f + bit.seed);
                    var c = new Color(.35f * glow, .95f, 1f, Mathf.Clamp01(age / .15f) * Mathf.Clamp01(k / .4f));
                    bit.sprite.color = c;
                    bit.tr.localScale = new Vector3(bit.size, bit.size, 1f);
                    return true;
                }
                case TrailBitKind.Petal:
                case TrailBitKind.Leaf:
                {
                    if (p.z < floor)
                    {
                        // Leaves kicked up by the jump slow, then settle into a gentle fall.
                        bit.vel.z = Mathf.Min(bit.terminal, bit.vel.z + 4f * dt);
                        bit.vel.x *= 1f - .6f * dt; bit.vel.y *= 1f - .6f * dt;
                        // Drifting down with a lazy side to side flutter.
                        p.z = Mathf.Min(floor, p.z + bit.vel.z * (.6f + .4f * Mathf.Sin(age * 6f + bit.seed)) * dt);
                        p.x += (bit.vel.x + Mathf.Sin(age * 5f + bit.seed) * .25f) * dt;
                        p.y += (bit.vel.y + Mathf.Cos(age * 4f + bit.seed) * .12f) * dt;
                        bit.tr.localRotation = Quaternion.Euler(0f, 0f, bit.spin * dt) * bit.tr.localRotation;
                    }
                    bit.tr.localPosition = p;
                    // Tumbling: the sprite narrows as it turns edge-on, until it lands flat.
                    float flip = p.z < floor ? .55f + .45f * Mathf.Abs(Mathf.Cos(age * 6f + bit.seed)) : 1f;
                    var c = bit.color; c.a = Mathf.Clamp01(age / .1f) * Mathf.Clamp01(k / .3f);
                    bit.sprite.color = c;
                    bit.tr.localScale = new Vector3(bit.size * flip, bit.size, 1f);
                    return true;
                }
                case TrailBitKind.Cog:
                {
                    bit.vel.z += TrailGravity * dt;
                    p += bit.vel * dt;
                    if (p.z > floor && bit.vel.z > 0f)
                    {
                        p.z = floor;
                        bit.vel.z *= -.35f;
                        bit.vel.x *= .55f; bit.vel.y *= .55f;
                        bit.spin *= .6f;
                    }
                    bit.tr.localPosition = p;
                    bit.tr.localRotation = Quaternion.Euler(0f, 0f, bit.spin * dt) * bit.tr.localRotation;
                    // A brassy glint flickers as the teeth catch the light.
                    float glint = 1f + .35f * Mathf.Max(0f, Mathf.Sin(age * 22f + bit.size * 90f));
                    var c = bit.color * glint; c.a = Mathf.Clamp01(k / .3f);
                    bit.sprite.color = c;
                    bit.tr.localScale = new Vector3(bit.size, bit.size, 1f);
                    return true;
                }
            }
            return false;
        }

        // ── Baked sprites (shared with the Customization auras, see SkinTrailSprites) ──

        private const int RuneVariants = SkinTrailSprites.RuneVariants;

        private static Sprite TrailSprite(TrailBitKind kind, int variant = 0)
        {
            switch (kind)
            {
                case TrailBitKind.Bubble: return SkinTrailSprites.Bubble();
                case TrailBitKind.Bolt: return SkinTrailSprites.Bolt();
                case TrailBitKind.Spark: return SkinTrailSprites.Spark();
                case TrailBitKind.Rune: return SkinTrailSprites.Rune(variant);
                case TrailBitKind.Petal: return SkinTrailSprites.Petal();
                case TrailBitKind.Leaf: return SkinTrailSprites.Leaf();
                case TrailBitKind.Cog: return SkinTrailSprites.Cog();
                default: return BoardView.UnitSquare(); // embers, stardust, streaks
            }
        }
    }
}
