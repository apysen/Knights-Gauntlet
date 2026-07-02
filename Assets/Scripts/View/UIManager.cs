using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Builds and runs the screen flow: Menu -> HUD (+ Pause) -> GameOver, plus the
    // "Was I trapped?" snapshot, a Settings hub, Achievements, How-to and Patch
    // notes. Logic lives in GameManager; this constructs the UI and relays presses.
    public class UIManager : MonoBehaviour
    {
        private GameManager _gm;
        private GameTheme _theme;
        private UIFactory _ui;

        private GameObject _menu, _hud, _pause, _gameOver, _trapped, _settings, _ach, _howto, _patch, _toast, _market, _reviveRow, _countdown, _dailyIntro, _dailyReward;
        private TMP_Text _scoreText, _gameOverScore, _savedName, _menuWarn, _speedLabel, _modeBadge;
        private TMP_Text _nameSetLabel, _permWarn, _menuShillings, _marketBalance, _marketMsg, _goReward, _goBalance, _countdownText, _dailyIntroAttempts, _dailyRewardText;
        private Button _dailyBtn;
        private bool _checkingName;
        private TMP_Text _trappedVerdict, _trappedSub, _toastText;
        private RectTransform _howtoArt;
        private HowToArt _art;
        private RectTransform _enemyBarFill;
        private GameObject _enemyBarRoot;
        private TMP_Text _howtoTitle, _howtoBody, _howtoPager, _patchVer, _patchDate, _patchBody, _patchPager;
        private TMP_InputField _nameInput;
        private Button _trappedBtn, _howtoPrev, _howtoNext, _patchPrev, _patchNext, _reviveSpendBtn;
        private RectTransform _trappedContent, _achList, _marketList;
        private ScrollRect _trappedScroll;

        private GameMode _selMode = GameMode.Trap;
        private Difficulty _selDiff = Difficulty.Medium;
        private Button _trapBtn, _battleBtn;
        private readonly Dictionary<Difficulty, Button> _diffBtns = new Dictionary<Difficulty, Button>();

        private DeathAnalysis _analysis;
        private int _howtoPage, _patchPage;
        private Coroutine _toastCo;

        private static readonly Color CheckerA = UIFactory.Hex("#2D2D4E");
        private static readonly Color CheckerB = UIFactory.Hex("#1E1E35");
        private static readonly Color TrapCell = new Color(0.78f, 0.20f, 0.20f, 0.35f);
        private static readonly Color TrapGlyph = UIFactory.Hex("#CC3333");
        private static readonly Color PathCell = new Color(0.24f, 0.86f, 0.47f, 0.42f);
        private static readonly Color PathText = UIFactory.Hex("#D8FFE6");
        private static readonly Color KnightCell = new Color(0.94f, 0.75f, 0.25f, 0.20f);
        private static readonly Color BadgeBg = new Color(0.047f, 0.086f, 0.157f, 0.9f);
        private static readonly Color BadgeText = UIFactory.Hex("#BFEAFF");
        private const float CellPx = 54f;

        public void Init(GameManager gm, GameTheme theme)
        {
            _gm = gm; _theme = theme;
            _ui = new UIFactory(
                theme != null ? theme.uiFont : null,
                theme != null ? theme.uiPanelSprite : null,
                theme != null ? theme.uiBorderSprite : null,
                theme != null ? theme.uiCircleSprite : null);
            BuildMenu(); BuildHud(); BuildPause(); BuildGameOver(); BuildTrapped();
            BuildSettings(); BuildAchievements(); BuildHowTo(); BuildPatch(); BuildToast();
            BuildMarketplace();
            BuildCountdown();
            BuildDailyIntro();
            BuildDailyReward();
            ShowMenu();
        }

        private int _lastScoreShown = int.MinValue;
        private GameObject _xformBadge;
        private Button _cbToggle;
        private TMP_Text _cbToggleLabel;
        private TMP_Text _xformBadgeGlyph;
        private Image _xformBadgeRing;
        private void Update()
        {
            if (_hud != null && _hud.activeSelf && _scoreText != null && _gm.Score != _lastScoreShown)
            {
                _lastScoreShown = _gm.Score;
                _scoreText.text = _gm.Score.ToString();
            }
            if (_xformBadge != null && _hud != null && _hud.activeSelf)
            {
                bool xf = _gm.TransformActive;
                if (_xformBadge.activeSelf != xf) _xformBadge.SetActive(xf);
                if (xf)
                {
                    var form = _gm.PlayerForm;
                    Color col = UIFactory.Hex(GameConfig.FormColorHex(form));
                    _xformBadgeGlyph.text = GameConfig.Glyph(form);
                    _xformBadgeGlyph.color = col;
                    _xformBadgeRing.color = col;
                }
            }
            else if (_xformBadge != null && _xformBadge.activeSelf) _xformBadge.SetActive(false);
        }

        private Image MakeCircleImage(Transform parent, float size, Color color)
        {
            var go = new GameObject("Circle", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = _ui.Circle();
            img.color = color;
            return img;
        }

        // ── Visibility ───────────────────────────────────────────────────────
        private void HideAll()
        {
            _menu.SetActive(false); _hud.SetActive(false); _pause.SetActive(false);
            _gameOver.SetActive(false); _trapped.SetActive(false); _settings.SetActive(false);
            _ach.SetActive(false); _howto.SetActive(false); _patch.SetActive(false);
            if (_market != null) _market.SetActive(false);
            if (_countdown != null) _countdown.SetActive(false);
            if (_dailyIntro != null) _dailyIntro.SetActive(false);
            if (_dailyReward != null) _dailyReward.SetActive(false);
        }
        public void ShowMenu() { HideAll(); RefreshNameUI(); _menu.SetActive(true); }
        public void ShowHud()
        {
            HideAll();
            if (_modeBadge != null)
                _modeBadge.text = (_selMode == GameMode.Trap ? "TRAP" : "BATTLE") + " \u00B7 " + _selDiff.ToString();
            _hud.SetActive(true);
        }
        public void ShowPause() { HideAll(); _hud.SetActive(true); _pause.SetActive(true); }
        public void ShowGameOver(int score, bool isTrap, DeathAnalysis analysis, int runReward, int reviveCost, int shillings)
        {
            HideAll();
            _analysis = analysis;
            _gameOverScore.text = "Score: " + score;
            if (_goReward != null)
            {
                _goReward.text = runReward > 0 ? "+" + runReward + " shillings collected" : "";
                _goReward.gameObject.SetActive(runReward > 0);
            }
            _savedName.text = "Saved as " + SaveData.GetName();
            _trappedBtn.gameObject.SetActive(isTrap);

            bool canRevive = reviveCost > 0;
            if (_reviveRow != null) _reviveRow.SetActive(canRevive);
            if (canRevive && _reviveSpendBtn != null)
            {
                var lbl = _reviveSpendBtn.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = "Spend " + reviveCost;
                _reviveSpendBtn.interactable = shillings >= reviveCost;
            }
            if (_goBalance != null) { _goBalance.color = UIFactory.Muted; _goBalance.text = shillings + " shillings"; }
            _gameOver.SetActive(true);
        }

        // ── Menu ─────────────────────────────────────────────────────────────
        private void BuildMenu()
        {
            var canvas = _ui.CreateCanvas("Menu", 100);
            _menu = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 12f);

            var knight = _ui.Label(col, "\u265E", 44, UIFactory.Text, FontStyles.Bold);
            if (_theme != null && _theme.glyphFont != null) knight.font = _theme.glyphFont;
            _ui.Label(col, "KNIGHT'S GAUNTLET", 36, UIFactory.Gold, FontStyles.Bold);
            _ui.Label(col, "Move in L-shapes.\nSurvive the endless battlefield.", 15, UIFactory.Muted);

            var modeRow = _ui.Row(col);
            _trapBtn = _ui.Button(modeRow, "Trap\n<size=58%><color=#9098AD>Avoid obstacles</color></size>", () => SetMode(GameMode.Trap), UIFactory.Panel, UIFactory.Muted, 150, 68, 18);
            _battleBtn = _ui.Button(modeRow, "Battle\n<size=58%><color=#9098AD>Capture enemies</color></size>", () => SetMode(GameMode.Battle), UIFactory.Panel, UIFactory.Muted, 150, 68, 18);

            _speedLabel = _ui.Label(col, "SCROLL SPEED", 12, UIFactory.Muted);
            var diffRow1 = _ui.Row(col, 8f);
            _diffBtns[Difficulty.Easy] = _ui.Button(diffRow1, "<color=#3CDC78>\u25CF</color> Easy", () => SetDiff(Difficulty.Easy), UIFactory.Panel, UIFactory.Muted, 104, 48, 14);
            _diffBtns[Difficulty.Medium] = _ui.Button(diffRow1, "<color=#F0C040>\u25CF</color> Medium", () => SetDiff(Difficulty.Medium), UIFactory.Panel, UIFactory.Muted, 120, 48, 14);
            _diffBtns[Difficulty.Hard] = _ui.Button(diffRow1, "<color=#FF5A5A>\u25CF</color> Hard", () => SetDiff(Difficulty.Hard), UIFactory.Panel, UIFactory.Muted, 104, 48, 14);
            var diffRow2 = _ui.Row(col, 8f);
            _diffBtns[Difficulty.Grandmaster] = _ui.Button(diffRow2, "<color=#F0C040>\u25CF</color> Grandmaster", () => SetDiff(Difficulty.Grandmaster), UIFactory.Panel, UIFactory.Muted, 214, 48, 14);

            _nameSetLabel = _ui.Label(col, "", 14, UIFactory.Muted);
            _nameInput = _ui.Input(col, "ENTER NAME", 7);
            _permWarn = _ui.Label(col, "Choose carefully \u2014 this is your permanent name and can't be changed later.", 11, UIFactory.Amber);
            _menuWarn = _ui.Label(col, "", 12, UIFactory.Amber);

            _ui.Button(col, "PLAY", OnPlay, UIFactory.Gold, UIFactory.Bg, 230, 56, 21);
            _dailyBtn = _ui.Button(col, "Daily Challenge", () => OnDailyPressed(), UIFactory.Panel, UIFactory.Gold, 240, 46, 15);
            var btnRow = _ui.Row(col, 10f);
            _ui.Button(btnRow, "Leaderboard", () => _gm.ShowLeaderboard(), UIFactory.Panel, UIFactory.Gold, 150, 46, 14);
            _ui.Button(btnRow, "Settings", () => ShowSettings(), UIFactory.Panel, UIFactory.Gold, 150, 46, 14);

            // Shop entry — top-right corner of the menu. It's anchored manually
            // (no layout group here), so the size must be set on the RectTransform
            // directly — the Button's width/height args only apply inside a layout.
            var shop = _ui.Button(screen.transform, "Shop", () => ShowMarketplace(), UIFactory.Panel, UIFactory.Gold, 60, 32, 13);
            var shopRt = shop.GetComponent<RectTransform>();
            shopRt.anchorMin = shopRt.anchorMax = new Vector2(1f, 1f);
            shopRt.pivot = new Vector2(1f, 1f);
            shopRt.sizeDelta = new Vector2(60f, 32f);
            shopRt.anchoredPosition = new Vector2(-14f, -14f);

            // Shillings count, top-right, just to the left of the Shop button.
            _menuShillings = _ui.Label(screen.transform, "", 14, UIFactory.Gold, FontStyles.Bold);
            _menuShillings.alignment = TextAlignmentOptions.Right;
            _menuShillings.enableWordWrapping = false;
            var shillRt = _menuShillings.rectTransform;
            shillRt.anchorMin = shillRt.anchorMax = new Vector2(1f, 1f);
            shillRt.pivot = new Vector2(1f, 1f);
            shillRt.sizeDelta = new Vector2(200f, 32f);
            shillRt.anchoredPosition = new Vector2(-82f, -14f);

            SetMode(_selMode); SetDiff(_selDiff);
        }

        private void SetMode(GameMode m)
        {
            _selMode = m;
            UIFactory.StyleToggle(_trapBtn, m == GameMode.Trap);
            UIFactory.StyleToggle(_battleBtn, m == GameMode.Battle);
            if (_speedLabel != null) _speedLabel.text = m == GameMode.Trap ? "SCROLL SPEED" : "ENEMY SPEED";
        }
        private void SetDiff(Difficulty d)
        {
            _selDiff = d;
            foreach (var kv in _diffBtns) UIFactory.StyleToggle(kv.Value, kv.Key == d);
        }
        private void RefreshNameUI()
        {
            bool locked = SaveData.IsNameLocked();
            _nameSetLabel.gameObject.SetActive(locked);
            _nameInput.gameObject.SetActive(!locked);
            _permWarn.gameObject.SetActive(!locked);
            if (locked) _nameSetLabel.text = "Playing as <b>" + SaveData.GetName() + "</b>";
            _menuWarn.text = "";
            if (_menuShillings != null) _menuShillings.text = _gm.Shillings + " shillings";
            if (_dailyBtn != null)
            {
                var dl = _dailyBtn.GetComponentInChildren<TMP_Text>();
                if (dl != null) dl.text = "Daily Challenge (" + SaveData.DailyAttemptsLeft() + " left)";
            }
        }

        private void OnPlay()
        {
            if (_checkingName) return;
            // Name already chosen and locked — straight to play.
            if (SaveData.IsNameLocked()) { _gm.StartGame(_selMode, _selDiff); return; }

            var raw = (_nameInput.text ?? "").Trim();
            if (raw.Length > 7) raw = raw.Substring(0, 7);
            if (string.IsNullOrEmpty(raw)) { _menuWarn.color = UIFactory.Amber; _menuWarn.text = "Enter a name to play."; return; }
            if (!NameFilter.IsAllowed(raw)) { _menuWarn.color = UIFactory.Error; _menuWarn.text = "Please choose a different name."; return; }

            _checkingName = true;
            _menuWarn.color = UIFactory.Muted; _menuWarn.text = "Checking name\u2026";
            _gm.CheckNameAvailable(raw, available =>
            {
                _checkingName = false;
                if (!available)
                {
                    _menuWarn.color = UIFactory.Error;
                    _menuWarn.text = "That name's taken \u2014 pick another.";
                    return;
                }
                SaveData.SetName(raw);   // permanent from here on
                RefreshNameUI();
                _gm.StartGame(_selMode, _selDiff);
            });
        }

        // ── HUD ──────────────────────────────────────────────────────────────
        private void BuildHud()
        {
            var canvas = _ui.CreateCanvas("HUD", 90, new Vector2(640, 1138));
            _hud = canvas.gameObject;

            _scoreText = _ui.Label(canvas.transform, "0", 28, UIFactory.Gold, FontStyles.Bold, 200);
            var sRt = _scoreText.rectTransform;
            sRt.anchorMin = sRt.anchorMax = new Vector2(0, 1); sRt.pivot = new Vector2(0, 1);
            sRt.anchoredPosition = new Vector2(24, -20);
            _scoreText.alignment = TextAlignmentOptions.TopLeft;

            // Transform form badge: the enemy piece you've become, in a ring, just
            // below the score. Hidden unless transformed.
            _xformBadge = new GameObject("XformBadge", typeof(RectTransform));
            _xformBadge.transform.SetParent(canvas.transform, false);
            var xbRt = _xformBadge.GetComponent<RectTransform>();
            xbRt.anchorMin = xbRt.anchorMax = new Vector2(0, 1); xbRt.pivot = new Vector2(0, 1);
            xbRt.sizeDelta = new Vector2(50, 50);
            xbRt.anchoredPosition = new Vector2(26, -62);
            _xformBadgeRing = MakeCircleImage(_xformBadge.transform, 50, UIFactory.Gold);       // coloured ring
            MakeCircleImage(_xformBadge.transform, 42, new Color(0.08f, 0.08f, 0.14f, 0.96f));   // dark inner disc
            _xformBadgeGlyph = _ui.Label(_xformBadge.transform, "", 26, UIFactory.Gold, FontStyles.Bold, 50);
            if (_theme != null && _theme.glyphFont != null) _xformBadgeGlyph.font = _theme.glyphFont;
            var xgRt = _xformBadgeGlyph.rectTransform;
            xgRt.anchorMin = Vector2.zero; xgRt.anchorMax = Vector2.one; xgRt.offsetMin = Vector2.zero; xgRt.offsetMax = Vector2.zero;
            _xformBadgeGlyph.alignment = TextAlignmentOptions.Center;
            _xformBadgeGlyph.enableWordWrapping = false;
            _xformBadge.SetActive(false);

            _modeBadge = _ui.Pill(canvas.transform, "");
            var mRt = _modeBadge.transform.parent.GetComponent<RectTransform>();
            mRt.anchorMin = mRt.anchorMax = new Vector2(0.5f, 1f); mRt.pivot = new Vector2(0.5f, 1f);
            mRt.anchoredPosition = new Vector2(0, -18);

            var restart = _ui.Button(canvas.transform, "\u21BA", () => _gm.RestartGame(), UIFactory.Panel, UIFactory.Gold, 40, 40, 18);
            var rRt = restart.GetComponent<RectTransform>();
            rRt.anchorMin = rRt.anchorMax = new Vector2(1, 1); rRt.pivot = new Vector2(1, 1);
            rRt.sizeDelta = new Vector2(40, 40);
            rRt.anchoredPosition = new Vector2(-64, -16);

            var pause = _ui.Button(canvas.transform, "II", () => _gm.PauseGame(), UIFactory.Panel, UIFactory.Gold, 40, 40, 16);
            var pRt = pause.GetComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = new Vector2(1, 1); pRt.pivot = new Vector2(1, 1);
            pRt.sizeDelta = new Vector2(40, 40);
            pRt.anchoredPosition = new Vector2(-16, -16);

            // Enemy-move timer (battle only): "ENEMY MOVE" label + a filling bar.
            _enemyBarRoot = new GameObject("EnemyBar", typeof(RectTransform));
            _enemyBarRoot.transform.SetParent(canvas.transform, false);
            var barRt = _enemyBarRoot.GetComponent<RectTransform>();
            barRt.anchorMin = barRt.anchorMax = new Vector2(0.5f, 0); barRt.pivot = new Vector2(0.5f, 0);
            barRt.sizeDelta = new Vector2(320, 26); barRt.anchoredPosition = new Vector2(0, 16);

            var lbl = _ui.Label(_enemyBarRoot.transform, "ENEMY MOVE", 11, UIFactory.Muted);
            var lRt = lbl.rectTransform;
            lRt.anchorMin = lRt.anchorMax = new Vector2(0.5f, 1); lRt.pivot = new Vector2(0.5f, 1);
            lRt.anchoredPosition = new Vector2(0, 0);

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(_enemyBarRoot.transform, false);
            var trRt = track.GetComponent<RectTransform>();
            trRt.anchorMin = trRt.anchorMax = new Vector2(0.5f, 0); trRt.pivot = new Vector2(0.5f, 0);
            trRt.sizeDelta = new Vector2(300, 7); trRt.anchoredPosition = Vector2.zero;
            var trImg = track.AddComponent<Image>();
            trImg.sprite = _ui.Fill(); trImg.type = Image.Type.Sliced;
            trImg.color = new Color(1, 1, 1, 0.12f);

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(track.transform, false);
            _enemyBarFill = fill.GetComponent<RectTransform>();
            _enemyBarFill.anchorMin = new Vector2(0, 0); _enemyBarFill.anchorMax = new Vector2(0, 1);
            _enemyBarFill.pivot = new Vector2(0, 0.5f);
            _enemyBarFill.anchoredPosition = Vector2.zero; _enemyBarFill.sizeDelta = new Vector2(0, 0);
            var fImg = fill.AddComponent<Image>();
            fImg.sprite = _ui.Fill(); fImg.type = Image.Type.Sliced; fImg.color = UIFactory.Gold;

            _enemyBarRoot.SetActive(false);
        }

        // Show/update the enemy-move timer. Called every frame from GameManager.
        public void SetEnemyBar(bool show, float progress)
        {
            if (_enemyBarRoot == null) return;
            if (_enemyBarRoot.activeSelf != show) _enemyBarRoot.SetActive(show);
            if (!show || _enemyBarFill == null) return;
            var sd = _enemyBarFill.sizeDelta; sd.x = 300f * Mathf.Clamp01(progress); _enemyBarFill.sizeDelta = sd;
            var img = _enemyBarFill.GetComponent<Image>();
            if (img != null) img.color = EnemyBarColor(progress);
        }

        // Green → gold → orange → red as the next enemy move approaches.
        private static Color EnemyBarColor(float t)
        {
            t = Mathf.Clamp01(t);
            Color green = UIFactory.Hex("#3CDC78");
            Color gold = UIFactory.Hex("#F0C040");
            Color orange = UIFactory.Hex("#FF8A3C");
            Color red = UIFactory.Hex("#FF4040");
            if (t < 0.4f) return Color.Lerp(green, gold, t / 0.4f);
            if (t < 0.75f) return Color.Lerp(gold, orange, (t - 0.4f) / 0.35f);
            return Color.Lerp(orange, red, (t - 0.75f) / 0.25f);
        }

        // ── Pause ────────────────────────────────────────────────────────────
        private void BuildPause()
        {
            var canvas = _ui.CreateCanvas("Pause", 120);
            _pause = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, new Color(0.08f, 0.08f, 0.16f, 0.92f));
            var col = _ui.Column(screen.transform, 14f);
            _ui.Label(col, "PAUSED", 28, UIFactory.Gold, FontStyles.Bold);
            _ui.Button(col, "Resume", () => _gm.ResumeGame(), UIFactory.Gold, UIFactory.Bg);
            _ui.Button(col, "How to Play", () => ShowHowTo(returnToPause: true), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Restart", () => _gm.RestartGame(), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Main Menu", () => _gm.ToMenu(), UIFactory.Panel, UIFactory.Gold);
        }

        // ── Game over ────────────────────────────────────────────────────────
        private void BuildGameOver()
        {
            var canvas = _ui.CreateCanvas("GameOver", 110);
            _gameOver = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, new Color(0.08f, 0.08f, 0.16f, 0.95f));
            var col = _ui.Column(screen.transform, 12f);
            _ui.Label(col, "GAME OVER", 28, UIFactory.Gold, FontStyles.Bold);
            _gameOverScore = _ui.Label(col, "Score: 0", 24, UIFactory.Gold, FontStyles.Bold);
            _goReward = _ui.Label(col, "", 13, UIFactory.Gold);
            _savedName = _ui.Label(col, "", 14, UIFactory.Muted);
            _trappedBtn = _ui.Button(col, "Was I trapped?", () => ShowTrapped(), UIFactory.Panel, UIFactory.Gold, 220, 44, 14);

            _reviveRow = _ui.Column(col, 6f).gameObject;
            _ui.Label(_reviveRow.transform, "Extra life?", 13, UIFactory.Muted);
            var rr = _ui.Row(_reviveRow.transform, 8f);
            _reviveSpendBtn = _ui.Button(rr, "Spend 5", OnReviveSpend, UIFactory.Gold, UIFactory.Bg, 160, 42, 14);

            _goBalance = _ui.Label(col, "", 12, UIFactory.Muted);
            _ui.Button(col, "Restart", () => _gm.RestartGame(), UIFactory.Gold, UIFactory.Bg);
            _ui.Button(col, "Home", () => _gm.ToMenu(), UIFactory.Panel, UIFactory.Gold);
        }

        private void OnReviveSpend()
        {
            if (!_gm.TryReviveWithShillings())
            {
                _goBalance.color = UIFactory.Error;
                _goBalance.text = "Not enough shillings.";
            }
        }

        // ── Revive countdown overlay ─────────────────────────────────────────
        private void BuildCountdown()
        {
            var canvas = _ui.CreateCanvas("Countdown", 140);
            _countdown = canvas.gameObject;
            var holder = new GameObject("num", typeof(RectTransform));
            holder.transform.SetParent(canvas.transform, false);
            var rt = holder.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(240, 240);
            _countdownText = holder.AddComponent<TextMeshProUGUI>();
            if (_theme != null && _theme.uiFont != null) _countdownText.font = _theme.uiFont;
            _countdownText.text = "3";
            _countdownText.fontSize = 130;
            _countdownText.color = UIFactory.Gold;
            _countdownText.fontStyle = FontStyles.Bold;
            _countdownText.alignment = TextAlignmentOptions.Center;
            _countdownText.raycastTarget = false;
            _countdown.SetActive(false);
        }
        public void ShowCountdown(int n) { if (_countdownText != null) _countdownText.text = n.ToString(); if (_countdown != null) _countdown.SetActive(true); }
        public void SetCountdown(int n) { if (_countdownText != null) _countdownText.text = n.ToString(); }
        public void HideCountdown() { if (_countdown != null) _countdown.SetActive(false); }

        // ── Daily challenge intro (first time only) ──────────────────────────
        private void BuildDailyIntro()
        {
            var canvas = _ui.CreateCanvas("DailyIntro", 112);
            _dailyIntro = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, new Color(0.08f, 0.08f, 0.16f, 0.96f));
            var col = _ui.Column(screen.transform, 13f);
            _ui.Label(col, "Daily Challenge", 27, UIFactory.Gold, FontStyles.Bold);
            _ui.Label(col, "Everyone plays the SAME board each day \u2014 the exact same layout for every player.", 15, UIFactory.Text, FontStyles.Normal, 320);
            _ui.Label(col, "You get 10 attempts per day. They reset at midnight, and there are no extra lives.", 15, UIFactory.Text, FontStyles.Normal, 320);
            _ui.Label(col, "Finish in the daily top 5 to earn shillings.", 13, UIFactory.Muted, FontStyles.Normal, 320);
            _dailyIntroAttempts = _ui.Label(col, "", 15, UIFactory.Gold, FontStyles.Bold);
            _ui.Button(col, "Let's go", () => { MarkDailySeen(); _gm.StartDaily(); }, UIFactory.Gold, UIFactory.Bg, 220, 52, 16);
            _ui.Button(col, "Back", () => ShowMenu(), UIFactory.Panel, UIFactory.Gold, 220, 46, 14);
        }
        private void MarkDailySeen() { PlayerPrefs.SetInt("kg_seen_daily", 1); PlayerPrefs.Save(); }
        private void OnDailyPressed()
        {
            if (PlayerPrefs.GetInt("kg_seen_daily", 0) == 1) { _gm.StartDaily(); return; }
            HideAll();
            if (_dailyIntroAttempts != null)
                _dailyIntroAttempts.text = SaveData.DailyAttemptsLeft() + " attempts left today";
            _dailyIntro.SetActive(true);
        }

        // ── Daily challenge reward popup (placed top 5 yesterday) ─────────────
        private void BuildDailyReward()
        {
            var canvas = _ui.CreateCanvas("DailyReward", 116);
            _dailyReward = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, new Color(0.08f, 0.08f, 0.16f, 0.96f));
            var col = _ui.Column(screen.transform, 16f);
            _ui.Label(col, "Daily Challenge Results", 25, UIFactory.Gold, FontStyles.Bold);
            _dailyRewardText = _ui.Label(col, "", 17, UIFactory.Text, FontStyles.Normal, 330);
            _ui.Button(col, "Claim", () => ShowMenu(), UIFactory.Gold, UIFactory.Bg, 220, 52, 16);
        }
        public void ShowDailyReward(int place, int amount, string dateLabel)
        {
            HideAll();
            string when = string.IsNullOrEmpty(dateLabel)
                ? "your last Daily Challenge"
                : "the Daily Challenge on " + dateLabel;
            _dailyRewardText.text =
                "Congrats! You finished <b>" + Ordinal(place) + "</b> in " + when + ".\n\n" +
                "<size=140%><color=#F0C040><b>+" + amount + " shillings</b></color></size>";
            _dailyReward.SetActive(true);
        }
        private static string Ordinal(int n)
        {
            switch (n) { case 1: return "1st"; case 2: return "2nd"; case 3: return "3rd"; default: return n + "th"; }
        }

        // ── Settings hub ─────────────────────────────────────────────────────
        private void BuildSettings()
        {
            var canvas = _ui.CreateCanvas("Settings", 105);
            _settings = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 14f);
            _ui.Label(col, "Settings", 28, UIFactory.Gold, FontStyles.Bold);
            _ui.Button(col, "How to Play", () => ShowHowTo(returnToPause: false), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Achievements", () => ShowAchievements(), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Patch Notes", () => ShowPatch(), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Report a Bug", () => _gm.ReportBug(), UIFactory.Panel, UIFactory.Gold);
            _cbToggle = _ui.Button(col, "", () => ToggleColorblind(), UIFactory.Panel, UIFactory.Gold);
            _cbToggleLabel = _cbToggle.GetComponentInChildren<TMP_Text>();
            UpdateColorblindLabel();
            _ui.Button(col, "Back", () => ShowMenu(), UIFactory.Gold, UIFactory.Bg);
        }
        private void ToggleColorblind()
        {
            SaveData.SetColorblind(!SaveData.IsColorblind());
            UpdateColorblindLabel();
        }
        private void UpdateColorblindLabel()
        {
            if (_cbToggleLabel != null)
                _cbToggleLabel.text = "Colorblind Mode: " + (SaveData.IsColorblind() ? "On" : "Off");
        }
        private void ShowSettings() { HideAll(); _settings.SetActive(true); }

        // ── Marketplace ──────────────────────────────────────────────────────
        private void BuildMarketplace()
        {
            var canvas = _ui.CreateCanvas("Market", 108);
            _market = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 10f);
            _ui.Label(col, "Marketplace", 26, UIFactory.Gold, FontStyles.Bold);
            _marketBalance = _ui.Label(col, "", 15, UIFactory.Gold, FontStyles.Bold);
            _ui.Label(col, "Buy skins with shillings. Equip here or from Achievements.", 11, UIFactory.Muted);
            _marketList = _ui.Column(col, 7f);
            _marketMsg = _ui.Label(col, "", 12, UIFactory.Amber);
            _ui.Button(col, "Back", () => ShowMenu(), UIFactory.Gold, UIFactory.Bg);
        }
        private void ShowMarketplace() { HideAll(); RebuildMarket(); _market.SetActive(true); }
        private void RebuildMarket()
        {
            _marketBalance.text = _gm.Shillings + " shillings";
            _marketMsg.text = "";
            for (int i = _marketList.childCount - 1; i >= 0; i--) Destroy(_marketList.GetChild(i).gameObject);

            string equipped = SaveData.GetSkin();
            foreach (var s in Skins.Shop)
            {
                var row = _ui.Row(_marketList, 8f);

                var sw = new GameObject("sw", typeof(RectTransform));
                sw.transform.SetParent(row, false);
                sw.GetComponent<RectTransform>().sizeDelta = new Vector2(26, 26);
                var swLe = sw.AddComponent<LayoutElement>(); swLe.preferredWidth = 26; swLe.preferredHeight = 26;
                var swImg = sw.AddComponent<Image>(); swImg.sprite = _ui.Circle(); swImg.color = s.Color;

                _ui.Label(row, s.Name, 14, UIFactory.Text, FontStyles.Bold, 96);

                if (SaveData.OwnsShopSkin(s.Id))
                {
                    if (equipped == s.Id) _ui.Label(row, "Equipped", 12, UIFactory.Gold, FontStyles.Bold, 100);
                    else { var id = s.Id; _ui.Button(row, "Equip", () => { _gm.EquipSkin(id); RebuildMarket(); }, UIFactory.Panel, UIFactory.Gold, 100, 40, 13); }
                }
                else
                {
                    var info = s;
                    _ui.Button(row, s.Price + " sh", () => OnBuy(info), UIFactory.Gold, UIFactory.Bg, 100, 40, 13);
                }
            }
        }
        private void OnBuy(SkinInfo s)
        {
            if (_gm.BuyShopSkin(s)) RebuildMarket();
            else { _marketMsg.color = UIFactory.Error; _marketMsg.text = "Not enough shillings for " + s.Name + "."; }
        }

        // ── Achievements ─────────────────────────────────────────────────────
        private void BuildAchievements()
        {
            var canvas = _ui.CreateCanvas("Achievements", 106);
            _ach = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 12f);
            _ui.Label(col, "Achievements", 26, UIFactory.Gold, FontStyles.Bold);
            _ui.Label(col, "Earn skins by completing challenges on Hard or Grandmaster.", 12, UIFactory.Muted);
            _achList = _ui.Column(col, 16f);
            _ui.Button(col, "Back", () => ShowSettings(), UIFactory.Gold, UIFactory.Bg);
        }
        private void ShowAchievements() { HideAll(); RebuildAchievements(); _ach.SetActive(true); }
        private void RebuildAchievements()
        {
            for (int i = _achList.childCount - 1; i >= 0; i--) Destroy(_achList.GetChild(i).gameObject);
            string active = SaveData.GetSkin();

            AchRow("Classic Gold", "The piece you start with.", "gold", true, active == "gold");
            foreach (var d in Achievements.All)
                AchRow(d.SkinName + " — " + d.Name, d.Desc, d.SkinId, Achievements.IsUnlocked(d.Id), active == d.SkinId);
        }
        private void AchRow(string title, string desc, string skinId, bool unlocked, bool equipped)
        {
            var row = _ui.Row(_achList, 8f);
            var meta = _ui.Column(row, 2f);
            _ui.Label(meta, title, 14, unlocked ? UIFactory.Text : UIFactory.Muted, FontStyles.Bold, 220);
            _ui.Label(meta, desc, 11, UIFactory.Muted, FontStyles.Normal, 220);
            if (!unlocked) _ui.Label(row, "Locked", 12, UIFactory.Muted, FontStyles.Normal, 80);
            else if (equipped) _ui.Label(row, "Equipped", 12, UIFactory.Hex("#3CDC78"), FontStyles.Bold, 80);
            else _ui.Button(row, "Use", () => { SaveData.SetSkin(skinId); RebuildAchievements(); }, UIFactory.Gold, UIFactory.Bg, 80, 38, 13);
        }

        // ── How-to (paged) ───────────────────────────────────────────────────
        private bool _howtoReturnPause;
        private void BuildHowTo()
        {
            var canvas = _ui.CreateCanvas("HowTo", 107);
            _howto = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 12f);
            _howtoTitle = _ui.Label(col, "", 26, UIFactory.Gold, FontStyles.Bold);

            // diagram panel (fixed size so the column layout stays stable)
            var artGo = new GameObject("Art", typeof(RectTransform));
            artGo.transform.SetParent(col, false);
            _howtoArt = artGo.GetComponent<RectTransform>();
            _howtoArt.sizeDelta = new Vector2(270, 200);
            var artLe = artGo.AddComponent<LayoutElement>();
            artLe.preferredWidth = 270; artLe.preferredHeight = 200;
            var artBg = artGo.AddComponent<Image>();
            artBg.sprite = _ui.Fill(); artBg.type = Image.Type.Sliced;
            artBg.color = UIFactory.Hex("#0F1626");
            _art = new HowToArt(_ui, _theme);

            _howtoBody = _ui.Label(col, "", 15, UIFactory.Text, FontStyles.Normal, 330);
            var nav = _ui.Row(col, 14f);
            _howtoPrev = _ui.Button(nav, "<", () => FlipHowTo(-1), UIFactory.Panel, UIFactory.Gold, 58, 46, 20);
            _howtoPager = _ui.Label(nav, "", 12, UIFactory.Muted, FontStyles.Normal, 60);
            _howtoNext = _ui.Button(nav, ">", () => FlipHowTo(1), UIFactory.Panel, UIFactory.Gold, 58, 46, 20);
            _ui.Button(col, "Got it", () => { if (_howtoReturnPause) _gm.ShowPauseScreen(); else ShowSettings(); }, UIFactory.Gold, UIFactory.Bg);
        }
        private void ShowHowTo(bool returnToPause)
        {
            _howtoReturnPause = returnToPause; _howtoPage = 0;
            HideAll(); RenderHowTo(); _howto.SetActive(true);
        }
        private void FlipHowTo(int d)
        {
            _howtoPage = Mathf.Clamp(_howtoPage + d, 0, GameContent.HowTo.Length - 1);
            RenderHowTo();
        }
        private void RenderHowTo()
        {
            var p = GameContent.HowTo[_howtoPage];
            _howtoTitle.text = p.title; _howtoBody.text = p.body;
            if (_art != null && _howtoArt != null) _art.Build(_howtoPage, _howtoArt);
            _howtoPager.text = (_howtoPage + 1) + " / " + GameContent.HowTo.Length;
            _howtoPrev.interactable = _howtoPage > 0;
            _howtoNext.interactable = _howtoPage < GameContent.HowTo.Length - 1;
        }

        // ── Patch notes (paged) ──────────────────────────────────────────────
        private void BuildPatch()
        {
            var canvas = _ui.CreateCanvas("Patch", 108);
            _patch = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 10f);
            _ui.Label(col, "Patch Notes", 24, UIFactory.Gold, FontStyles.Bold);
            _patchVer = _ui.Label(col, "", 18, UIFactory.Gold, FontStyles.Bold);
            _patchDate = _ui.Label(col, "", 12, UIFactory.Muted);
            _patchBody = _ui.Label(col, "", 14, UIFactory.Text, FontStyles.Normal, 320);
            var nav = _ui.Row(col, 14f);
            _patchPrev = _ui.Button(nav, "<", () => FlipPatch(-1), UIFactory.Panel, UIFactory.Gold, 56, 44, 20);
            _patchPager = _ui.Label(nav, "", 12, UIFactory.Muted, FontStyles.Normal, 60);
            _patchNext = _ui.Button(nav, ">", () => FlipPatch(1), UIFactory.Panel, UIFactory.Gold, 56, 44, 20);
            _ui.Button(col, "Back", () => ShowSettings(), UIFactory.Gold, UIFactory.Bg);
        }
        private void ShowPatch() { _patchPage = 0; HideAll(); RenderPatch(); _patch.SetActive(true); }
        private void FlipPatch(int d)
        {
            _patchPage = Mathf.Clamp(_patchPage + d, 0, GameContent.Patches.Length - 1);
            RenderPatch();
        }
        private void RenderPatch()
        {
            var p = GameContent.Patches[_patchPage];
            _patchVer.text = "v" + p.version; _patchDate.text = p.date; _patchBody.text = p.notes;
            _patchPager.text = (_patchPage + 1) + " / " + GameContent.Patches.Length;
            _patchPrev.interactable = _patchPage > 0;
            _patchNext.interactable = _patchPage < GameContent.Patches.Length - 1;
        }

        // ── Achievement toast ────────────────────────────────────────────────
        private void BuildToast()
        {
            var canvas = _ui.CreateCanvas("Toast", 200);
            _toast = canvas.gameObject;
            var go = new GameObject("ToastBox", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -70); rt.sizeDelta = new Vector2(360, 64);
            var img = go.AddComponent<Image>(); img.color = new Color(0.07f, 0.086f, 0.157f, 0.96f);
            _toastText = _ui.Label(go.transform, "", 14, UIFactory.Text, FontStyles.Normal, 340);
            UIFactory.Stretch(_toastText.rectTransform);
            _toast.SetActive(false);
        }
        public void ShowToast(string message)
        {
            _toastText.text = message;
            _toast.SetActive(true);
            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(ToastRoutine());
        }
        private IEnumerator ToastRoutine()
        {
            yield return new WaitForSecondsRealtime(3.2f);
            _toast.SetActive(false);
        }

        // ── "Was I trapped?" snapshot ────────────────────────────────────────
        private void BuildTrapped()
        {
            var canvas = _ui.CreateCanvas("Trapped", 115);
            _trapped = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 10f);

            _trappedVerdict = _ui.Label(col, "", 30, UIFactory.Gold, FontStyles.Bold);
            _trappedSub = _ui.Label(col, "", 13, UIFactory.Muted, FontStyles.Normal, 320);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(col, false);
            var scrollLe = scrollGo.AddComponent<LayoutElement>();
            scrollLe.preferredWidth = GameConfig.Cols * CellPx + 8;
            scrollLe.preferredHeight = 460;
            _trappedScroll = scrollGo.AddComponent<ScrollRect>();
            _trappedScroll.horizontal = false; _trappedScroll.vertical = true;
            _trappedScroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            UIFactory.Stretch(vpRt);
            viewport.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.18f, 1f);
            viewport.AddComponent<RectMask2D>();
            _trappedScroll.viewport = vpRt;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _trappedContent = content.GetComponent<RectTransform>();
            _trappedContent.anchorMin = _trappedContent.anchorMax = new Vector2(0.5f, 1f);
            _trappedContent.pivot = new Vector2(0.5f, 1f);
            _trappedScroll.content = _trappedContent;

            _ui.Label(col, "Green = a path you could have taken (in order). Blue = your last moves.", 11, UIFactory.Muted, FontStyles.Normal, 320);
            _ui.Button(col, "Back", CloseTrapped, UIFactory.Panel, UIFactory.Gold, 200, 44, 14);
        }
        private void ShowTrapped() { Populate(_analysis); _gameOver.SetActive(false); _trapped.SetActive(true); }
        private void CloseTrapped() { _trapped.SetActive(false); _gameOver.SetActive(true); }

        private void Populate(DeathAnalysis a)
        {
            for (int i = _trappedContent.childCount - 1; i >= 0; i--) Destroy(_trappedContent.GetChild(i).gameObject);

            if (!a.Escaped)
            {
                _trappedVerdict.text = "Yep"; _trappedVerdict.color = UIFactory.Hex("#FF8A5C");
                _trappedSub.text = "Genuinely trapped — no legal jump out of that square.";
            }
            else if (a.Reason == EscapeReason.DeepBack)
            {
                _trappedVerdict.text = "Nope"; _trappedVerdict.color = UIFactory.Hex("#FFD060");
                _trappedSub.text = "Escapable — but the only line out doubled back through tiles already off-screen.";
            }
            else
            {
                _trappedVerdict.text = "Nope"; _trappedVerdict.color = UIFactory.Hex("#3CDC78");
                _trappedSub.text = "You had a way out. Here's the path.";
            }

            int relTop = 0, relBottom = 0;
            void Inc(int r) { if (r < relTop) relTop = r; if (r > relBottom) relBottom = r; }
            Inc(0);
            foreach (var s in a.Path) Inc(s.RelRow);
            foreach (var t in a.Trail) Inc(t.relRow);
            foreach (var tr in a.Traps) Inc(tr.relRow);
            relTop -= 1; relBottom += 1;
            if (relBottom - relTop > 44) relBottom = relTop + 44;
            int rows = relBottom - relTop + 1;

            _trappedContent.sizeDelta = new Vector2(GameConfig.Cols * CellPx, rows * CellPx);
            bool InRange(int r) => r >= relTop && r <= relBottom;

            for (int r = relTop; r <= relBottom; r++)
                for (int c = 0; c < GameConfig.Cols; c++)
                {
                    int parity = (((r + c) % 2) + 2) % 2;
                    Cell(c, r - relTop, parity == 0 ? CheckerA : CheckerB);
                }

            foreach (var t in a.Traps)
            {
                if (!InRange(t.relRow)) continue;
                Cell(t.col, t.relRow - relTop, TrapCell);
                GlyphCell(t.col, t.relRow - relTop, "\u2715", TrapGlyph, true);
            }
            for (int i = 0; i < a.Path.Count; i++)
            {
                var s = a.Path[i];
                if (!InRange(s.RelRow)) continue;
                Cell(s.Col, s.RelRow - relTop, PathCell);
                GlyphCell(s.Col, s.RelRow - relTop, (i + 1).ToString(), PathText, false);
            }
            // Connect the player's last-10 landings with a thin blue line.
            for (int i = 0; i + 1 < a.Trail.Count; i++)
            {
                var pa = a.Trail[i]; var pb = a.Trail[i + 1];
                if (!InRange(pa.relRow) || !InRange(pb.relRow)) continue;
                TrailLine(pa.col, pa.relRow - relTop, pb.col, pb.relRow - relTop);
            }
            Cell(a.PlayerCol, -relTop, KnightCell);
            GlyphCell(a.PlayerCol, -relTop, "\u265E", UIFactory.Gold, true);
            foreach (var tr in a.Trail)
            {
                if (!InRange(tr.relRow)) continue;
                Badge(tr.col, tr.relRow - relTop, tr.moveNum.ToString());
            }

            int knightRowIdx = -relTop;
            _trappedScroll.verticalNormalizedPosition = rows > 1
                ? Mathf.Clamp01(1f - (float)knightRowIdx / (rows - 1)) : 1f;
        }

        private void TrailLine(int c0, int r0, int c1, int r1)
        {
            Vector2 pa = new Vector2(c0 * CellPx + CellPx * 0.5f, -(r0 * CellPx) - CellPx * 0.5f);
            Vector2 pb = new Vector2(c1 * CellPx + CellPx * 0.5f, -(r1 * CellPx) - CellPx * 0.5f);
            Vector2 mid = (pa + pb) * 0.5f;
            Vector2 d = pb - pa;
            float len = d.magnitude;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var go = new GameObject("trailLine", typeof(RectTransform));
            go.transform.SetParent(_trappedContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(len, 3f);
            rt.anchoredPosition = mid;
            rt.localRotation = Quaternion.Euler(0, 0, ang);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.435f, 0.827f, 1f, 0.75f); // #6FD3FF
            img.raycastTarget = false;
        }

        private Image Cell(int col, int rowIdx, Color color)
        {
            var go = new GameObject("cell", typeof(RectTransform));
            go.transform.SetParent(_trappedContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(CellPx, CellPx);
            rt.anchoredPosition = new Vector2(col * CellPx, -rowIdx * CellPx);
            var img = go.AddComponent<Image>(); img.color = color;
            return img;
        }
        private void GlyphCell(int col, int rowIdx, string text, Color color, bool useGlyphFont)
        {
            var go = new GameObject("glyph", typeof(RectTransform));
            go.transform.SetParent(_trappedContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(CellPx, CellPx);
            rt.anchoredPosition = new Vector2(col * CellPx, -rowIdx * CellPx);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (useGlyphFont && _theme != null && _theme.glyphFont != null) t.font = _theme.glyphFont;
            else if (_ui.Font) t.font = _ui.Font;
            t.text = text; t.color = color; t.fontSize = CellPx * 0.5f;
            t.alignment = TextAlignmentOptions.Center; t.fontStyle = FontStyles.Bold;
        }
        private void Badge(int col, int rowIdx, string text)
        {
            var go = new GameObject("badge", typeof(RectTransform));
            go.transform.SetParent(_trappedContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(CellPx * 0.42f, CellPx * 0.32f);
            rt.anchoredPosition = new Vector2(col * CellPx + 3, -rowIdx * CellPx - 3);
            go.AddComponent<Image>().color = BadgeBg;
            var lab = new GameObject("n", typeof(RectTransform));
            lab.transform.SetParent(go.transform, false);
            UIFactory.Stretch(lab.GetComponent<RectTransform>());
            var t = lab.AddComponent<TextMeshProUGUI>();
            if (_ui.Font) t.font = _ui.Font;
            t.text = text; t.color = BadgeText; t.fontSize = CellPx * 0.2f;
            t.alignment = TextAlignmentOptions.Center; t.fontStyle = FontStyles.Bold;
        }
    }
}
