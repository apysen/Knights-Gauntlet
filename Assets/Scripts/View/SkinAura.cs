using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // A looping UI preview of a premium skin's hop trail, played around its knight icon in
    // the Customization showcase so players can see the trail before they buy it.
    // Every particle runs on a fixed loop driven by unscaled time and its own seed, so
    // there is no per-frame allocation and no state to drift. Particles live in two
    // layers: one behind the knight (echoes, orbiting runes and cogs) and one in front
    // (falling leaves, coins, sparks), so the knight stays readable.
    public class SkinAura : MonoBehaviour
    {
        private sealed class Bit
        {
            public RectTransform rt;
            public Graphic g;
            public float r1, r2, r3;
            public int i;
        }

        private int _fx;
        private float _size, _alpha, _bitScale = 1f;
        private Bit[] _bits;
        private RawImage _knight;   // echoes copy the knight's rendered texture
        private RectTransform _front;

        private static readonly Color[] Jewels =
        {
            new Color(.95f, .2f, .3f), new Color(.25f, .5f, 1f), new Color(.15f, .85f, .45f),
            new Color(1f, .75f, .2f), new Color(.72f, .32f, 1f),
        };
        private static readonly Color[] Stars =
        {
            Color.white, new Color(.7f, .85f, 1f), new Color(.82f, .62f, 1f), new Color(1f, .75f, .92f),
        };
        private static readonly Color[] Petals = { new Color(1f, .8f, .9f), new Color(1f, .94f, .96f), new Color(.98f, .68f, .82f) };
        private static readonly Color[] Leaves =
        {
            new Color(.5f, .85f, .25f), new Color(.75f, .85f, .2f), new Color(1f, .55f, .15f), new Color(1f, .8f, .25f), new Color(.9f, .3f, .12f),
        };
        private static readonly Color[] Brass = { new Color(.95f, .72f, .35f), new Color(.82f, .56f, .25f), new Color(.72f, .45f, .25f) };

        // Adds an aura around `knight` (a child of `parent`) for this skin. Plain colour
        // skins have no trail and get nothing. Returns null when there is no aura.
        // `size` sets how far the aura spreads; `bitScale` shrinks the particles within it.
        public static SkinAura Attach(Transform parent, RawImage knight, string skinId, float size, float alpha, float bitScale = 1f)
        {
            int fx = Skins.FxOf(skinId);
            if (fx == SkinFx.None || SaveData.IsReducedEffects()) return null;
            var back = Layer(parent, "AuraBack", size);
            if (knight != null) back.SetSiblingIndex(knight.transform.GetSiblingIndex());
            var front = Layer(parent, "AuraFront", size);
            var aura = back.gameObject.AddComponent<SkinAura>();
            aura._bitScale = bitScale;
            aura.Build(fx, size, alpha, knight, front);
            return aura;
        }

        private static RectTransform Layer(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        // Fades the whole aura: every bit's colour is multiplied by this. The menu
        // knight's tap burst drives it in and out; the showcase leaves it at 1.
        public float Alpha { get => _alpha; set => _alpha = value; }

        private void OnDestroy()
        {
            if (_front != null) Destroy(_front.gameObject);
        }

        private void Build(int fx, float size, float alpha, RawImage knight, RectTransform front)
        {
            _fx = fx; _size = size; _alpha = alpha; _knight = knight; _front = front;
            int count; Sprite sprite = null; bool echo = false;
            switch (fx)
            {
                case SkinFx.Phantom: case SkinFx.Nightfall: count = 3; echo = true; break;
                case SkinFx.Gilded: count = 7; sprite = SkinTrailSprites.Coin(); break;
                case SkinFx.StainedGlass: count = 8; sprite = SkinTrailSprites.Shard(); break;
                case SkinFx.Queensbane: count = 15; sprite = SkinTrailSprites.Spark(); break;
                case SkinFx.Starforged: count = 13; sprite = SkinTrailSprites.Spark(); break;
                case SkinFx.Pearl: count = 9; sprite = SkinTrailSprites.Bubble(); break;
                case SkinFx.Storm: count = 14; sprite = SkinTrailSprites.Bolt(); break;
                case SkinFx.Runestone: count = 6; break;
                case SkinFx.Jade: count = 8; sprite = SkinTrailSprites.Petal(); break;
                case SkinFx.Clockwork: count = 9; sprite = SkinTrailSprites.Cog(); break;
                case SkinFx.Heartwood: count = 8; sprite = SkinTrailSprites.Leaf(); break;
                default: count = 0; break;
            }
            _bits = new Bit[count];
            for (int i = 0; i < count; i++)
            {
                bool inFront = InFront(fx, i);
                var go = new GameObject("Bit", typeof(RectTransform));
                go.transform.SetParent(inFront ? front : (RectTransform)transform, false);
                Graphic g;
                if (echo) { var raw = go.AddComponent<RawImage>(); g = raw; }
                else
                {
                    var img = go.AddComponent<Image>();
                    img.sprite = fx == SkinFx.Runestone ? SkinTrailSprites.Rune(i % SkinTrailSprites.RuneVariants)
                               : fx == SkinFx.Starforged && i == count - 1 ? SkinTrailSprites.Bolt()
                               : fx == SkinFx.Storm && i >= 10 ? SkinTrailSprites.Spark()
                               : fx == SkinFx.Clockwork && i >= 3 ? SkinTrailSprites.Spark()
                               : sprite;
                    g = img;
                }
                g.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                _bits[i] = new Bit { rt = rt, g = g, i = i, r1 = Hash(i * 3.1f + fx), r2 = Hash(i * 7.7f + fx * 2.3f), r3 = Hash(i * 13.3f + fx * 5.1f) };
            }
            Update();
        }

        private static bool InFront(int fx, int i)
        {
            switch (fx)
            {
                case SkinFx.Phantom: case SkinFx.Nightfall: case SkinFx.Runestone: return false;
                case SkinFx.Clockwork: return i >= 3;      // cogs behind, sparks in front
                case SkinFx.Storm: return i >= 10 || i % 2 == 0;
                default: return i % 2 == 0;
            }
        }

        private static float Hash(float x)
        {
            float h = Mathf.Sin(x * 12.9898f + 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private void Set(Bit b, float x, float y, float w, float h, float rot, Color c)
        {
            b.rt.anchoredPosition = new Vector2(x * _size, y * _size);
            // Echoes are copies of the knight and keep its size; everything else scales.
            float k = b.g is RawImage ? _size : _size * _bitScale;
            b.rt.sizeDelta = new Vector2(w * k, h * k);
            b.rt.localRotation = Quaternion.Euler(0f, 0f, rot);
            c.a *= _alpha;
            b.g.color = c;
        }

        private void Update()
        {
            if (_bits == null) return;
            float t = Time.unscaledTime;
            foreach (var b in _bits)
            {
                switch (_fx)
                {
                    case SkinFx.Phantom: case SkinFx.Nightfall: Echo(b, t); break;
                    case SkinFx.Gilded: Coin(b, t); break;
                    case SkinFx.StainedGlass: Shard(b, t); break;
                    case SkinFx.Queensbane: Ember(b, t); break;
                    case SkinFx.Starforged: Star(b, t); break;
                    case SkinFx.Pearl: Bubble(b, t); break;
                    case SkinFx.Storm: Storm(b, t); break;
                    case SkinFx.Runestone: Rune(b, t); break;
                    case SkinFx.Jade: Falling(b, t, Petals, .13f, 2.6f, .07f); break;
                    case SkinFx.Clockwork: Clockwork(b, t); break;
                    case SkinFx.Heartwood: Falling(b, t, Leaves, .18f, 2.2f, .14f); break;
                }
            }
        }

        // Afterimages of the knight sliding off to one side, like a hop just happened.
        private void Echo(Bit b, float t)
        {
            var raw = (RawImage)b.g;
            if (_knight != null) { raw.texture = _knight.texture; raw.uvRect = _knight.uvRect; }
            float u = Mathf.Repeat(t / 1.5f + b.i / 3f, 1f);
            float k = _knight != null ? _knight.rectTransform.rect.width / _size : .9f;
            bool night = _fx == SkinFx.Nightfall;
            float a = (1f - u) * (night ? .7f : .55f);
            if (_knight != null) a *= Mathf.Max(_knight.color.a, .5f); // locked knights are faded
            var c = night ? new Color(1f, .55f, .55f, a) : new Color(.75f, 1f, 1f, a);
            Set(b, -u * .34f, u * .04f, k, k, 0f, c);
        }

        // Spinning coins popping up from the base and falling back.
        private void Coin(Bit b, float t)
        {
            float u = Mathf.Repeat(t / 1.3f + b.r1, 1f);
            float x0 = (b.r2 - .5f) * .55f;
            float x = x0 + (b.r3 - .5f) * .45f * u;
            float y = -.4f + .62f * 4f * u * (1f - u);
            float spin = Mathf.Abs(Mathf.Cos(t * 7f + b.r1 * 10f));
            float a = Mathf.Clamp01((1f - u) / .2f) * Mathf.Clamp01(u / .08f);
            Set(b, x, y, .17f * Mathf.Max(.15f, spin), .17f, 0f, new Color(1f, .82f, .3f, a));
        }

        // Jewel shards circling the knight and tumbling.
        private void Shard(Bit b, float t)
        {
            int n = _bits.Length;
            float ang = t * .9f + b.i * Mathf.PI * 2f / n;
            float rad = .4f + .06f * Mathf.Sin(t * 1.7f + b.r1 * 6f);
            float twinkle = .7f + .3f * Mathf.Sin(t * 5f + b.r2 * 9f);
            var c = Jewels[b.i % Jewels.Length] * (1.1f + .4f * twinkle); c.a = .9f;
            float s = .13f + .05f * b.r3;
            Set(b, Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad * .85f, s, s * 1.2f, t * (120f + 160f * b.r3) * (b.i % 2 == 0 ? 1 : -1), c);
        }

        // Embers rising off the base, white-hot to deep red.
        private void Ember(Bit b, float t)
        {
            float u = Mathf.Repeat(t / 1.6f + b.r1, 1f);
            float x = (b.r2 - .5f) * .8f + Mathf.Sin(u * 6f + b.r3 * 6f) * .06f;
            float y = -.45f + u * .95f;
            var c = u < .35f ? Color.Lerp(new Color(1f, .92f, .55f), new Color(1f, .38f, .1f), u / .35f)
                             : Color.Lerp(new Color(1f, .38f, .1f), new Color(.75f, .06f, .03f), (u - .35f) / .65f);
            c.a = (1f - u) * Mathf.Clamp01(u * 6f);
            float s = .15f * (1f - u * .5f);
            Set(b, x, y, s, s, 0f, c);
        }

        // Twinkling stardust around the knight, and a shooting star now and then.
        private void Star(Bit b, float t)
        {
            if (b.i == _bits.Length - 1)
            {
                float v = Mathf.Repeat(t / 2.6f, 1f);
                if (v > .3f) { Set(b, 0f, 0f, 0f, 0f, 0f, Color.clear); return; }
                float p = v / .3f;
                Set(b, -.55f + p * 1.1f, .48f - p * .35f, .4f * (1f - p * .5f), .05f, -17f, new Color(.9f, .94f, 1f, 1f - p));
                return;
            }
            float u = Mathf.Repeat(t / 1.4f + b.r1, 1f);
            float cycle = Mathf.Floor(t / 1.4f + b.r1);
            float ang = Hash(cycle * 1.7f + b.i) * Mathf.PI * 2f;
            float rad = .3f + .2f * Hash(cycle * 3.1f + b.i * 2f);
            var c = Stars[b.i % Stars.Length]; c.a = Mathf.Min(1f, Mathf.Sin(u * Mathf.PI) * 1.4f);
            float s = .1f + .07f * b.r2;
            Set(b, Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, s, s, 45f, c);
        }

        // Iridescent bubbles drifting up and popping.
        private void Bubble(Bit b, float t)
        {
            float u = Mathf.Repeat(t / 2f + b.r1, 1f);
            float x = (b.r2 - .5f) * .85f + Mathf.Sin(u * 8f + b.r3 * 6f) * .05f;
            float y = -.42f + u * .95f;
            float pop = Mathf.Clamp01((u - .9f) / .1f);
            var c = Color.HSVToRGB(Mathf.Repeat(b.r3 + t * .25f, 1f), .35f, 1f);
            c.a = Mathf.Clamp01(u * 8f) * (1f - pop);
            float s = (.09f + .07f * b.r2) * (1f + pop * .5f);
            Set(b, x, y, s, s, 0f, c);
        }

        // Two jagged bolts that strike between points around the knight, plus sparks.
        private void Storm(Bit b, float t)
        {
            const int segs = 5;
            if (b.i >= 10)
            {
                float u = Mathf.Repeat(t / .8f + b.r1, 1f);
                float ang = b.r2 * Mathf.PI * 2f;
                float d = .3f + u * .25f;
                var sc = new Color(.6f, .85f, 1f, 1f - u);
                float s = .07f * (1f - u * .5f);
                Set(b, Mathf.Cos(ang) * d, Mathf.Sin(ang) * d, s, s, 45f, sc);
                return;
            }
            int arc = b.i / segs, seg = b.i % segs;
            float period = 1.1f;
            float cyc = Mathf.Floor(t / period + arc * .5f);
            float ph = Mathf.Repeat(t / period + arc * .5f, 1f);
            if (ph > .35f) { Set(b, 0f, 0f, 0f, 0f, 0f, Color.clear); return; }
            Vector2 P(int k)
            {
                float a0 = Hash(cyc * 2.1f + arc * 9f) * Mathf.PI * 2f;
                float a1 = a0 + Mathf.PI * (.6f + .5f * Hash(cyc * 5.3f + arc));
                float f = k / (float)segs;
                float a = Mathf.Lerp(a0, a1, f);
                float r = .44f + (k == 0 || k == segs ? 0f : (Hash(cyc * 7.7f + k * 3.3f + arc) - .5f) * .16f);
                return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            Vector2 p0 = P(seg), p1 = P(seg + 1), mid = (p0 + p1) * .5f, dv = p1 - p0;
            float flick = Hash(Mathf.Floor(t * 24f) + b.i) < .25f ? .3f : 1f;
            var c = (arc == 0 ? new Color(.88f, .96f, 1f) : new Color(.5f, .78f, 1f));
            c.a = (1f - ph / .35f) * flick;
            Set(b, mid.x, mid.y, dv.magnitude * 1.15f / _bitScale, arc == 0 ? .1f : .07f, Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg, c);
        }

        // A slow ring of glowing runes orbiting behind the knight.
        private void Rune(Bit b, float t)
        {
            int n = _bits.Length;
            float ang = t * .55f + b.i * Mathf.PI * 2f / n;
            float glow = .55f + .45f * Mathf.Pow(.5f + .5f * Mathf.Sin(t * 2f - b.i * 1.1f), 2f);
            var c = new Color(.35f + .3f * glow, .95f, 1f, .45f + .55f * glow);
            float s = .18f + .03f * glow;
            Set(b, Mathf.Cos(ang) * .44f, Mathf.Sin(ang) * .4f, s, s, 0f, c);
        }

        // Petals or leaves fluttering down past the knight.
        private void Falling(Bit b, float t, Color[] palette, float size, float period, float sway)
        {
            float u = Mathf.Repeat(t / period + b.r1, 1f);
            float age = u * period;
            float x = (b.r2 - .5f) * .9f + Mathf.Sin(age * 2.6f + b.r3 * 6f) * sway;
            float y = .5f - u * 1f;
            float flip = .5f + .5f * Mathf.Abs(Mathf.Cos(age * 3f + b.r3 * 5f));
            var c = palette[b.i % palette.Length];
            c.a = Mathf.Clamp01(u * 7f) * Mathf.Clamp01((1f - u) * 5f);
            float s = size * (.8f + .4f * b.r3);
            Set(b, x, y, s * flip, s, age * (b.r2 - .5f) * 260f + b.r1 * 360f, c);
        }

        // Three meshing cogs turning behind the knight, with sparks where they grind.
        private void Clockwork(Bit b, float t)
        {
            if (b.i < 3)
            {
                Vector2 pos = b.i == 0 ? new Vector2(-.32f, -.26f) : b.i == 1 ? new Vector2(.34f, -.2f) : new Vector2(.28f, .28f);
                float s = b.i == 0 ? .28f : b.i == 1 ? .22f : .17f;
                float rot = t * (70f / s) * .3f * (b.i == 1 ? -1f : 1f);
                Set(b, pos.x, pos.y, s, s, rot, Brass[b.i] * 1.1f);
                return;
            }
            float u = Mathf.Repeat(t / .7f + b.r1, 1f);
            Vector2 from = b.i % 2 == 0 ? new Vector2(-.2f, -.3f) : new Vector2(.2f, -.2f);
            float ang = (b.r2 * .8f + .1f) * Mathf.PI;
            Vector2 p = from + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (u * .3f) + new Vector2(0f, -u * u * .2f);
            var c = new Color(1f, .78f, .35f, 1f - u);
            float ss = .06f * (1f - u * .5f);
            Set(b, p.x, p.y, ss, ss, 45f, c);
        }
    }
}
