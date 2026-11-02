using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // Settings screen life (modern style), built only from effects the other screens use:
    //   - gold motes behind, and the menu's spinning 3D gear beside the title
    //   - sections pop in one after another when the screen opens
    //   - every button squashes when pressed (PressSquash)
    //   - flipping a toggle ON bursts gold sparks at its state label, with a tap of vibration
    //   - a volume slider's knob swells and glows while it moves
    //   - the body scrolls with a rubber-band edge and a JuicyScrollbar
    public partial class UIManager
    {
        private sealed class SliderJuice { public Slider slider; public RectTransform handle, glow; public float last = -1f, active; }

        private RectTransform _settingsFx, _settingsContent;
        private readonly List<SliderJuice> _settingsSliders = new List<SliderJuice>();

        // Called at the end of BuildPolishedSettings.
        private void SetupSettingsJuice(RectTransform content)
        {
            _settingsFx = null; _settingsContent = content; _settingsSliders.Clear();
            if (content == null) return;
            var frame = content.parent.parent as RectTransform;   // SettingsSections → Viewport → Frame
            var screen = frame != null ? frame.parent : null;
            if (frame == null || screen == null) return;

            AddShopMotes(screen, 14);

            // The menu's settings gear, turning beside the title.
            var title = frame.GetComponentInChildren<TMP_Text>();
            if (title != null)
            {
                float w = title.GetPreferredValues(title.text, 1000f, 100f).x;
                var holder = new GameObject("Gear", typeof(RectTransform));
                holder.transform.SetParent(title.rectTransform, false);
                var hrt = (RectTransform)holder.transform;
                hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(.5f, .5f);
                hrt.sizeDelta = new Vector2(34f, 34f); hrt.anchoredPosition = new Vector2(-(w * .5f + 26f), 0f);
                AddModelIcon(holder.transform, "IconModels/SettingsGear", 34f, spin: true);
            }

            foreach (var b in frame.GetComponentsInChildren<Button>(true))
                if (b.GetComponent<PressSquash>() == null) b.gameObject.AddComponent<PressSquash>();

            foreach (var s in content.GetComponentsInChildren<Slider>(true))
            {
                if (s.handleRect == null) continue;
                var g = new GameObject("KnobGlow", typeof(RectTransform), typeof(Image));
                g.transform.SetParent(s.handleRect.parent, false);
                g.transform.SetSiblingIndex(s.handleRect.GetSiblingIndex());
                var grt = (RectTransform)g.transform;
                grt.anchorMin = grt.anchorMax = new Vector2(0f, .5f);
                var img = g.GetComponent<Image>(); img.sprite = UISprites.SoftGlow(); img.raycastTarget = false;
                img.color = new Color(1f, .82f, .4f, 0f);
                _settingsSliders.Add(new SliderJuice { slider = s, handle = s.handleRect, glow = grt });
            }

            AddSettingsScrollbar(frame, content);
            _settingsFx = ShopFxLayer(screen);
        }

        private void AddSettingsScrollbar(RectTransform frame, RectTransform content)
        {
            var scroll = content.GetComponentInParent<ScrollRect>();
            if (scroll == null) return;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = .12f;

            // Same layout as the viewport, in the margin just right of it.
            var sbGo = new GameObject("SettingsScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbGo.transform.SetParent(frame, false);
            var rt = (RectTransform)sbGo.transform;
            var view = (RectTransform)scroll.transform;
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, .5f);
            rt.offsetMin = new Vector2(8f, view.offsetMin.y); rt.offsetMax = new Vector2(14f, view.offsetMax.y);
            sbGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, .06f);
            var area = new GameObject("SlidingArea", typeof(RectTransform));
            area.transform.SetParent(rt, false); UIFactory.Stretch((RectTransform)area.transform);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(area.transform, false);
            var hrt = (RectTransform)handle.transform; UIFactory.Stretch(hrt);
            var sb = sbGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = hrt; sb.targetGraphic = handle.GetComponent<Image>();
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            sbGo.AddComponent<JuicyScrollbar>().Setup(UIFactory.Gold, new Color(1f, 1f, 1f, .07f), 4f, 8f);
        }

        // From ShowSettings.
        private void SettingsOpened()
        {
            if (_settingsContent == null) return;
            PopInChildren(_settingsContent, .06f);
            var scroll = _settingsContent.GetComponentInParent<ScrollRect>();
            if (scroll != null) { scroll.velocity = Vector2.zero; scroll.verticalNormalizedPosition = 1f; }
        }

        // From the preference toggles: sparks on the state label when it turns on.
        private void SettingToggled(bool on)
        {
            if (_settingsFx == null || _settings == null || !_settings.activeSelf) return;
            Haptics.Play(Haptics.Select, Haptics.SelectAmp);
            if (!on) return;
            var btn = PressedButton();
            if (btn == null) return;
            var state = btn.Find("PreferenceState");
            SparkBurst(_settingsFx, state != null ? state.position : btn.position, 12, UIFactory.Gold, Color.white, .55f);
        }

        private void UpdateSettingsJuice()
        {
            if (_settings == null || !_settings.activeSelf || _settingsSliders.Count == 0) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            bool reduced = SaveData.IsReducedEffects();
            foreach (var j in _settingsSliders)
            {
                if (j.slider == null) continue;
                float v = j.slider.value;
                bool moving = j.last >= 0f && Mathf.Abs(v - j.last) > .0005f;
                j.last = v;
                j.active = Mathf.MoveTowards(j.active, moving ? 1f : 0f, dt * (moving ? 10f : 2.5f));
                float a = reduced ? 0f : j.active * j.active * (3f - 2f * j.active);
                j.handle.localScale = Vector3.one * (1f + .3f * a);
                j.glow.position = j.handle.position;
                j.glow.sizeDelta = j.handle.rect.size * (2.6f + .6f * a);
                j.glow.GetComponent<Image>().color = new Color(1f, .82f, .4f, .55f * a);
            }
        }
    }
}
