using UnityEngine;

namespace KnightsGauntlet.View
{
    // Generates simple UI sprites at runtime so the look (rounded buttons, gold
    // outlines, dots) needs no art files. All cached after first use.
    public static class UISprites
    {
        private static Sprite _fill, _border, _pixelBorder, _squareBorder, _circle, _glow, _softGlow, _star, _refresh, _cracked, _lock;

        // Padlock (locked adventure levels/worlds): a rounded body plus a shackle
        // arc on top. Tint via Image color.
        public static Sprite Lock()
        {
            if (_lock != null) return _lock;
            const int n = 64;
            var tex = NewTex(n);
            float cx = (n - 1) * 0.5f;
            // Body: rounded rectangle in the lower half.
            float bx0 = n * 0.24f, bx1 = n * 0.76f, by0 = n * 0.10f, by1 = n * 0.56f;
            float corner = n * 0.08f;
            // Shackle: an annulus arc (upper half only) sitting above the body.
            float sy = by1, R = n * 0.16f, thick = n * 0.055f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool on = false;
                    // rounded-rect body
                    float dx = Mathf.Max(bx0 + corner - x, x - (bx1 - corner), 0f);
                    float dy = Mathf.Max(by0 + corner - y, y - (by1 - corner), 0f);
                    if (Mathf.Sqrt(dx * dx + dy * dy) <= corner) on = true;
                    // shackle (upper semicircle of an annulus)
                    if (!on)
                    {
                        float ox = x - cx, oy = y - sy;
                        float d = Mathf.Sqrt(ox * ox + oy * oy);
                        if (oy >= 0f && Mathf.Abs(d - R) <= thick) on = true;
                    }
                    tex.SetPixel(x, y, on ? Color.white : new Color(0, 0, 0, 0));
                }
            // Keyhole: punch a small hole in the body.
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float ox = x - cx, oy = y - n * 0.34f;
                    if (Mathf.Sqrt(ox * ox + oy * oy) <= n * 0.055f) tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            tex.Apply();
            _lock = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _lock;
        }

