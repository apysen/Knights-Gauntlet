using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Custom leaderboard UI: Personal (local) and Online (Supabase) sources, a
    // category grid, crowns for the top three, and each row's equipped-skin glyph
    // tinted by the theme. Rows use explicit anchoring (not a layout group) so the
    // rank, score, crown and skin never clip at the edges.
    public class LeaderboardScreen : MonoBehaviour
    {
        private GameTheme _theme;
        private LeaderboardClient _client;
        private UIFactory _ui;

        private GameObject _root;
        private RectTransform _listContent;
        private ScrollRect _scroll;
        private TMP_Text _status;

        private string _source = "online";
        private string _category = "trap-hard";
        private Button _personalTab, _onlineTab, _dailyTab;
        private GameObject _personalCats, _onlineCats, _dailyCats;
        private readonly Dictionary<string, Button> _catBtns = new Dictionary<string, Button>();

        private static readonly Color Gold = UIFactory.Hex("#FFD34D");
        private static readonly Color Silver = UIFactory.Hex("#CFD6E0");
        private static readonly Color Bronze = UIFactory.Hex("#CD7F32");
        private static readonly Color RowText = UIFactory.Hex("#E2E6F0");
        private static readonly Color RowLine = new Color(1f, 1f, 1f, 0.06f);

        // Row geometry (reference px). Left gutter holds crown + rank/name; the
        // right gutter holds score + skin. Name stretches between them.
        private const float RowH = 50f;
        private const float LeftPad = 14f;
        private const float NameLeft = 46f;   // space reserved on the left for the crown
        private const float NameRight = 116f;  // space reserved on the right for score+skin
        private const float ScoreFromRight = 40f;
        private const float ScoreW = 86f;
        private const float SkinFromRight = 8f;
        private const float SkinW = 26f;
        private const float RewardW = 80f;
        private static readonly int[] DailyRewards = { 1000, 750, 500, 250, 100 };

        public void Init(GameTheme theme, LeaderboardClient client)
        {
            _theme = theme; _client = client;
            _ui = new UIFactory(
                theme != null ? theme.uiFont : null,
                theme != null ? theme.uiPanelSprite : null,
                theme != null ? theme.uiBorderSprite : null,
                theme != null ? theme.uiCircleSprite : null);
            Build();
            _root.SetActive(false);
        }

        public void Open(GameMode mode, Difficulty diff)
        {
            _source = "online";
            string md = mode == GameMode.Trap ? "trap" : "battle";
            _category = (diff == Difficulty.Hard || diff == Difficulty.Grandmaster)
                ? md + "-" + diff.ToString().ToLower()
                : md + "-hard";
            ApplySourceVisibility();
            UIFactory.StyleToggle(_personalTab, false);
            UIFactory.StyleToggle(_onlineTab, true);
            if (_dailyTab != null) UIFactory.StyleToggle(_dailyTab, false);
            Refresh();
            _root.SetActive(true);
        }

        private void Build()
        {
            var canvas = _ui.CreateCanvas("Leaderboard", 130);
            _root = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 12f);

            _ui.Label(col, "Leaderboard", 30, UIFactory.Gold, FontStyles.Bold);

            var srcRow = _ui.Row(col, 10f);
            _personalTab = _ui.Button(srcRow, "Personal", () => SetSource("personal"), UIFactory.Panel, UIFactory.Muted, 122, 50, 15);
            _onlineTab = _ui.Button(srcRow, "Online", () => SetSource("online"), UIFactory.Panel, UIFactory.Muted, 110, 50, 15);
            _dailyTab = _ui.Button(srcRow, "Daily", () => SetSource("daily"), UIFactory.Panel, UIFactory.Muted, 110, 50, 15);

            // Personal categories: one row per mode, all four difficulties.
            _personalCats = _ui.Column(col, 7f).gameObject;
            var pTrap = _ui.Row(_personalCats.transform, 6f);
            _ui.Label(pTrap, "TRAP", 12, UIFactory.Muted, FontStyles.Bold, 56);
            CatTab(pTrap, "personal", "trap-easy", "Easy"); CatTab(pTrap, "personal", "trap-medium", "Med");
            CatTab(pTrap, "personal", "trap-hard", "Hard"); CatTab(pTrap, "personal", "trap-grandmaster", "GM");
            var pBat = _ui.Row(_personalCats.transform, 6f);
            _ui.Label(pBat, "BATTLE", 12, UIFactory.Muted, FontStyles.Bold, 56);
            CatTab(pBat, "personal", "battle-easy", "Easy"); CatTab(pBat, "personal", "battle-medium", "Med");
            CatTab(pBat, "personal", "battle-hard", "Hard"); CatTab(pBat, "personal", "battle-grandmaster", "GM");

            // Online categories: Hard & GM per mode.
            _onlineCats = _ui.Column(col, 7f).gameObject;
            var oTrap = _ui.Row(_onlineCats.transform, 6f);
            _ui.Label(oTrap, "TRAP", 12, UIFactory.Muted, FontStyles.Bold, 56);
            CatTab(oTrap, "online", "trap-hard", "Hard"); CatTab(oTrap, "online", "trap-grandmaster", "GM");
            var oBat = _ui.Row(_onlineCats.transform, 6f);
            _ui.Label(oBat, "BATTLE", 12, UIFactory.Muted, FontStyles.Bold, 56);
            CatTab(oBat, "online", "battle-hard", "Hard"); CatTab(oBat, "online", "battle-grandmaster", "GM");

            // Daily: a single date-seeded board shared by everyone, resets daily.
            _dailyCats = _ui.Column(col, 4f).gameObject;
            _ui.Label(_dailyCats.transform, "TODAY'S CHALLENGE", 13, UIFactory.Gold, FontStyles.Bold, 360);
            _ui.Label(_dailyCats.transform, "Trap \u00B7 Grandmaster \u00B7 a new board every day.", 11, UIFactory.Muted, FontStyles.Normal, 360);

            _status = _ui.Label(col, "", 14, UIFactory.Muted, FontStyles.Normal, 360);

            BuildScroll(col);

            _ui.Button(col, "\u2190 Back", () => _root.SetActive(false), UIFactory.Bg, UIFactory.Gold, 220, 50, 16, true);

            SetSource("online");
        }

        private void CatTab(Transform parent, string source, string key, string label)
        {
            var btn = _ui.Button(parent, label, () => SetCategory(key), UIFactory.Panel, UIFactory.Muted, 72, 44, 13);
            // Keyed by source+category: the same category (e.g. trap-hard) appears
            // in both the Personal and Online grids, so a plain key would collide
            // and the wrong button would get highlighted.
            _catBtns[source + "|" + key] = btn;
        }

        private void BuildScroll(Transform parent)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            var le = scrollGo.AddComponent<LayoutElement>();
            le.preferredWidth = 360; le.preferredHeight = 470;
            _scroll = scrollGo.AddComponent<ScrollRect>();
            _scroll.horizontal = false; _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 26f;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = new Vector2(-10, 0); // leave room for scrollbar
            var vpImg = viewport.AddComponent<Image>();
            vpImg.sprite = _ui.Fill(); vpImg.type = Image.Type.Sliced;
            vpImg.color = new Color(0.08f, 0.09f, 0.16f, 1f);
            viewport.AddComponent<RectMask2D>();
            _scroll.viewport = vpRt;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _listContent = content.GetComponent<RectTransform>();
            _listContent.anchorMin = new Vector2(0, 1); _listContent.anchorMax = new Vector2(1, 1);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.offsetMin = new Vector2(0, _listContent.offsetMin.y);
            _listContent.offsetMax = new Vector2(0, _listContent.offsetMax.y);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 0f; vlg.padding = new RectOffset(0, 0, 4, 4);
            var fit = content.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.content = _listContent;

            BuildScrollbar(scrollGo.transform);
        }

        private void BuildScrollbar(Transform scrollParent)
        {
            var sbGo = new GameObject("Scrollbar", typeof(RectTransform));
            sbGo.transform.SetParent(scrollParent, false);
            var rt = sbGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(8, 0); rt.anchoredPosition = Vector2.zero;
            var bg = sbGo.AddComponent<Image>();
            bg.sprite = _ui.Fill(); bg.type = Image.Type.Sliced;
            bg.color = new Color(1, 1, 1, 0.05f);
            var sb = sbGo.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;

            var slideGo = new GameObject("SlidingArea", typeof(RectTransform));
            slideGo.transform.SetParent(sbGo.transform, false);
            UIFactory.Stretch(slideGo.GetComponent<RectTransform>());
            var handleGo = new GameObject("Handle", typeof(RectTransform));
            handleGo.transform.SetParent(slideGo.transform, false);
            var hRt = handleGo.GetComponent<RectTransform>();
            UIFactory.Stretch(hRt);
            var hImg = handleGo.AddComponent<Image>();
            hImg.sprite = _ui.Fill(); hImg.type = Image.Type.Sliced;
            hImg.color = new Color(1, 1, 1, 0.28f);
            sb.handleRect = hRt; sb.targetGraphic = hImg;

            _scroll.verticalScrollbar = sb;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void SetSource(string src)
        {
            _source = src;
            UIFactory.StyleToggle(_personalTab, src == "personal");
            UIFactory.StyleToggle(_onlineTab, src == "online");
            if (_dailyTab != null) UIFactory.StyleToggle(_dailyTab, src == "daily");
            _category = DefaultCategoryFor(src);
            ApplySourceVisibility();
            Refresh();
        }
        // Top-left difficulty of each source's grid, so switching tabs always lands
        // on a populated board (never a blank one).
        private static string DefaultCategoryFor(string src)
        {
            if (src == "daily") return "daily-" + System.DateTime.Now.ToString("yyyyMMdd");
            if (src == "online") return "trap-hard";
            return "trap-easy"; // personal
        }

        private void SetCategory(string key) { _category = key; Refresh(); }

        private void ApplySourceVisibility()
        {
            _personalCats.SetActive(_source == "personal");
            _onlineCats.SetActive(_source == "online");
            if (_dailyCats != null) _dailyCats.SetActive(_source == "daily");
            string sel = _source + "|" + _category;
            foreach (var kv in _catBtns) UIFactory.StyleToggle(kv.Value, kv.Key == sel);
        }

        private void Refresh()
        {
            ApplySourceVisibility();
            ClearList();
            if (_source == "online" || _source == "daily")
            {
                if (!LeaderboardClient.IsOnlineCategory(_category))
                {
                    _status.text = _source == "daily" ? "No daily board yet." : "Online is Hard & Grandmaster only.";
                    return;
                }
                _status.text = "Loading\u2026";
                _client.FetchOnline(_category, rows => { if (this != null) RenderRows(rows); });
            }
            else
            {
                RenderRows(_client.GetLocal(_category));
            }
        }

        private void RenderRows(List<ScoreRow> rows)
        {
            ClearList();
            if (rows == null || rows.Count == 0) { _status.text = "No scores yet."; return; }
            _status.text = "";
            for (int i = 0; i < rows.Count; i++)
            {
                int rank = i + 1;
                string reward = (_source == "daily" && rank <= DailyRewards.Length) ? "+" + DailyRewards[rank - 1] : null;
                AddRow(rank, rows[i], reward);
            }
            _scroll.verticalNormalizedPosition = 1f;
        }

        private void ClearList()
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);
        }

        // Each row is a fixed-height container with explicitly anchored children, so
        // the rank, score, crown, and skin glyph stay put regardless of width.
        private void AddRow(int rank, ScoreRow row, string reward = null)
        {
            var go = new GameObject("row", typeof(RectTransform));
            go.transform.SetParent(_listContent, false);
            var le = go.AddComponent<LayoutElement>(); le.minHeight = RowH; le.preferredHeight = RowH;

            // When a reward badge is shown (daily top 5), shift the score+skin left
            // to make room for it on the far right.
            float shift = string.IsNullOrEmpty(reward) ? 0f : RewardW + 6f;

            // bottom separator line
            var line = new GameObject("line", typeof(RectTransform));
            line.transform.SetParent(go.transform, false);
            var lrt = line.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(0.5f, 0);
            lrt.sizeDelta = new Vector2(0, 1); lrt.anchoredPosition = Vector2.zero;
            line.AddComponent<Image>().color = RowLine;

            bool top = rank == 1;
            Color rowColor = top ? UIFactory.Gold : RowText;

            // crown (top 3 only), anchored far left
            if (rank <= 3)
            {
                var crown = Glyph(go.transform, "\u2654", 19, rank == 1 ? Gold : rank == 2 ? Silver : Bronze,
                                  _theme != null ? _theme.glyphFont : null, TextAlignmentOptions.Center);
                AnchorLeft(crown.rectTransform, LeftPad, 26);
            }

            // rank + name, stretched between gutters, left-aligned
            var name = Glyph(go.transform, rank + ". " + row.name, 17, rowColor, _ui.Font,
                             TextAlignmentOptions.Left, top ? FontStyles.Bold : FontStyles.Normal);
            var nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 1);
            nrt.offsetMin = new Vector2(NameLeft, 0); nrt.offsetMax = new Vector2(-(NameRight + shift), 0);

            // score, anchored from the right
            var score = Glyph(go.transform, row.score.ToString(), 17, rowColor, _ui.Font,
                              TextAlignmentOptions.Right, FontStyles.Bold);
            AnchorRight(score.rectTransform, ScoreFromRight + shift, ScoreW);

            // skin glyph, anchored far right
            string skin = string.IsNullOrEmpty(row.skin) ? "gold" : row.skin;
            Color skinColor = _theme != null ? _theme.GetSkinTint(skin) : UIFactory.Gold;
            var sk = Glyph(go.transform, "\u265E", 17, skinColor, _theme != null ? _theme.glyphFont : null,
                           TextAlignmentOptions.Center);
            AnchorRight(sk.rectTransform, SkinFromRight + shift, SkinW);

            // daily reward badge, far right
            if (!string.IsNullOrEmpty(reward))
            {
                var rw = Glyph(go.transform, reward, 15, UIFactory.Gold, _ui.Font,
                               TextAlignmentOptions.Right, FontStyles.Bold);
                AnchorRight(rw.rectTransform, 8f, RewardW);
            }
        }

        private TMP_Text Glyph(Transform parent, string text, float size, Color color,
                               TMP_FontAsset font, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("c", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font) t.font = font;
            t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
            t.alignment = align; t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
            return t;
        }

        private static void AnchorLeft(RectTransform rt, float x, float w)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(w, 32); rt.anchoredPosition = new Vector2(x, 0);
        }

        private static void AnchorRight(RectTransform rt, float xFromRight, float w)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f); rt.pivot = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(w, 32); rt.anchoredPosition = new Vector2(-xFromRight, 0);
        }
    }
}
