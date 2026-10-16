using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace KnightsGauntlet.View
{
    // Helpers for building uGUI screens in code, now with rounded sprites and a
    // gold-outline toggle state to match the web look. Fonts funnel through Font.
    public partial class UIFactory
    {
        public TMP_FontAsset Font;
        public Sprite PanelSprite, BorderSprite, CircleSprite, ButtonSprite;
        public readonly System.Collections.Generic.List<Canvas> Canvases = new System.Collections.Generic.List<Canvas>();

        // Shared size adjustment for the display and body typefaces.
        public const float FontScale = 1.12f;

        // "Pixel Font by Thaleah": a TMP font asset built once at runtime from the
        // bundled TTF (there is no pre-baked .asset for it). Shared by every screen.
        private static TMP_FontAsset _pixelFont;
        private static bool _pixelFontTried;

        public static Color Gold => LegacyStyle ? new Color32(0xF0,0xC0,0x40,255) : new Color32(0xDD,0xBA,0x68,255);
        public static Color Bg => LegacyStyle ? new Color32(0x1A,0x1A,0x2E,255) : new Color32(0x08,0x0E,0x1D,255);
        public static Color Panel => LegacyStyle ? new Color32(0x16,0x21,0x3E,255) : new Color32(0x14,0x27,0x46,255);
        public static Color Muted => LegacyStyle ? new Color32(0x90,0x98,0xAD,255) : new Color32(0xA9,0xB5,0xCC,255);
        public static Color Text => LegacyStyle ? new Color32(0xEE,0xEE,0xEE,255) : new Color32(0xF6,0xED,0xD6,255);
        public static readonly Color Amber = Hex("#FFB454");
        public static Color Error => LegacyStyle ? new Color32(0xE0,0x50,0x50,255) : new Color32(0xB5,0x53,0x49,255);
        private static TMP_FontAsset _comicFont, _bodyFont;
        public static TMP_FontAsset ComicFont() => _comicFont != null ? _comicFont :
            (_comicFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF") ?? TMP_Settings.defaultFontAsset);
        public static TMP_FontAsset BodyFont() => _bodyFont != null ? _bodyFont :
            (_bodyFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF") ?? TMP_Settings.defaultFontAsset);

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
        public Sprite Fill() => PanelSprite != null ? PanelSprite : LegacyStyle ? UISprites.RoundedFill() : UISprites.RoyalPanel();
        public Sprite Border() => BorderSprite != null ? BorderSprite : LegacyStyle ? UISprites.PixelBorder() : UISprites.RoyalPanel(true);
        public Sprite Circle() => CircleSprite != null ? CircleSprite : UISprites.Circle();

        // Theme override or an antialiased outlined panel.
        public Sprite ButtonBg()
        {
            if (LegacyStyle) return LegacyButtonBg();
            return ButtonSprite != null ? ButtonSprite : Fill();
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

        // Explicit theme font or the bundled display face.
        public TMP_FontAsset ResolveFont() => Font != null ? Font : LegacyStyle ? PixelFont() : ComicFont();

        // Smooth chess glyphs for small UI symbols and missing custom sprites.
        private static TMP_FontAsset _chessFont;
        private static bool _chessFontTried;
        public static TMP_FontAsset ChessFont()
        {
            if (!_chessFontTried)
            {
                _chessFontTried = true;
                var ttf = Resources.Load<Font>("Fonts/DejaVuSans-Bold");
                if (ttf != null)
                {
                    _chessFont = TMP_FontAsset.CreateFontAsset(ttf);
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
            Canvases.Add(canvas);
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
            if (!LegacyStyle && parent.name == "Screen" && parent.parent != null &&
                parent.parent.GetComponent<Canvas>() != null && parent.parent.name != "Trapped")
                go.AddComponent<UIScreenFit>();
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
            var font = LegacyStyle ? ResolveFont() : size < 28f ? BodyFont() : ResolveFont();
            if (font) t.font = font;
            t.text = text; t.fontSize = size * FontScale; t.color = color; t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            return t;
        }

        // The legacy pixel argument selects the themed button sprite; false uses
        // the plain panel for compact icon and customization cells.
        // clickSfx: the sound played when the button is pressed. Defaults to the UI
        // "click" (Cursor 1) so every button is audible; pass a different Sfx for
        // special buttons (Play, adventure level, page turns) or null for silence.
        public Button Button(Transform parent, string text, Action onClick,
                             Color? fill = null, Color? textColor = null,
                             float width = 220f, float height = 50f, float fontSize = 20f,
                             bool outline = false, bool pixel = true,
                             Sfx? clickSfx = Sfx.TabSelect)
        {
            if (LegacyStyle) return LegacyButton(parent, text, onClick, fill, textColor, width, height, fontSize, outline, pixel, clickSfx);
            var go = NewUI("Button", parent);
            var img = go.AddComponent<Image>();
            img.sprite = pixel ? ButtonBg() : Fill(); img.type = Image.Type.Sliced;
            img.color = fill ?? Gold;
            var inkOutline = go.AddComponent<Outline>();
            inkOutline.effectColor = Hex("#111210");
            inkOutline.effectDistance = new Vector2(1f, -1f);
            inkOutline.useGraphicAlpha = false;
            // Smooth nine-sliced panel with a constant ink outline.
            img.pixelsPerUnitMultiplier = 1f;
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
            // The selection ring uses the same corner geometry as the panel.
            bimg.pixelsPerUnitMultiplier = 1f;
            border.SetActive(outline);

            var fillet = NewUI("GoldFillet", go.transform);
            Stretch(fillet.GetComponent<RectTransform>());
            var filletImage = fillet.AddComponent<Image>();
            filletImage.sprite = Border(); filletImage.type = Image.Type.Sliced;
            filletImage.color = new Color(Gold.r,Gold.g,Gold.b,.30f);
            filletImage.raycastTarget = false;

            var label = Label(go.transform, text, fontSize, textColor ?? Bg, FontStyles.Normal, width);
            label.font = BodyFont();
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var labelRt = label.rectTransform;
            Stretch(labelRt);
            // Keep lettering clear of the border.
            labelRt.offsetMin = new Vector2(12f, 4f);
            labelRt.offsetMax = new Vector2(-12f, -4f);
            label.GetComponent<LayoutElement>().ignoreLayout = true;
            // Shrink only when a translated or long label needs the space.
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize * FontScale;
            label.fontSizeMin = Mathf.Max(12f, fontSize * FontScale * 0.75f);

            btn.onClick.AddListener(() =>
            {
                if (clickSfx.HasValue) SfxManager.Play(clickSfx.Value);
                onClick?.Invoke();
            });
            return btn;
        }

        // Horizontal 0..1 slider using the same panel art and palette as the buttons.
        public UnityEngine.UI.Slider Slider(Transform parent, float value, Action<float> onChange,
                             float width = 300f, float height = 36f)
        {
            if (LegacyStyle) return LegacySlider(parent, value, onChange, width, height);
            float hw = height * 0.75f;
            float trackH = 6f;

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
            bgImg.sprite = BoardView.UnitSquare(); bgImg.color = Hex("#2A3D59");

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
            fillImg.sprite = BoardView.UnitSquare(); fillImg.color = Gold;

            var handleArea = NewUI("Handle Slide Area", go.transform);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = new Vector2(0, .5f); haRt.anchorMax = new Vector2(1, .5f);
            haRt.pivot = new Vector2(0.5f, 0.5f); haRt.sizeDelta = new Vector2(-hw, 0f);
            var handle = NewUI("Handle", handleArea.transform);
            var hRt = handle.GetComponent<RectTransform>();
            hRt.anchorMin = hRt.anchorMax = new Vector2(0, .5f);
            hRt.sizeDelta = new Vector2(hw, hw);
            var hImg = handle.AddComponent<Image>();
            hImg.sprite = Circle(); hImg.color = Gold;
            // Gold pixel border on the knob, matching the buttons.
            var hBorder = NewUI("Border", handle.transform);
            Stretch(hBorder.GetComponent<RectTransform>());
            var hbImg = hBorder.AddComponent<Image>();
            hbImg.sprite = Circle(); hbImg.color = Gold;
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