        // Cracked/fragile tile placeholder: a rounded square with dark crack lines.
        public static Sprite Cracked()
        {
            if (_cracked != null) return _cracked;
            _cracked = Resources.Load<Sprite>("UI/Cracked Tile"); // pixel-art cracked tile
            if (_cracked != null) return _cracked;
            const int n = 48;
            float c = (n - 1) * 0.5f, rad = n * 0.44f, corner = n * 0.16f;
            var tex = NewTex(n);
            var baseCol = new Color(0.62f, 0.50f, 0.34f, 0.85f); // tan
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // rounded-square mask
                    float dx = Mathf.Abs(x - c) - (rad - corner), dy = Mathf.Abs(y - c) - (rad - corner);
                    float d = Mathf.Sqrt(Mathf.Max(dx, 0) * Mathf.Max(dx, 0) + Mathf.Max(dy, 0) * Mathf.Max(dy, 0)) + Mathf.Min(Mathf.Max(dx, dy), 0) - corner;
                    tex.SetPixel(x, y, d < 0 ? baseCol : new Color(0, 0, 0, 0));
                }
            var crack = new Color(0.15f, 0.11f, 0.07f, 0.9f);
            void Line(float x0, float y0, float x1, float y1)
            {
                int steps = 40;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    int px = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), py = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int xx = px + ox, yy = py + oy;
                            if (xx >= 0 && xx < n && yy >= 0 && yy < n && tex.GetPixel(xx, yy).a > 0.5f)
                                tex.SetPixel(xx, yy, crack);
                        }
                }
            }
            Line(c, c, 8, 6); Line(c, c, 40, 10); Line(c, c, 12, 42); Line(c, c, 38, 40); Line(c, c, c, 44);
            tex.Apply();
            _cracked = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n); // 1 world unit
            return _cracked;
        }

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
            _refresh = Resources.Load<Sprite>("UI/Retry"); // pixel-art retry/reload icon
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

        // Thin STRAIGHT-edged square outline (no rounded corners), matching the web's
        // threat-tile stroke. 9-sliced so the frame stays a constant thickness.
        public static Sprite SquareBorder()
        {
            if (_squareBorder != null) return _squareBorder;
            const int n = 64, thickness = 1; // thin straight edge (~1.5% of the cell)
            var tex = NewTex(n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool edge = x < thickness || x >= n - thickness || y < thickness || y >= n - thickness;
                    tex.SetPixel(x, y, edge ? Color.white : new Color(0, 0, 0, 0));
                }
            tex.Apply();
            // ppu = n → the sprite is 1 world unit, so scaled by a full cell the frame
            // hugs the cell edge (rather than shrinking to a ring inside it).
            _squareBorder = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n,
                0, SpriteMeshType.FullRect, new Vector4(thickness, thickness, thickness, thickness));
            return _squareBorder;
        }

        // Radial shine that FILLS the square and is hard-clipped by the cell edge (the
        // web look): brightest at the centre, still lit at the edges, cut off outside.
        // The falloff stays positive at the edge-centres (u=1) so scaling to exactly one
        // cell yields a square-cropped gradient rather than a soft inset circle.
        public static Sprite Glow()
        {
            if (_glow != null) return _glow;
            const int n = 64;
            float c = (n - 1) * 0.5f, half = n * 0.5f;
            var tex = NewTex(n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float u = d / half; // 0 centre, 1 edge-centre, ~1.41 corner
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - 0.55f * u)));
                }
            tex.Apply();
            _glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n); // 1 world unit
            return _glow;
        }

        // A soft radial halo whose alpha falls off aggressively (cubic) to exactly zero by
        // the sprite edge, so no rectangular boundary is ever visible. Brightest at the
        // centre, gone well before the corners. Used behind the near-black Nightfall knight.
        public static Sprite SoftGlow()
        {
            if (_softGlow != null) return _softGlow;
            const int n = 64;
            float c = (n - 1) * 0.5f, half = n * 0.5f;
            var tex = NewTex(n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float u = d / half; // 0 centre, 1 at edge midpoints, ~1.41 corner
                    float a = Mathf.Pow(Mathf.Clamp01(1f - u), 3f); // aggressive fade, 0 by the edge
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            _softGlow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _softGlow;
        }

        // Gold selection outline that matches the DEVNIK pixel buttons' baked black
        // border EXACTLY. Measured from BAR1: an 8px-thick border with a 2-step (4px)
        // chamfered corner (NOT a smooth radius), 9-sliced at 16px. Built at the same
        // pixel dimensions and drawn POINT-filtered at the same 2x pixels-per-unit as
        // the button background, so it lines up with, and reads as pixel art like, that
        // black border.
        public static Sprite PixelBorder()
        {
            if (_pixelBorder != null) return _pixelBorder;
            const int n = 34, border = 16, thickness = 8;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool outer = InChamferRect(x, y, 0, 0, n, n);
                    bool inner = InChamferRect(x, y, thickness, thickness, n - thickness, n - thickness);
                    tex.SetPixel(x, y, (outer && !inner) ? Color.white : new Color(0, 0, 0, 0));
                }
            tex.Apply();
            _pixelBorder = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            return _pixelBorder;
        }

        // Membership in a rectangle [x0,x1) x [y0,y1) whose corners are cut by a 2-step
        // pixel chamfer (4px then 8px), matching the DEVNIK button corner.
        private static bool InChamferRect(int x, int y, int x0, int y0, int x1, int y1)
        {
            if (x < x0 || x >= x1 || y < y0 || y >= y1) return false;
            int dx = Mathf.Min(x - x0, x1 - 1 - x);
            int dy = Mathf.Min(y - y0, y1 - 1 - y);
            return !(dx < 8 && dy < 8 && (dx < 4 || dy < 4));
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

        // (Ring sprite removed; colorblind threat markers use the rounded border.)

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
