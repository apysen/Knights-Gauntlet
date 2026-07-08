using UnityEngine;

namespace KnightsGauntlet.View
{
    // Generates simple UI sprites at runtime so the look (rounded buttons, gold
    // outlines, dots) needs no art files. All cached after first use.
    public static class UISprites
    {
        private static Sprite _fill, _border, _circle, _star, _refresh;

        // Five-pointed star (placeholder for adventure stars). Tint via Image color.
        public static Sprite Star()
        {
            if (_star != null) return _star;
            const int n = 64;
            float cx = (n - 1) * 0.5f, cy = (n - 1) * 0.5f;
            float outer = n * 0.47f, inner = outer * 0.42f;
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f; // point-up, 36 deg steps
                float rad = (i % 2 == 0) ? outer : inner;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * rad, cy + Mathf.Sin(ang) * rad);
            }
            var tex = NewTex(n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (InPoly(x + sx * 0.5f - 0.25f, y + sy * 0.5f - 0.25f, pts)) hits++;
                    tex.SetPixel(x, y, new Color(1, 1, 1, hits / 4f));
                }
            tex.Apply();
            _star = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _star;
        }

        // Circular "restart" arrow (an arc with a gap and an arrowhead).
        public static Sprite Refresh()
        {
            if (_refresh != null) return _refresh;
            const int n = 64;
            float cx = (n - 1) * 0.5f, cy = (n - 1) * 0.5f;
            float R = n * 0.32f, t = n * 0.075f;
            float gapC = 0.6f, gapW = 0.6f; // gap centered ~34 deg, half-width ~34 deg
            var tex = NewTex(n);
            // arc (annulus minus a wedge)
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);
                    float da = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, gapC * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    float ring = Mathf.Clamp01(t - Mathf.Abs(d - R) + 0.5f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, da > gapW ? ring : 0f));
                }
            // arrowhead at the counterclockwise end of the arc (angle gapC + gapW)
            float ea = gapC + gapW;
            var radial = new Vector2(Mathf.Cos(ea), Mathf.Sin(ea));
            var tangent = new Vector2(-Mathf.Sin(ea), Mathf.Cos(ea));
            var e = new Vector2(cx + radial.x * R, cy + radial.y * R);
            var tri = new[]
            {
                e + tangent * (t * 2.6f),
                e + radial * (t * 1.9f),
                e - radial * (t * 1.9f)
            };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (InPoly(x, y, tri))
                        tex.SetPixel(x, y, new Color(1, 1, 1, 1));
            tex.Apply();
            _refresh = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _refresh;
        }

        private static bool InPoly(float px, float py, Vector2[] p)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if (((p[i].y > py) != (p[j].y > py)) &&
                    (px < (p[j].x - p[i].x) * (py - p[i].y) / (p[j].y - p[i].y) + p[i].x))
                    inside = !inside;
            return inside;
        }

        public static Sprite RoundedFill()
        {
            if (_fill != null) return _fill;
            const int n = 48, r = 16;
            var tex = NewTex(n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, RoundedCoverage(x, y, n, n, r)));
            tex.Apply();
            _fill = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _fill;
        }

        public static Sprite RoundedBorder()
        {
            if (_border != null) return _border;
            const int n = 48, r = 16, thickness = 4;
            var tex = NewTex(n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float outer = RoundedCoverage(x, y, n, n, r);
                    float inner = RoundedCoverage(x, y, n, n, r, thickness);
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(outer - inner)));
                }
            tex.Apply();
            _border = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _border;
        }

        public static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int n = 32;
            float c = (n - 1) * 0.5f, rad = n * 0.5f;
            var tex = NewTex(n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(rad - d + 0.5f)));
                }
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _circle;
        }

        // (Ring sprite removed — colorblind threat markers use the rounded border.)

        // Coverage of a rounded rect, optionally inset by `pad` (for the ring hole).
        private static float RoundedCoverage(int x, int y, int w, int h, float r, float pad = 0f)
        {
            float lo = r + pad, hiX = w - 1 - r - pad, hiY = h - 1 - r - pad;
            float cx = Mathf.Clamp(x, lo, hiX);
            float cy = Mathf.Clamp(y, lo, hiY);
            float dx = x - cx, dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((r - pad) - dist + 0.5f);
        }

        private static Texture2D NewTex(int n)
        {
            return new Texture2D(n, n, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        }
    }
}
