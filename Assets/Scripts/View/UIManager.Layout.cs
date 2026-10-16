using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    public partial class UIManager
    {
        private void PreferenceState(Button button, string title, bool on)
        {
            if (button == null) return;
            UIFactory.StyleToggle(button, on);
            if (LegacyStyle) return;
            var label = button.GetComponentInChildren<TMP_Text>();
            label.text = title;
            label.alignment = TextAlignmentOptions.Left;
            label.rectTransform.offsetMin = new Vector2(16, 4);
            label.rectTransform.offsetMax = new Vector2(-62, -4);
            var state = button.transform.Find("PreferenceState");
            if (state == null)
            {
                var go = new GameObject("PreferenceState", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(button.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, .5f); rt.sizeDelta = new Vector2(48, -8); rt.anchoredPosition = new Vector2(-14, 0);
                var status = go.GetComponent<TextMeshProUGUI>();
                status.font = UIFactory.BodyFont(); status.fontSize = 13 * UIFactory.FontScale;
                status.alignment = TextAlignmentOptions.Right; status.raycastTarget = false;
                state = go.transform;
            }
            var value = state.GetComponent<TMP_Text>();
            value.text = on ? "ON" : "OFF"; value.color = on ? UIFactory.Gold : UIFactory.Muted;
        }

        private void BuildPolishedPause()
        {
            var canvas = _ui.CreateCanvas("Pause", 120);
            _pause = canvas.gameObject;
            var col = _ui.Column(_ui.Screen(canvas.transform, new Color(.03f, .055f, .11f, .97f)).transform, 12);
            _ui.Label(col, "PAUSED", 30, UIFactory.Gold, width: 300);
            MenuSpace(col, 12);
            MenuButton(col, "Resume", () => _gm.ResumeGame(), 300, 56, 20, primary: true);
            _pauseLevelSelectBtn = SettingAction(col, "Level Select", () => _gm.ExitAdventureToLevelSelect()).gameObject;
            _pauseLevelSelectBtn.SetActive(false);
            SettingAction(col, "How to Play", () => ShowHowTo(returnToPause: true));
            MenuSpace(col, 8);
            SettingCaption(col, "PREFERENCES");
            _pauseShakeToggle = SettingAction(col, "Screen shake", ToggleScreenShake);
            _pauseHapticsToggle = SettingAction(col, "Capture vibration", ToggleCaptureHaptics);
            _pauseEffectsToggle = SettingAction(col, "Reduce effects", ToggleReducedEffects);
            MenuSpace(col, 8);
            var actions = _ui.Row(col, 12);
            MenuButton(actions, "Restart", () => _gm.RestartGame(), 144, 48, 17);
            MenuButton(actions, "Main Menu", () => _gm.ToMenu(), 144, 48, 17);
            UpdateScreenShakeToggles();
        }

        private void BuildPolishedSettings()
        {
            var canvas = _ui.CreateCanvas("Settings", 105);
            _settings = canvas.gameObject;
            var content = SettingsPage(_ui.Screen(canvas.transform, UIFactory.Bg).transform);
            var account = SettingsSection(content, "Player");
            SettingAction(account, "Player Profile", ShowProfile);
            var audio = SettingsSection(content, "Audio");
            SettingCaption(audio, "Music volume");
            _ui.Slider(audio, SfxManager.MusicVolume / SfxManager.MaxMusicVolume,
                value => SfxManager.MusicVolume = value * SfxManager.MaxMusicVolume, 300, 28);
            SettingCaption(audio, "Sound effects volume");
            _ui.Slider(audio, SfxManager.SfxVolume / SfxManager.MaxSfxVolume,
                value => SfxManager.SfxVolume = value * SfxManager.MaxSfxVolume, 300, 28);
            var display = SettingsSection(content, "Display & comfort");
            _legacySettingsToggle = SettingAction(display, "", () => _gm.ToggleLegacy2D());
            _legacySettingsToggle.name = "LegacyModeToggle";
            RefreshLegacyViewToggle();
            _cbToggle = SettingAction(display, "Colorblind Mode", ToggleColorblind);
            _cbToggleLabel = _cbToggle.GetComponentInChildren<TMP_Text>();
            _shakeToggle = SettingAction(display, "Screen Shake", ToggleScreenShake);
            _hapticsToggle = SettingAction(display, "Capture Vibration", ToggleCaptureHaptics);
            _effectsToggle = SettingAction(display, "Reduce Effects", ToggleReducedEffects);
            UpdateColorblindLabel(); UpdateScreenShakeToggles();
            var help = SettingsSection(content, "Help & progress");
            SettingAction(help, "How to Play", () => ShowHowTo(returnToPause: false));
            SettingAction(help, "Tutorials", ShowTutorialPick);
            SettingAction(help, "Achievements", ShowAchievements);
            SettingAction(help, "Report a Bug", ShowBugReport);
            if (Debug.isDebugBuild) SettingAction(help, "Ad Test Suite", () => _gm.LaunchAdTestSuite());
        }

        // A fixed title and return action frame a scrollable settings body.
        // Only the body scrolls, keeping navigation reachable on short phones.
        private RectTransform SettingsPage(Transform parent)
        {
            var frame = new GameObject("SettingsFrame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(parent, false); UIFactory.Stretch(frame);
            frame.gameObject.AddComponent<UISettingsSafeArea>();
            var title = _ui.Label(frame, "SETTINGS", 30, UIFactory.Gold, width: 340);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = titleRt.pivot = new Vector2(.5f, 1);
            titleRt.sizeDelta = new Vector2(340, 48); titleRt.anchoredPosition = Vector2.zero;
            var back = MenuButton(frame, "Back", ShowMenu, 340, 52, 18);
            var backRt = (RectTransform)back.transform;
            backRt.anchorMin = backRt.anchorMax = backRt.pivot = new Vector2(.5f, 0);
            backRt.sizeDelta = new Vector2(340, 52);
            var viewport = new GameObject("SettingsViewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(frame, false);
            var viewRt = (RectTransform)viewport.transform; UIFactory.Stretch(viewRt);
            viewRt.offsetMin = new Vector2(0, 72); viewRt.offsetMax = new Vector2(0, -68);
            // A transparent graphic receives drags between the controls.
            viewport.AddComponent<Image>().color = Color.clear;
            var content = _ui.Column(viewRt, 16);
            content.name = "SettingsSections";
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 2, 8);
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewRt; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
            return content;
        }

        private RectTransform SettingsSection(Transform parent, string title)
        {
            var section = _ui.Column(parent, 10);
            section.name = title;
            var image = section.gameObject.AddComponent<Image>();
            image.sprite = _ui.Fill(); image.type = Image.Type.Sliced; image.color = UIFactory.Panel;
            image.raycastTarget = false;
            var group = section.GetComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(20, 20, 16, 18);
            var label = _ui.Label(section, title.ToUpperInvariant(), 13, UIFactory.Gold, width: 300);
            label.alignment = TextAlignmentOptions.Left; label.characterSpacing = 1.2f;
            return section;
        }

        private Button SettingAction(Transform parent, string title, Action action)
            => MenuButton(parent, title, action, 300, 48, 17);

        private void SettingCaption(Transform parent, string title)
        {
            var label = _ui.Label(parent, title, 14, UIFactory.Muted, width: 300);
            label.alignment = TextAlignmentOptions.Left;
        }
    }

    public sealed class UISettingsSafeArea : MonoBehaviour
    {
        private Rect _last;
        private Vector2 _screen;
        private void LateUpdate()
        {
            var safe = Screen.safeArea;
            var screen = new Vector2(Screen.width, Screen.height);
            if (safe == _last && screen == _screen) return;
            _last = safe; _screen = screen;
            var canvas = GetComponentInParent<Canvas>();
            float unit = Mathf.Max(.001f, canvas.scaleFactor);
            var rect = (RectTransform)transform;
            rect.offsetMin = new Vector2(safe.xMin / unit + 24, safe.yMin / unit + 96);
            rect.offsetMax = new Vector2(-(Screen.width - safe.xMax) / unit - 24,
                -(Screen.height - safe.yMax) / unit - 24);
            // Maintain the same content width as the title and footer.
            float scale = Mathf.Min(1, rect.rect.width / 340f);
            foreach (RectTransform child in rect)
            {
                if (child.name == "SettingsViewport")
                {
                    var content = child.GetComponent<ScrollRect>().content;
                    content.localScale = Vector3.one * scale;
                }
                else child.localScale = Vector3.one * scale;
            }
        }
    }
}
