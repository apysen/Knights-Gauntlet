using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Builds the little chess-grid diagrams shown on each How-to page, mirroring
    // the SVG art from the web version. Piece/trap glyphs use the theme's symbol
    // font; labels use the regular UI font.
    public class HowToArt
    {
        private readonly UIFactory _ui;
        private readonly GameTheme _theme;

        private static readonly Color Gold = UIFactory.Hex("#F0C040");
        private static readonly Color Blue = UIFactory.Hex("#4080E0");
        private static readonly Color Violet = UIFactory.Hex("#9060E0");
        private static readonly Color Pink = UIFactory.Hex("#E04080");
        private static readonly Color Green = UIFactory.Hex("#50C878");
        private static readonly Color Red = UIFactory.Hex("#CC4444");
        private static readonly Color Cap = UIFactory.Hex("#8A93AD");
        private static readonly Color GreenTxt = UIFactory.Hex("#3CDC78");
        private static readonly Color Frost = UIFactory.Hex("#8FE0FF");

        // The in-game board look. Green grass tiles on dark grout inside a slate
        // rim with a gold edge, gold target squares, and the real 3D pieces and trap token.
        private static readonly Color TileLight = UIFactory.Hex("#5B8661");
        private static readonly Color TileDark = UIFactory.Hex("#497350");
        private static readonly Color Grout = UIFactory.Hex("#1D2E40");
        private static readonly Color Rim = UIFactory.Hex("#2E414F");
        private static readonly Color RimGold = new Color(.87f, .73f, .41f, .8f);
        private static readonly Color TargetTint = new Color(.66f, .58f, .27f, .9f);
        private static readonly Color TargetRing = UIFactory.Hex("#EBBC40");
        private static readonly Color CapLight = UIFactory.Hex("#C9D2E3");

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
            Put(Box(row, 40), "\u265D", Violet, 24, player: true);
            Pill(box, "5.0s", Frost);
            Caption(box, "capture 3 \u2192 become the 3rd for 5s", Cap, 11);
        }

        private void Shield(Transform box)
        {
            var row = HRow(box, 10);
            Put(Box(row, 44), "\u265B", Pink, 26, player: true);
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
            return BoardGrid(parent, cols, rows, c * 1.15f, out cells);
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
            // A single board tile on its grout, like a square lifted off the board.
            img.color = Grout;
            var tile = New("tile", go.transform);
            var trt = tile.GetComponent<RectTransform>(); UIFactory.Stretch(trt);
            trt.offsetMin = new Vector2(2f, 2f); trt.offsetMax = new Vector2(-2f, -2f);
            tile.AddComponent<Image>().color = TileLight;
            Shine(trt);
            return rt;
        }

        private void Put(RectTransform cell, string glyph, Color color, float size, bool player = false)
        {
            if (PutModel(cell, glyph, color, player)) return;
            var go = New("g", cell);
            UIFactory.Stretch(go.GetComponent<RectTransform>());
            var t = go.AddComponent<TextMeshProUGUI>();
            if (Glyphs) t.font = Glyphs; else if (_ui.Font) t.font = _ui.Font;
            // Chess-piece glyphs (U+2654–265F) read a touch small, so nudge them up slightly.
            bool isPiece = glyph.Length > 0 && glyph[0] >= '♔' && glyph[0] <= '♟';
            t.text = glyph; t.color = color; t.fontSize = isPiece ? size * 1.2f : size;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        }

        private void Hi(RectTransform cell)
        {
            Target(cell);
        }

        private void HiBorder(RectTransform cell)
        {
            Frame(cell, TargetRing, 3f, 2f);
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
            t.text = text; t.color = color == Cap ? CapLight : color; t.fontSize = size * 1.15f;
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        }

        // ── In-game board pieces ───────────────────────────────────────
        private RectTransform BoardGrid(Transform parent, int cols, int rows, float c, out RectTransform[,] cells)
        {
            const float pad = 5f, gap = 2f;
            var go = New("board", parent);
            var rt = go.GetComponent<RectTransform>();
            float w = cols * c + pad * 2f, h = rows * c + pad * 2f;
            rt.sizeDelta = new Vector2(w, h);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = h;
            var rim = go.AddComponent<Image>(); rim.color = Rim;
            var edge = go.AddComponent<Outline>(); edge.effectColor = RimGold; edge.effectDistance = new Vector2(1.5f, -1.5f);
            var grout = New("grout", go.transform);
            var grt = grout.GetComponent<RectTransform>(); UIFactory.Stretch(grt);
            grt.offsetMin = new Vector2(pad, pad); grt.offsetMax = new Vector2(-pad, -pad);
            grout.AddComponent<Image>().color = Grout;
            cells = new RectTransform[cols, rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    var cell = New("cell", grout.transform);
                    var crt = cell.GetComponent<RectTransform>();
                    crt.anchorMin = crt.anchorMax = new Vector2(0, 1); crt.pivot = new Vector2(.5f, .5f);
                    crt.sizeDelta = new Vector2(c - gap, c - gap);
                    crt.anchoredPosition = new Vector2(x * c + c * .5f, -y * c - c * .5f);
                    cell.AddComponent<Image>().color = ((x + y) % 2 == 0) ? TileLight : TileDark;
                    Shine(crt);
                    cells[x, y] = crt;
                }
            return rt;
        }

        // The soft top-lit bevel the 3D tiles have.
        private static void Shine(RectTransform tile)
        {
            var s = New("shine", tile);
            var srt = s.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, .72f); srt.anchorMax = Vector2.one;
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            var img = s.AddComponent<Image>(); img.color = new Color(1f, 1f, 1f, .07f); img.raycastTarget = false;
        }

        // A move target, the way the board marks one: a gold-washed tile inside a gold ring.
        private void Target(RectTransform cell)
        {
            var wash = New("target", cell);
            UIFactory.Stretch(wash.GetComponent<RectTransform>());
            var img = wash.AddComponent<Image>(); img.color = TargetTint; img.raycastTarget = false;
            Frame(cell, TargetRing, 2.5f, 3.5f);
        }

        // Four strips: a crisp square ring of `thick` px, `inset` px inside the cell.
        private static void Frame(RectTransform cell, Color color, float thick, float inset)
        {
            for (int i = 0; i < 4; i++)
            {
                var g = New("ring", cell);
                var rt = g.GetComponent<RectTransform>();
                bool horizontal = i < 2;
                rt.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0f);
                rt.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 1f);
                rt.pivot = horizontal ? new Vector2(.5f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, .5f);
                rt.sizeDelta = horizontal ? new Vector2(-inset * 2f, thick) : new Vector2(thick, -inset * 2f);
                rt.anchoredPosition = horizontal ? new Vector2(0f, i == 0 ? -inset : inset) : new Vector2(i == 2 ? inset : -inset, 0f);
                var img = g.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            }
        }

        // A chess/trap glyph becomes the real 3D model. False for anything else (arrows,
        // labels) or if the model can't be rendered, so the caller draws the glyph.
        private bool PutModel(RectTransform cell, string glyph, Color color, bool player)
        {
            PieceType type;
            switch (glyph)
            {
                case "\u265E": type = PieceType.Knight; break;
                case "\u265B": type = PieceType.Queen; break;
                case "\u265C": type = PieceType.Rook; break;
                case "\u265D": type = PieceType.Bishop; break;
                case "\u2715": type = PieceType.Trap; break;
                default: return false;
            }
            bool isPlayer = player || (type == PieceType.Knight && color == Gold);
            var tex = PieceIconCache.Get(_theme, type, isPlayer, isPlayer ? SaveData.GetSkin() : null);
            if (tex == null) return false;
            float c = Mathf.Max(cell.sizeDelta.x, 20f);
            var go = New("piece", cell);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            bool token = type == PieceType.Trap;
            // Pieces stand a little taller than their square, as on the board.
            rt.sizeDelta = Vector2.one * c * (token ? .92f : 1.4f);
            rt.anchoredPosition = new Vector2(0f, token ? 0f : c * .14f);
            var img = go.AddComponent<RawImage>(); img.texture = tex; img.raycastTarget = false;
            return true;
        }

        private static GameObject New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
