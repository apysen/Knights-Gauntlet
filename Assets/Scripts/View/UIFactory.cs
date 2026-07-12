using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace KnightsGauntlet.View
{
    // Helpers for building uGUI screens in code, now with rounded sprites and a
    // gold-outline toggle state to match the web look. Fonts funnel through Font.
    public class UIFactory
    {
        public TMP_FontAsset Font;
        public Sprite PanelSprite, BorderSprite, CircleSprite, ButtonSprite;

        // Global multiplier on every label's point size. A light bump so the pixel
        // font reads a touch larger across the whole UI.
        public const float FontScale = 1.12f;

        // DEVNIK "2D UI Pixel Buttons": the neutral, text-free bar used as the
        // 9-sliced background for every labelled button. Loaded once from Resources
        // (the sheet lives at Assets/Resources/UI/…), tinted to the game's palette
        // by the caller so the pixel look adopts our gold/dark scheme.
        private static Sprite _pixelButton;
        private static bool _pixelButtonTried;

        // "Pixel Font by Thaleah": a TMP font asset built once at runtime from the
        // bundled TTF (there is no pre-baked .asset for it). Shared by every screen.
        private static TMP_FontAsset _pixelFont;
        private static bool _pixelFontTried;

        public static readonly Color Gold = Hex("#F0C040");
        public static readonly Color Bg = Hex("#1A1A2E");
        public static readonly Color Panel = Hex("#16213E");
        public static readonly Color Muted = Hex("#9098AD");
        public static readonly Color Text = Hex("#EEEEEE");
        public static readonly Color Amber = Hex("#FFB454");
        public static readonly Color Error = Hex("#E05050");

        public UIFactory(TMP_FontAsset font, Sprite panel = null, Sprite border = null,
                         Sprite circle = null, Sprite button = null)
        {
            Font = font; PanelSprite = panel; BorderSprite = border; CircleSprite = circle;
            ButtonSprite = button;
            EnsureEventSystem();
        }
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        // Sprite resolvers: use the theme's custom art if provided, else the
        // procedurally generated default. Everything visual is overridable.
        public Sprite Fill() => PanelSprite != null ? PanelSprite : UISprites.RoundedFill();
        public Sprite Border() => BorderSprite != null ? BorderSprite : UISprites.PixelBorder();
        public Sprite Circle() => CircleSprite != null ? CircleSprite : UISprites.Circle();

        // Background for labelled buttons: theme override → DEVNIK pixel bar → the
        // procedural rounded fill (so the game still builds if the art is missing).
        public Sprite ButtonBg()
        {
            if (ButtonSprite != null) return ButtonSprite;
            if (!_pixelButtonTried)
            {
                _pixelButtonTried = true;
                var all = Resources.LoadAll<Sprite>("UI/UI SIMPLE PIXEL UNSPLIT");
                foreach (var s in all)
                    if (s.name == "BAR1") { _pixelButton = s; break; }
            }
            return _pixelButton != null ? _pixelButton : Fill();
        }

        // The Thaleah pixel TMP font, generated on first use from the bundled TTF in
        // Resources. Returns null if the TTF is missing, so callers keep the TMP
        // default. Chess-glyph text keeps its own (DejaVu) font and isn't in here.
        public static TMP_FontAsset PixelFont()
        {
            if (!_pixelFontTried)
            {
                _pixelFontTried = true;
                var ttf = Resources.Load<Font>("Fonts/ThaleahFat_TTF");
                if (ttf != null) _pixelFont = TMP_FontAsset.CreateFontAsset(ttf);
            }
            return _pixelFont;
        }

        // The font every UI label should use: an explicit theme font if set, else
        // the Thaleah pixel font.
        public TMP_FontAsset ResolveFont() => Font != null ? Font : PixelFont();

        // The pixel chess-piece TMP font (PixelChess.ttf), built once at runtime.
        // DejaVu is wired in as a fallback so glyphs PixelChess lacks (notably the
        // trap "✕") still render. Used for every chess-piece glyph in the game.
        private static TMP_FontAsset _chessFont;
        private static bool _chessFontTried;
        public static TMP_FontAsset ChessFont()
        {
            if (!_chessFontTried)
            {
                _chessFontTried = true;
                var ttf = Resources.Load<Font>("Fonts/PixelChess");
                if (ttf != null)
                {
                    _chessFont = TMP_FontAsset.CreateFontAsset(ttf);
                    var fbTtf = Resources.Load<Font>("Fonts/DejaVuSans-Bold");
                    if (_chessFont != null && fbTtf != null)
                    {
                        var fb = TMP_FontAsset.CreateFontAsset(fbTtf);
                        if (fb != null)
                        {
                            _chessFont.fallbackFontAssetTable ??=
                                new System.Collections.Generic.List<TMP_FontAsset>();
                            _chessFont.fallbackFontAssetTable.Add(fb);
                        }
                    }
                }
            }
            return _chessFont;
        }

        public void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }
        }

        public Canvas CreateCanvas(string name, int sortOrder, Vector2? referenceResolution = null)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Lower reference = larger UI. Overlays (menus, how-to, achievements)
            // use a smaller reference so text/buttons fill more of the screen; the
            // in-game HUD passes the original 640x1138 to keep its buttons compact.
            scaler.referenceResolution = referenceResolution ?? new Vector2(533, 948);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public GameObject Screen(Transform parent, Color bg)
        {
            var go = NewUI("Screen", parent);
            Stretch(go.GetComponent<RectTransform>());
            go.AddComponent<Image>().color = bg;
            return go;
        }

        public RectTransform Column(Transform parent, float spacing = 14f)
        {
            var go = NewUI("Column", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            AddFitter(go);
            return rt;
        }

        public RectTransform Row(Transform parent, float spacing = 10f)
        {
            var go = NewUI("Row", parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            AddFitter(go);
            return go.GetComponent<RectTransform>();
        }

        public TMP_Text Label(Transform parent, string text, float size, Color color,
                              FontStyles style = FontStyles.Normal, float width = 320f)
        {
            var go = NewUI("Label", parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            var font = ResolveFont();
            if (font) t.font = font;
            t.text = text; t.fontSize = size * FontScale; t.color = color; t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            return t;
        }

        // pixel: use the DEVNIK pixel-bar background (default). Pass false for the
        // small square icon/grid tiles (restart, pause, level cells, skin swatches)
        // that read better with the plain rounded fill.
        // clickSfx: the sound played when the button is pressed. Defaults to the UI
        // "click" (Cursor 1) so every button is audible; pass a different Sfx for
        // special buttons (Play, adventure level, page turns) or null for silence.
        public Button Button(Transform parent, string text, Action onClick,
                             Color? fill = null, Color? textColor = null,
                             float width = 220f, float height = 50f, float fontSize = 20f,
                             bool outline = false, bool pixel = true,
                             Sfx? clickSfx = Sfx.TabSelect)
        {
            var go = NewUI("Button", parent);
            var img = go.AddComponent<Image>();
            img.sprite = pixel ? ButtonBg() : Fill(); img.type = Image.Type.Sliced;
            img.color = fill ?? Gold;
            // Draw the pixel bar's 9-slice at 2x pixels-per-unit so its baked black
            // outline (and corners) render at roughly half thickness. The sprite's
            // outline looked too heavy at 1:1.
            if (pixel) img.pixelsPerUnitMultiplier = 2f;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.minWidth = width;
            le.preferredHeight = height; le.minHeight = height;

            var border = NewUI("Border", go.transform);
            Stretch(border.GetComponent<RectTransform>());
            var bimg = border.AddComponent<Image>();
            bimg.sprite = Border(); bimg.type = Image.Type.Sliced;
            bimg.color = Gold; bimg.raycastTarget = false;
            // Match the pixel bg's 9-slice scale so the gold selection outline is the
            // same thickness/corner as the button's baked black border.
            if (pixel) bimg.pixelsPerUnitMultiplier = 2f;
            border.SetActive(outline);

            var label = Label(go.transform, text, fontSize, textColor ?? Bg, FontStyles.Normal, width);
            var labelRt = label.rectTransform;
            Stretch(labelRt);
            // Inset from the pixel border so glyphs don't touch the edge.
            labelRt.offsetMin = new Vector2(12f, 4f);
            labelRt.offsetMax = new Vector2(-12f, -4f);
            label.GetComponent<LayoutElement>().ignoreLayout = true;
            // The Thaleah pixel font is chunky; auto-shrink (never grow past the
            // requested size) so any label fits within its button.
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize * FontScale;
            label.fontSizeMin = Mathf.Max(6f, fontSize * FontScale * 0.4f);

            btn.onClick.AddListener(() =>
            {
                if (clickSfx.HasValue) SfxManager.Play(clickSfx.Value);
                onClick?.Invoke();
            });
            return btn;
        }

        // A horizontal 0..1 slider styled with the DEVNIK pixel assets: a dark pixel-bar
        // track, a gold pixel-bar fill, and a light pixel-bar handle with a gold border,
        // all drawn at 2x pixels-per-unit to match the buttons. onChange fires
        // continuously as the player drags.
        public UnityEngine.UI.Slider Slider(Transform parent, float value, Action<float> onChange,
                             float width = 300f, float height = 36f)
        {
            float hw = height * 0.85f;      // handle width
            float trackH = height;          // track fills the full height (no vertical padding)

            var go = NewUI("Slider", parent);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.minWidth = width;
            le.preferredHeight = height; le.minHeight = height;
            var slider = go.AddComponent<UnityEngine.UI.Slider>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;

            var bg = NewUI("Background", go.transform);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f); bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f); bgRt.sizeDelta = new Vector2(0f, trackH);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = ButtonBg(); bgImg.type = Image.Type.Sliced; bgImg.color = Panel; bgImg.pixelsPerUnitMultiplier = 2f;

            // Fill Area spans the full track width so a maxed slider fully covers it
            // (no leftover track showing at the ends).
            var fillArea = NewUI("Fill Area", go.transform);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.5f); faRt.anchorMax = new Vector2(1f, 0.5f);
            faRt.pivot = new Vector2(0.5f, 0.5f); faRt.sizeDelta = new Vector2(0f, trackH);
            var fill = NewUI("Fill", fillArea.transform);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite = ButtonBg(); fillImg.type = Image.Type.Sliced; fillImg.color = Gold; fillImg.pixelsPerUnitMultiplier = 2f;

            var handleArea = NewUI("Handle Slide Area", go.transform);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
            haRt.pivot = new Vector2(0.5f, 0.5f); haRt.sizeDelta = new Vector2(-hw, 0f);
            var handle = NewUI("Handle", handleArea.transform);
            var hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(hw, 0f);
            var hImg = handle.AddComponent<Image>();
            hImg.sprite = ButtonBg(); hImg.type = Image.Type.Sliced; hImg.color = Text; hImg.pixelsPerUnitMultiplier = 2f;
            // Gold pixel border on the knob, matching the buttons.
            var hBorder = NewUI("Border", handle.transform);
            Stretch(hBorder.GetComponent<RectTransform>());
            var hbImg = hBorder.AddComponent<Image>();
            hbImg.sprite = Border(); hbImg.type = Image.Type.Sliced; hbImg.color = Gold;
            hbImg.pixelsPerUnitMultiplier = 2f; hbImg.raycastTarget = false;

            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = hImg;
            slider.value = Mathf.Clamp01(value);
            if (onChange != null) slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        // A small rounded badge (e.g. the in-game "TRAP · Easy"). Returns the label.
        public TMP_Text Pill(Transform parent, string text, float fontSize = 17f)
        {
            var go = NewUI("Pill", parent);
            var img = go.AddComponent<Image>();
            img.sprite = Fill(); img.type = Image.Type.Sliced; img.color = Panel;
            var h = go.AddComponent<HorizontalLayoutGroup>();
            // Tighter padding than a normal pill: the mode banner should read larger
            // (bigger font) without the badge itself growing.
            h.padding = new RectOffset(12, 12, 2, 2); h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            AddFitter(go);
            var lab = Label(go.transform, text, fontSize, Muted, FontStyles.Normal, 200);
            lab.GetComponent<LayoutElement>().preferredWidth = -1;
            lab.textWrappingMode = TextWrappingModes.NoWrap;
            return lab;
        }

        public TMP_InputField Input(Transform parent, string placeholder, int maxLen,
                                    float width = 240f, float height = 50f)
        {
            var go = NewUI("Input", parent);
            var img = go.AddComponent<Image>();
            img.sprite = Fill(); img.type = Image.Type.Sliced; img.color = Panel;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.minWidth = width;
            le.preferredHeight = height; le.minHeight = height;

            var input = go.AddComponent<TMP_InputField>();
            input.characterLimit = maxLen;

            var area = NewUI("TextArea", go.transform);
            var areaRt = area.GetComponent<RectTransform>();
            Stretch(areaRt);
            areaRt.offsetMin = new Vector2(12, 6); areaRt.offsetMax = new Vector2(-12, -6);
            area.AddComponent<RectMask2D>();

            var ph = Label(area.transform, placeholder, 18, Muted, FontStyles.Normal, width);
            Stretch(ph.rectTransform); ph.GetComponent<LayoutElement>().ignoreLayout = true;
            var txt = Label(area.transform, "", 18, Text, FontStyles.Normal, width);
            Stretch(txt.rectTransform); txt.GetComponent<LayoutElement>().ignoreLayout = true;

            input.textViewport = areaRt;
            input.textComponent = txt;
            input.placeholder = ph;
            input.onFocusSelectAll = false;
            return input;
        }

        // Toggle the gold outline + text colour for selectable tabs/buttons.
        public static void StyleToggle(Button btn, bool active)
        {
            var border = btn.transform.Find("Border");
            if (border) border.gameObject.SetActive(active);
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label) label.color = active ? Gold : Muted;
        }

        private static void AddFitter(GameObject go)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
