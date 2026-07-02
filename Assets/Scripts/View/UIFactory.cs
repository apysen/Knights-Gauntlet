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
        public Sprite PanelSprite, BorderSprite, CircleSprite;

        public static readonly Color Gold = Hex("#F0C040");
        public static readonly Color Bg = Hex("#1A1A2E");
        public static readonly Color Panel = Hex("#16213E");
        public static readonly Color Muted = Hex("#9098AD");
        public static readonly Color Text = Hex("#EEEEEE");
        public static readonly Color Amber = Hex("#FFB454");
        public static readonly Color Error = Hex("#E05050");

        public UIFactory(TMP_FontAsset font, Sprite panel = null, Sprite border = null, Sprite circle = null)
        {
            Font = font; PanelSprite = panel; BorderSprite = border; CircleSprite = circle;
            EnsureEventSystem();
        }
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        // Sprite resolvers: use the theme's custom art if provided, else the
        // procedurally generated default. Everything visual is overridable.
        public Sprite Fill() => PanelSprite != null ? PanelSprite : UISprites.RoundedFill();
        public Sprite Border() => BorderSprite != null ? BorderSprite : UISprites.RoundedBorder();
        public Sprite Circle() => CircleSprite != null ? CircleSprite : UISprites.Circle();

        public void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
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
            if (Font) t.font = Font;
            t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = true; t.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            return t;
        }

        public Button Button(Transform parent, string text, Action onClick,
                             Color? fill = null, Color? textColor = null,
                             float width = 220f, float height = 50f, float fontSize = 20f,
                             bool outline = false)
        {
            var go = NewUI("Button", parent);
            var img = go.AddComponent<Image>();
            img.sprite = Fill(); img.type = Image.Type.Sliced;
            img.color = fill ?? Gold;
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
            border.SetActive(outline);

            var label = Label(go.transform, text, fontSize, textColor ?? Bg, FontStyles.Bold, width);
            Stretch(label.rectTransform);
            label.GetComponent<LayoutElement>().ignoreLayout = true;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        // A small rounded badge (e.g. the in-game "TRAP · Easy"). Returns the label.
        public TMP_Text Pill(Transform parent, string text, float fontSize = 12f)
        {
            var go = NewUI("Pill", parent);
            var img = go.AddComponent<Image>();
            img.sprite = Fill(); img.type = Image.Type.Sliced; img.color = Panel;
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 6, 6); h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            AddFitter(go);
            var lab = Label(go.transform, text, fontSize, Muted, FontStyles.Bold, 200);
            lab.GetComponent<LayoutElement>().preferredWidth = -1;
            lab.enableWordWrapping = false;
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
