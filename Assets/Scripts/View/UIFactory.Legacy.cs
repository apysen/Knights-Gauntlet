using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KnightsGauntlet.View
{
    public partial class UIFactory
    {
        public static bool LegacyStyle { get; set; }
        private static Sprite _legacyPixelButton;
        private static bool _legacyPixelButtonTried, _legacyChessFontTried;
        private static TMP_FontAsset _legacyChessFont;
        private Button LegacyButton(Transform parent, string text, Action onClick,
                             Color? fill = null, Color? textColor = null,
                             float width = 220f, float height = 50f, float fontSize = 20f,
                             bool outline = false, bool pixel = true,
                             Sfx? clickSfx = Sfx.TabSelect)
        {
            var go = NewUI("LegacyButton", parent);
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

        private UnityEngine.UI.Slider LegacySlider(Transform parent, float value, Action<float> onChange,
                             float width = 300f, float height = 36f)
        {
            float hw = height * 0.85f;      // handle width
            float trackH = height;          // track fills the full height (no vertical padding)

            var go = NewUI("LegacySlider", parent);
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

        private Sprite LegacyButtonBg()
        {
            if (ButtonSprite != null) return ButtonSprite;
            if (!_legacyPixelButtonTried)
            {
                _legacyPixelButtonTried = true;
                var all = Resources.LoadAll<Sprite>("UI/UI SIMPLE PIXEL UNSPLIT");
                foreach (var s in all)
                    if (s.name == "BAR1") { _legacyPixelButton = s; break; }
            }
            return _legacyPixelButton != null ? _legacyPixelButton : Fill();
        }

        public static TMP_FontAsset LegacyChessFont()
        {
            if (!_legacyChessFontTried)
            {
                _legacyChessFontTried = true;
                var ttf = Resources.Load<Font>("Fonts/PixelChess");
                if (ttf != null)
                {
                    _legacyChessFont = TMP_FontAsset.CreateFontAsset(ttf);
                    var fbTtf = Resources.Load<Font>("Fonts/DejaVuSans-Bold");
                    if (_legacyChessFont != null && fbTtf != null)
                    {
                        var fb = TMP_FontAsset.CreateFontAsset(fbTtf);
                        if (fb != null)
                        {
                            _legacyChessFont.fallbackFontAssetTable ??=
                                new System.Collections.Generic.List<TMP_FontAsset>();
                            _legacyChessFont.fallbackFontAssetTable.Add(fb);
                        }
                    }
                }
            }
            return _legacyChessFont;
        }
    }
}
