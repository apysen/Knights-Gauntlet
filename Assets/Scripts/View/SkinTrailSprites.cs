using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    // Small procedural sprites for the premium-skin trails, baked once at runtime and
    // shared by the in-game hop trails (PieceView) and the Customization auras (SkinAura).
    public static class SkinTrailSprites
    {
        public const int RuneVariants = 4;
        // Domain reload is off in the editor, so a cached sprite can outlive its texture
        // between Play sessions: entries are checked before reuse.
        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

        public static Sprite Bubble() => Get(0, BubbleShape);
        public static Sprite Bolt() => Get(1, BoltShape);
        public static Sprite Spark() => Get(2, SparkShape);
        public static Sprite Petal() => Get(3, PetalShape);
        public static Sprite Leaf() => Get(4, LeafShape);
        public static Sprite Cog() => Get(5, CogShape);
        public static Sprite Coin() => Get(6, CoinShape);
        public static Sprite Shard() => Get(7, ShardShape);
        public static Sprite Rune(int variant)
        {
            int v = Mathf.Clamp(variant, 0, RuneVariants - 1);
            return Get(16 + v, (x, y) => RuneShape(x, y, v));
        }

        private static Sprite Get(int key, System.Func<float, float, Color> shape)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null && cached.texture != null) return cached;
            var sprite = Bake(shape);
            Cache[key] = sprite;
            return sprite;
        }

        // Shape functions take x, y in [-1, 1] and return a white-ish colour with alpha;
        // the trail tints them through SpriteRenderer.color.
        internal static Sprite Bake(System.Func<float, float, Color> shape)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SkinTrailSprite" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = shape((x + .5f) / n * 2f - 1f, (y + .5f) / n * 2f - 1f);
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
        }

        private static float Step(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static Color BubbleShape(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = (1f - Step(.88f, .96f, r)) * Mathf.Lerp(.15f, 1f, Step(.62f, .9f, r));
            float hx = x + .38f, hy = y - .38f;
            float highlight = 1f - Step(.08f, .22f, Mathf.Sqrt(hx * hx + hy * hy));
            return new Color(1f, 1f, 1f, Mathf.Max(a, highlight));
        }

        private static Color BoltShape(float x, float y)
        {
            float across = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y)), 1.6f);
            float ends = 1f - Step(.85f, 1f, Mathf.Abs(x));
            float core = Mathf.Lerp(.75f, 1f, across);
            return new Color(core, core, 1f, across * ends);
        }

        private static Color SparkShape(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, a * a);
        }

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // Four runic glyphs: a stave with branches.
        private static readonly float[][] RuneStrokes =
        {
            new[] { -.25f, -.8f, -.25f, .8f,   -.25f, .75f, .4f, .35f,   -.25f, .25f, .4f, -.15f },
            new[] { 0f, -.8f, 0f, .8f,         0f, .8f, -.45f, .35f,     0f, .8f, .45f, .35f },
            new[] { -.3f, -.8f, -.3f, .8f,     -.3f, .45f, .3f, 0f,      .3f, 0f, -.3f, -.45f },
            new[] { 0f, -.8f, 0f, .8f,         0f, .2f, -.45f, .75f,     0f, .2f, .45f, .75f },
        };

        private static Color RuneShape(float x, float y, int variant)
        {
            var s = RuneStrokes[Mathf.Clamp(variant, 0, RuneStrokes.Length - 1)];
            float d = 8f;
            for (int i = 0; i + 3 < s.Length; i += 4) d = Mathf.Min(d, Segment(x, y, s[i], s[i + 1], s[i + 2], s[i + 3]));
            float line = 1f - Step(.08f, .14f, d);
            float glow = (1f - Step(.08f, .45f, d)) * .4f;
            return new Color(1f, 1f, 1f, Mathf.Max(line, glow));
        }

        private static Color PetalShape(float x, float y)
        {
            // A rounded petal with a pointed base and a small notch at the tip.
            float v = (y - .1f) / .85f;
            float width = .62f * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)) * Mathf.Sqrt(Step(-1.05f, .15f, y));
            float body = 1f - Step(width - .05f, width + .02f, Mathf.Abs(x));
            float nx = x, ny = y - .98f;
            float notch = Step(.16f, .22f, Mathf.Sqrt(nx * nx + ny * ny));
            float shade = Mathf.Lerp(.82f, 1f, (y + 1f) * .5f);
            return new Color(shade, shade, shade, body * notch);
        }

        private static Color LeafShape(float x, float y)
        {
            // A pointed lens (two overlapping circles) with a short stem and a darker midrib.
            float l = Mathf.Sqrt((x - .75f) * (x - .75f) + y * y);
            float r = Mathf.Sqrt((x + .75f) * (x + .75f) + y * y);
            float body = (1f - Step(1.0f, 1.06f, l)) * (1f - Step(1.0f, 1.06f, r));
            float stem = 1f - Step(.04f, .08f, Segment(x, y, 0f, -.6f, 0f, -.95f));
            float shade = Mathf.Abs(x) < .045f && y > -.7f ? .7f : Mathf.Lerp(.85f, 1f, (y + 1f) * .5f);
            return new Color(shade, shade, shade, Mathf.Max(body, stem));
        }

        private static Color CogShape(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float outer = .76f + .16f * Step(-.2f, .2f, Mathf.Cos(a * 8f));
            float body = (1f - Step(outer - .04f, outer + .02f, r)) * Step(.26f, .32f, r);
            float shade = Mathf.Lerp(.75f, 1f, Step(.3f, .7f, r));
            return new Color(shade, shade, shade, body);
        }

        // A gold coin: rim, face and a little embossed crown-ish bar.
        private static Color CoinShape(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = 1f - Step(.9f, .97f, r);
            float rim = Step(.68f, .76f, r);
            float shade = Mathf.Lerp(.85f, 1f, rim) * Mathf.Lerp(.9f, 1.05f, (y + 1f) * .5f);
            if (Mathf.Abs(y) < .12f && Mathf.Abs(x) < .35f) shade *= .82f;
            return new Color(shade, shade, shade, a);
        }

        // A sliver of glass with a bright edge.
        private static Color ShardShape(float x, float y)
        {
            float d = Mathf.Max(Mathf.Max(-(y + .75f), (y - .9f * (1f - Mathf.Abs(x) / .55f))), Mathf.Abs(x) - .55f);
            float a = 1f - Step(-.04f, .02f, d);
            float edge = Step(-.16f, -.04f, d);
            float shade = Mathf.Lerp(.8f, 1f, edge);
            return new Color(shade, shade, shade, a * Mathf.Lerp(.85f, 1f, edge));
        }
    }
}
