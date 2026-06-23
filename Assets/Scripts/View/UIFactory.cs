using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace KnightsGauntlet.View
{
    // Minimal helpers for building uGUI screens in code, so the screen logic in
    // UIManager stays readable. Everything funnels its font through `Font` (the
    // theme's uiFont) — leave it null to use the TMP default.
    public class UIFactory
    {
        public TMP_FontAsset Font;

        public static readonly Color Gold = Hex("#F0C040");
        public static readonly Color Bg = Hex("#1A1A2E");
        public static readonly Color Panel = Hex("#16213E");
        public static readonly Color Muted = Hex("#AAAAAA");
        public static readonly Color Text = Hex("#EEEEEE");
        public static readonly Color Amber = Hex("#FFB454");
        public static readonly Color Error = Hex("#E05050");

        public UIFactory(TMP_FontAsset font)
        {
            Font = font;
            EnsureEventSystem();
        }

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }
        }

        public Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 1422); // portrait reference
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // Full-screen background panel.
        public GameObject Screen(Transform parent, Color bg)
        {
            var go = NewUI("Screen", parent);
            Stretch(go.GetComponent<RectTransform>());
            var img = go.AddComponent<Image>();
            img.color = bg;
            return go;
        }

        // Centered vertical stack; children keep their preferred sizes.
        public RectTransform Column(Transform parent, float spacing = 14f)
        {
            var go = NewUI("Column", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            AddFitter(go);
            return rt;
        }

        public RectTransform Row(Transform parent, float spacing = 10f)
        {
            var go = NewUI("Row", parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
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
            t.enableWordWrapping = true;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            return t;
        }

        public Button Button(Transform parent, string text, Action onClick,
                             Color? fill = null, Color? textColor = null,
                             float width = 220f, float height = 50f, float fontSize = 20f)
        {
            var go = NewUI("Button", parent);
            var img = go.AddComponent<Image>();
            img.color = fill ?? Gold;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.minWidth = width;
            le.preferredHeight = height; le.minHeight = height;

            var label = Label(go.transform, text, fontSize, textColor ?? Bg, FontStyles.Bold, width);
            Stretch(label.rectTransform);
            label.GetComponent<LayoutElement>().ignoreLayout = true;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public TMP_InputField Input(Transform parent, string placeholder, int maxLen,
                                    float width = 240f, float height = 50f)
        {
            var go = NewUI("Input", parent);
            var img = go.AddComponent<Image>();
            img.color = Panel;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.minWidth = width;
            le.preferredHeight = height; le.minHeight = height;

            var input = go.AddComponent<TMP_InputField>();
            input.characterLimit = maxLen;

            var area = NewUI("TextArea", go.transform);
            var areaRt = area.GetComponent<RectTransform>();
            Stretch(areaRt);
            areaRt.offsetMin = new Vector2(10, 6);
            areaRt.offsetMax = new Vector2(-10, -6);
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

        // Recolor a button created above (used for the mode/difficulty toggles).
        public static void StyleToggle(Button btn, bool active)
        {
            var img = btn.GetComponent<Image>();
            if (img) img.color = active ? Gold : Panel;
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label) label.color = active ? Bg : Muted;
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
