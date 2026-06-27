using UnityEngine;

namespace KnightsGauntlet.View
{
    // Generates simple UI sprites at runtime so the look (rounded buttons, gold
    // outlines, dots) needs no art files. All cached after first use.
    public static class UISprites
    {
        private static Sprite _fill, _border, _circle;

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
