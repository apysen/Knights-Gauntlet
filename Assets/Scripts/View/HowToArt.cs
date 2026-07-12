using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KnightsGauntlet.View
{
    // Builds the little chess-grid diagrams shown on each How-to page, mirroring
    // the SVG art from the web version. Piece/trap glyphs use the theme's symbol
    // font; labels use the regular UI font.
    public class HowToArt
    {
        private readonly UIFactory _ui;
        private readonly GameTheme _theme;

        private static readonly Color CellA = UIFactory.Hex("#1C2A4C");
        private static readonly Color CellB = UIFactory.Hex("#15203A");
        private static readonly Color Gold = UIFactory.Hex("#F0C040");
        private static readonly Color Blue = UIFactory.Hex("#4080E0");
        private static readonly Color Violet = UIFactory.Hex("#9060E0");
        private static readonly Color Pink = UIFactory.Hex("#E04080");
        private static readonly Color Green = UIFactory.Hex("#50C878");
        private static readonly Color Red = UIFactory.Hex("#CC4444");
        private static readonly Color Cap = UIFactory.Hex("#8A93AD");
        private static readonly Color GreenTxt = UIFactory.Hex("#3CDC78");
        private static readonly Color Frost = UIFactory.Hex("#8FE0FF");

        public HowToArt(UIFactory ui, GameTheme theme) { _ui = ui; _theme = theme; }
        private TMP_FontAsset Glyphs => _theme != null ? _theme.glyphFont : null;

        public void Build(int page, RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Object.Destroy(parent.GetChild(i).gameObject);
            var box = Wrapper(parent);
            switch (page)
            {
                case 0: Move(box); break;
                case 1: Modes(box); break;
                case 2: Trap(box); break;
                case 3: Combo(box); break;
                case 4: Transform(box); break;
                default: Shield(box); break;
            }
        }

        // ── Pages ───────────────────────────────────────────────────────────────
        private void Move(Transform box)
        {
            var cells = Grid(box, 5, 5, 30, out var g);
            int[,] hi = { { 0, 1 }, { 4, 1 }, { 0, 3 }, { 4, 3 }, { 1, 0 }, { 3, 0 }, { 1, 4 }, { 3, 4 } };
            for (int i = 0; i < hi.GetLength(0); i++) Hi(g[hi[i, 0], hi[i, 1]]);
            Put(g[2, 2], "\u265E", Gold, 20);
        }

        private void Modes(Transform box)
        {
            Caption(box, "TRAP \u2014 slip through the gaps", UIFactory.Hex("#D08080"), 11);
            Grid(box, 5, 2, 26, out var t);
            Put(t[0, 0], "\u2715", Red, 15); Put(t[1, 0], "\u2715", Red, 15);
            Put(t[3, 0], "\u2715", Red, 15); Put(t[4, 0], "\u2715", Red, 15);
            Put(t[2, 1], "\u265E", Gold, 16);
            Caption(box, "BATTLE \u2014 capture the pieces", UIFactory.Hex("#7FB0E0"), 11);
            Grid(box, 5, 2, 26, out var b);
            Put(b[0, 0], "\u265B", Pink, 16); Put(b[2, 0], "\u265C", Blue, 16); Put(b[4, 0], "\u265D", Violet, 16);
            Put(b[2, 1], "\u265E", Gold, 16);
        }

        private void Trap(Transform box)
        {
            Grid(box, 5, 3, 28, out var g);
            for (int x = 0; x < 5; x++) if (x != 1) Put(g[x, 1], "\u2715", Red, 16);
            Hi(g[1, 1]);
            Put(g[3, 2], "\u265E", Gold, 17);
            Caption(box, "leap through the gaps to push forward", Cap, 11);
        }

        private void Combo(Transform box)
        {
            var row = HRow(box, 8);
            Put(Box(row, 34), "\u265E", Gold, 20);
            Caption(row, "\u2192", Cap, 18);
            Hi(Box(row, 34, out var rt)); Put(rt, "\u265C", Blue, 18);
            Caption(row, "+4", GreenTxt, 19);
            // Thaleah has no "\u00D7" (U+00D7); use "x" so the multiplier stays in the same
            // pixel font as the "5" (a fallback \u00D7 rendered mismatched and off-center).
            Pill(row, "x5", Gold);
            Caption(box, "each capture in a row builds the x", Cap, 11);
        }

        private void Transform(Transform box)
        {
            var row = HRow(box, 6);
            Put(Box(row, 32), "\u265E", Gold, 19);
            Caption(row, "\u2192", Cap, 16);
            Put(Box(row, 28), "\u265C", Blue, 16);
            Put(Box(row, 28), "\u265B", Pink, 16);
            var third = Box(row, 30, out var trt); HiBorder(trt); Put(trt, "\u265D", Violet, 16);
            Caption(row, "\u2192", Cap, 16);
            Put(Box(row, 40), "\u265D", Violet, 24);
            Pill(box, "5.0s", Frost);
            Caption(box, "capture 3 \u2192 become the 3rd for 5s", Cap, 11);
        }

        private void Shield(Transform box)
        {
            var row = HRow(box, 10);
            Put(Box(row, 44), "\u265B", Pink, 26);
            Caption(row, "shielded!", Frost, 14);
            Bar(box, 150, 12, 0.62f, Frost);
            Caption(box, "a hit costs the form, not your run", Cap, 11);
        }

        // ── Building blocks ─────────────────────────────────────────────────────
        private RectTransform Wrapper(RectTransform parent)
        {
            var go = New("art", parent);
            var rt = go.GetComponent<RectTransform>();
            UIFactory.Stretch(rt);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.MiddleCenter; v.spacing = 6f;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            return rt;
        }

        private RectTransform Grid(Transform parent, int cols, int rows, float c, out RectTransform[,] cells)
        {
            var go = New("grid", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(cols * c, rows * c);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = cols * c; le.preferredHeight = rows * c;
            cells = new RectTransform[cols, rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    var cell = New("cell", go.transform);
                    var crt = cell.GetComponent<RectTransform>();
                    crt.anchorMin = crt.anchorMax = new Vector2(0, 1); crt.pivot = new Vector2(0, 1);
                    crt.sizeDelta = new Vector2(c, c);
                    crt.anchoredPosition = new Vector2(x * c, -y * c);
                    cell.AddComponent<Image>().color = ((x + y) % 2 == 0) ? CellA : CellB;
                    cells[x, y] = crt;
                }
            return rt;
        }

        private RectTransform HRow(Transform parent, float spacing)
        {
            var go = New("row", parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var f = go.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.GetComponent<RectTransform>();
        }

        private RectTransform Box(Transform parent, float size) { return Box(parent, size, out _); }
        private RectTransform Box(Transform parent, float size, out RectTransform rt)
        {
            var go = New("box", parent);
            rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size; le.preferredHeight = size;
            var img = go.AddComponent<Image>();
            img.sprite = _ui.Fill(); img.type = Image.Type.Sliced; img.color = CellA;
            return rt;
        }

        private void Put(RectTransform cell, string glyph, Color color, float size)
        {
            var go = New("g", cell);
            UIFactory.Stretch(go.GetComponent<RectTransform>());
            var t = go.AddComponent<TextMeshProUGUI>();
            if (Glyphs) t.font = Glyphs; else if (_ui.Font) t.font = _ui.Font;
            // Chess-piece glyphs (U+2654–265F) read a touch small at the pixel font's
            // proportions, so nudge them up slightly on the how-to diagrams.
            bool isPiece = glyph.Length > 0 && glyph[0] >= '♔' && glyph[0] <= '♟';
            t.text = glyph; t.color = color; t.fontSize = isPiece ? size * 1.2f : size;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        }

        private void Hi(RectTransform cell)
        {
            var go = New("hi", cell);
            UIFactory.Stretch(go.GetComponent<RectTransform>());
            go.AddComponent<Image>().color = new Color(0.94f, 0.75f, 0.25f, 0.22f);
            var dot = New("dot", cell);
            var drt = dot.GetComponent<RectTransform>();
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f); drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(7, 7); drt.anchoredPosition = Vector2.zero;
            var di = dot.AddComponent<Image>();
            di.sprite = _ui.Circle(); di.color = new Color(0.94f, 0.75f, 0.25f, 0.9f);
        }

        private void HiBorder(RectTransform cell)
        {
            var go = New("border", cell);
            UIFactory.Stretch(go.GetComponent<RectTransform>());
            var img = go.AddComponent<Image>();
            img.sprite = _ui.Border(); img.type = Image.Type.Sliced; img.color = Gold;
        }

        private void Bar(Transform parent, float w, float h, float fill, Color color)
        {
            var go = New("bar", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = h;
            var bg = go.AddComponent<Image>();
            bg.sprite = _ui.Fill(); bg.type = Image.Type.Sliced;
            bg.color = new Color(1, 1, 1, 0.12f);
            var fg = New("fill", go.transform);
            var frt = fg.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(fill, 1); frt.pivot = new Vector2(0, 0.5f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            var fi = fg.AddComponent<Image>();
            fi.sprite = _ui.Fill(); fi.type = Image.Type.Sliced; fi.color = color;
        }

        private void Pill(Transform parent, string text, Color color)
        {
            var go = New("pill", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _ui.Fill(); img.type = Image.Type.Sliced;
            img.color = UIFactory.Hex("#23304F");
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 12, 6, 6); h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var f = go.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var t = New("t", go.transform).AddComponent<TextMeshProUGUI>();
            if (_ui.Font) t.font = _ui.Font;
            t.text = text; t.color = color; t.fontSize = 16; t.fontStyle = FontStyles.Normal;
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        }

        private void Caption(Transform parent, string text, Color color, float size)
        {
            var t = New("cap", parent).AddComponent<TextMeshProUGUI>();
            if (_ui.Font) t.font = _ui.Font;
            t.text = text; t.color = color; t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        }

        private static GameObject New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
