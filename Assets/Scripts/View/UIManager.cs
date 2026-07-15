using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Builds and runs the screen flow: Menu -> HUD (+ Pause) -> GameOver, plus the
    // "Was I trapped?" snapshot, a Settings hub, Achievements and How-to. Logic
    // lives in GameManager; this constructs the UI and relays presses.
    public class UIManager : MonoBehaviour
    {
        private GameManager _gm;
        private GameTheme _theme;
        private UIFactory _ui;

        private GameObject _menu, _hud, _pause, _gameOver, _trapped, _settings, _ach, _howto, _toast, _market, _reviveRow, _countdown, _dailyIntro, _dailyReward;
        private GameObject _bugReport;
        private TMP_InputField _bugInput;
        private TMP_Text _scoreText, _gameOverScore, _savedName, _menuWarn, _speedLabel, _modeBadge;
        private TMP_Text _nameSetLabel, _permWarn, _menuShillings, _marketBalance, _marketMsg, _goReward, _goBalance, _countdownText, _dailyIntroAttempts, _dailyRewardText;
        private Button _dailyBtn;
        private bool _checkingName;
        private TMP_Text _trappedVerdict, _trappedSub, _toastText;
        private RectTransform _howtoArt;
        private HowToArt _art;
        private RectTransform _enemyBarFill;
        private RectTransform _enemyBarRt;
        private Canvas _hudCanvas;
        private GameObject _enemyBarRoot;
        private TMP_Text _howtoTitle, _howtoBody, _howtoPager;
        private TMP_InputField _nameInput;
        private TMP_InputField _goNameInput;
        private GameObject _goClaimRow;
        private Button _trappedBtn, _howtoPrev, _howtoNext, _reviveSpendBtn, _reviveWatchBtn;
        private RectTransform _trappedContent, _achList, _marketList;
        private const int LootboxCost = 1000;
        // CS:GO-style case-opening screen.
        private GameObject _caseOpen, _caseContinueBtn;
        private RectTransform _caseReel, _caseViewport;
        private TMP_Text _caseResultLabel;
        private Coroutine _caseCo;
        private Coroutine _chestCo;                 // chest-open animation + scroll transition
        private int _pendingWin; private Prize _pendingWon; private bool _pendingDupe;
        private Image _winnerVfxImg;                // VFX image of the winning cell, animated once it lands
        private static readonly Color VfxCellColor = new Color(0.72f, 0.55f, 1f, 1f); // violet accent for VFX prizes

        // A single reel prize: either a shop skin or a movement VFX (never both).
        private struct Prize
        {
            public SkinInfo skin;
            public VfxInfo vfx;
            public bool IsVfx => vfx != null;
            public string Name => IsVfx ? vfx.Name : skin.Name;
            public Color Accent => IsVfx ? VfxCellColor : skin.Color;
        }
        private const float CaseCellW = 132f, CaseCellH = 150f, CaseViewW = 480f;
        private Scrollbar _tallScrollbar;
        private bool _settingTallBar;
        private bool _tallScrollUserChanged;
        private GameObject _marketOdds, _custom, _skinPopup, _skinPopupEquip, _skinPopupCatcher;
        private RectTransform _customGrid;
        private TMP_Text _skinPopupName, _skinPopupInfo;
        private SkinInfo _popupSkin;
        private VfxInfo _popupVfx;
        private bool _popupIsVfx;
        private int _customTab; // 0 = Skins, 1 = VFX
        private Button _tabSkins, _tabVfx;
        private int _shopTab; // 0 = Lootbox, 1 = Powerups
        private Button _tabLootbox, _tabPowerups;
        private RectTransform _shopLootbox, _shopPowerups;
        private GameObject _shillingsShop;
        private TMP_Text _shillingsShopBalance;
        private readonly TMP_Text[] _powerupOwned = new TMP_Text[3]; // owned-count labels per powerup
        private ScrollRect _trappedScroll;
        private RectTransform _trappedPanel;   // the centered column, uniformly scaled to fit the device
        private RectTransform _trappedCanvasRt; // the trapped canvas, for the visible-area size

        private GameMode _selMode = GameMode.Trap;
        private Difficulty _selDiff = Difficulty.Medium;
        private Button _trapBtn, _battleBtn;
        private readonly Dictionary<Difficulty, Button> _diffBtns = new Dictionary<Difficulty, Button>();
        private TMP_Text _gmText; // Grandmaster button text (recoloured on selection; crown stays gold)

        private DeathAnalysis _analysis;
        private static readonly int[] AllHowTo = { 0, 1, 2, 3, 4, 5 };
        private int[] _howtoPages = AllHowTo;   // which pages this showing walks through
        private int _howtoIdx;                  // index into _howtoPages
        private System.Action _howtoOnDone;     // for first-time tutorials: start the run on "Got it"
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
        private const float CellPx = 81f; // "Was I trapped?" grid cell size (drives glyphs/badges/trail too)

        public void Init(GameManager gm, GameTheme theme)
        {
            _gm = gm; _theme = theme;
            _ui = new UIFactory(
                theme != null ? theme.uiFont : null,
                theme != null ? theme.uiPanelSprite : null,
                theme != null ? theme.uiBorderSprite : null,
                theme != null ? theme.uiCircleSprite : null,
                theme != null ? theme.uiButtonSprite : null);
            BuildMenu(); BuildHud(); BuildPause(); BuildGameOver(); BuildTrapped();
            BuildSettings(); BuildBugReport(); BuildAchievements(); BuildHowTo(); BuildToast();
            BuildMarketplace();
            BuildLootboxOdds();
            BuildCaseOpen();
            BuildCustomization();
            BuildCountdown();
            BuildDailyIntro();
            BuildDailyReward();
            BuildTutorial();
            BuildTutorialPick();
            BuildAdventureResult();
            BuildAdventureIntro();
            BuildLevelSelect();
            ShowMenu();
        }

        private int _lastScoreShown = int.MinValue;
        private GameObject _xformBadge;
        private Button _cbToggle;
        private TMP_Text _cbToggleLabel;
        private Button _shakeToggle, _pauseShakeToggle;
        private GameObject _tutorial, _tutContinueBtn, _tutPick;
        private TMP_Text _tutCaption;
        private TMP_Text _advMovesLabel, _advResultInfo, _advResultTitle;
        private TMP_Text _advResultReward, _advResultShillings;
        private RectTransform _advResultStarRow;
        private Coroutine _advResultCo;
        private GameObject _advNextBtn, _pauseLevelSelectBtn;
        private LevelData _advNextLevel;
        private GameObject _advResult;
        private GameObject _advIntro, _levelSelect;
        private RectTransform _levelRow;
        private TMP_Text _levelPageLabel;
        private string[] _advWorlds;
        private int _advPage;
        private TMP_Text _xformBadgeGlyph;
        private Image _xformBadgeRing;
        // Safe-area layout: bars + the elements LayoutHud repositions each frame.
        private RectTransform _topBarRt, _bottomBarRt, _scoreRt, _advMovesRt, _xformBadgeRt, _restartRt, _pauseRt, _modeBadgeRt;
        // Queen-boss health bar ("THE QUEEN" + segments), shown only during a boss.
        private GameObject _bossHud;
        private RectTransform _bossHudRt;
        private Image[] _bossSegs;
        // In-game powerup activation buttons (Battle/Trap only).
        private Button _chronoBtn, _divineBtn;
        private RectTransform _chronoBtnRt, _divineBtnRt;
        private TMP_Text _chronoCount, _divineCount;
        private Image _chronoRadial, _divineRadial; // radial-wipe duration indicator
        // Divine Path activation button (adventure only).
        private Button _pathBtn; private RectTransform _pathBtnRt; private TMP_Text _pathCount; private Image _pathRadial;
        // Main-menu corner controls, brought down to clear notches/cameras (see LayoutMenu).
        private Canvas _menuCanvas;
        private RectTransform _menuShopRt, _menuCustomRt, _menuShillingsRt;
        // The HUD/menu layout is a pure function of the screen metrics, but writing an
        // anchoredPosition dirties the Canvas even when the value is unchanged, so
        // re-running it every frame forced a full UI rebuild every frame. Run it only when
        // the screen actually changes (rotation, resize, safe-area shift), plus once each
        // time a screen is shown.
        private int _layoutW, _layoutH;
        private Rect _layoutSafe;
        private bool _forceLayout = true;
        private bool ScreenChanged()
        {
            var safe = Screen.safeArea;
            if (!_forceLayout && Screen.width == _layoutW && Screen.height == _layoutH && safe == _layoutSafe)
                return false;
            _layoutW = Screen.width; _layoutH = Screen.height; _layoutSafe = safe;
            _forceLayout = false;
            return true;
        }

        private void Update()
        {
            if (_hud != null && _hud.activeSelf && _scoreText != null && _gm.Score != _lastScoreShown)
            {
                _lastScoreShown = _gm.Score;
                _scoreText.text = _gm.Score.ToString();
            }
            bool relayout = ScreenChanged();
            if (_hud != null && _hud.activeSelf)
            {
                if (relayout) LayoutHud();
                UpdatePowerupButtons();
            }
            if (_menu != null && _menu.activeSelf && relayout) LayoutMenu();
            if (_xformBadge != null && _hud != null && _hud.activeSelf)
            {
                bool xf = _gm.TransformActive;
                if (_xformBadge.activeSelf != xf) _xformBadge.SetActive(xf);
                // Only re-tint/re-letter on an actual form change: assigning TMP text
                // rebuilds its mesh even when the string is identical.
                if (xf && _gm.PlayerForm != _xformShown)
                {
                    _xformShown = _gm.PlayerForm;
                    Color col = UIFactory.Hex(GameConfig.FormColorHex(_xformShown));
                    _xformBadgeGlyph.text = GameConfig.Glyph(_xformShown);
                    _xformBadgeGlyph.color = col;
                    _xformBadgeRing.color = col;
                }
            }
            else if (_xformBadge != null && _xformBadge.activeSelf) _xformBadge.SetActive(false);
        }
        private PieceType _xformShown = (PieceType)(-1);

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
            _forceLayout = true;   // whichever screen comes up next gets one layout pass
            _menu.SetActive(false); _hud.SetActive(false); _pause.SetActive(false);
            _gameOver.SetActive(false); _trapped.SetActive(false); _settings.SetActive(false);
            _ach.SetActive(false); _howto.SetActive(false);
            if (_bugReport != null) _bugReport.SetActive(false);
            if (_market != null) _market.SetActive(false);
            if (_shillingsShop != null) _shillingsShop.SetActive(false);
            if (_caseOpen != null) _caseOpen.SetActive(false);
            if (_marketOdds != null) _marketOdds.SetActive(false);
            if (_custom != null) _custom.SetActive(false);
            if (_countdown != null) _countdown.SetActive(false);
            if (_dailyIntro != null) _dailyIntro.SetActive(false);
            if (_dailyReward != null) _dailyReward.SetActive(false);
            if (_tutorial != null) _tutorial.SetActive(false);
            if (_tutPick != null) _tutPick.SetActive(false);
            if (_advResult != null) _advResult.SetActive(false);
            if (_advIntro != null) _advIntro.SetActive(false);
            if (_levelSelect != null) _levelSelect.SetActive(false);
            // Banner off by default on every screen switch; each non-gameplay Show*
            // turns it back on right after calling HideAll. Only the actual play/pause
            // screens leave it off. (Leaderboard sits on top of the menu, so it inherits.)
            if (_gm != null) _gm.SetBanner(false);
        }
        public void ShowMenu() { HideAll(); RefreshNameUI(); _menu.SetActive(true); _gm.SetBanner(true); SfxManager.PlayMenuMusic(); }
        public void ShowHud(bool showBadge = true)
        {
            HideAll();
            _hud.SetActive(true);
            if (_modeBadge != null)
                _modeBadge.text = (_selMode == GameMode.Trap ? "TRAP" : "BATTLE") + " · " + _selDiff.ToString();
            if (_modeBadgeRt != null) _modeBadgeRt.gameObject.SetActive(showBadge); // daily hides it
            if (_scoreText != null) _scoreText.gameObject.SetActive(true);
            if (_advMovesLabel != null) _advMovesLabel.gameObject.SetActive(false);
            if (_tallScrollbar != null) _tallScrollbar.gameObject.SetActive(false);
        }
        // Tall-map preview scrollbar API.
        private void OnTallScrollChanged(float v) { if (!_settingTallBar) _tallScrollUserChanged = true; }
        public float TallScroll => _tallScrollbar != null ? _tallScrollbar.value : 0f;
        public bool ConsumeTallScrollInput() { bool c = _tallScrollUserChanged; _tallScrollUserChanged = false; return c; }
        public void SetTallScroll(float v)
        {
            if (_tallScrollbar == null) return;
            _settingTallBar = true; _tallScrollbar.value = Mathf.Clamp01(v); _settingTallBar = false;
        }
        public void ShowTallScroll(bool on, float handleSize)
        {
            if (_tallScrollbar == null) return;
            if (on) _tallScrollbar.size = Mathf.Clamp(handleSize, 0.1f, 1f);
            _tallScrollbar.gameObject.SetActive(on);
            _tallScrollUserChanged = false;
        }
        public void ShowPause(bool adventure = false) { HideAll(); _hud.SetActive(true); _pause.SetActive(true); if (_pauseLevelSelectBtn != null) _pauseLevelSelectBtn.SetActive(adventure); UpdateScreenShakeToggles(); }
        public void ShowGameOver(int score, bool isTrap, DeathAnalysis analysis, int runReward, int reviveCost, int shillings, bool adLife = false)
        {
            HideAll();
            _gm.SetBanner(true);
            _analysis = analysis;
            _gameOverScore.text = "Score: " + score;
            if (_goReward != null)
            {
                _goReward.text = runReward > 0 ? "+" + runReward + " shillings collected" : "";
                _goReward.gameObject.SetActive(runReward > 0);
            }
            string goName = SaveData.GetName();
            bool anon = string.IsNullOrEmpty(goName);
            if (_goClaimRow != null) _goClaimRow.SetActive(anon);
            if (anon)
            {
                _savedName.color = UIFactory.Muted;
                _savedName.text = "Enter a name to save this score:";
                if (_goNameInput != null) _goNameInput.text = "";
            }
            else
            {
                _savedName.color = UIFactory.Hex("#3CDC78");
                _savedName.text = "Saved as " + goName;
            }
            _trappedBtn.gameObject.SetActive(isTrap);

            bool canRevive = reviveCost > 0;
            if (_reviveRow != null) _reviveRow.SetActive(canRevive);
            if (canRevive && _reviveSpendBtn != null)
            {
                var lbl = _reviveSpendBtn.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = "Spend " + reviveCost;
                _reviveSpendBtn.interactable = shillings >= reviveCost;
            }
            // The free ad life is offered once per run, and only when an ad is loaded.
            // Otherwise the row falls back to the shilling revive on its own.
            if (_reviveWatchBtn != null) _reviveWatchBtn.gameObject.SetActive(canRevive && adLife);
            if (_goBalance != null) { _goBalance.color = UIFactory.Muted; _goBalance.text = shillings + " shillings"; }
            _gameOver.SetActive(true);
        }

        // ── Menu ─────────────────────────────────────────────────────────────
        private void BuildMenu()
        {
            var canvas = _ui.CreateCanvas("Menu", 100);
            _menu = canvas.gameObject;
            _menuCanvas = canvas;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 12f);

            var knight = _ui.Label(col, "\u265E", 90, UIFactory.Text, FontStyles.Normal);
            if (_theme != null && _theme.glyphFont != null) knight.font = _theme.glyphFont;
            _ui.Label(col, "KNIGHT'S GAUNTLET", 38, UIFactory.Gold, FontStyles.Normal);

            var modeRow = _ui.Row(col);
            _trapBtn = _ui.Button(modeRow, "Trap\n<size=58%><color=#9098AD>Avoid obstacles</color></size>", () => SetMode(GameMode.Trap), UIFactory.Panel, UIFactory.Muted, 150, 68, 20);
            _battleBtn = _ui.Button(modeRow, "Battle\n<size=58%><color=#9098AD>Capture enemies</color></size>", () => SetMode(GameMode.Battle), UIFactory.Panel, UIFactory.Muted, 150, 68, 20);

            _ui.Button(col, "Adventure", () => OnAdventurePressed(), UIFactory.Panel, UIFactory.Muted, 310, 60, 20);

            _speedLabel = _ui.Label(col, "SCROLL SPEED", 14, UIFactory.Muted);
            var diffRow1 = _ui.Row(col, 8f);
            _diffBtns[Difficulty.Easy] = _ui.Button(diffRow1, "<color=#3CDC78>\u25CF</color> Easy", () => SetDiff(Difficulty.Easy), UIFactory.Panel, UIFactory.Muted, 104, 48, 16);
            _diffBtns[Difficulty.Medium] = _ui.Button(diffRow1, "<color=#F0C040>\u25CF</color> Medium", () => SetDiff(Difficulty.Medium), UIFactory.Panel, UIFactory.Muted, 120, 48, 16);
            _diffBtns[Difficulty.Hard] = _ui.Button(diffRow1, "<color=#FF5A5A>\u25CF</color> Hard", () => SetDiff(Difficulty.Hard), UIFactory.Panel, UIFactory.Muted, 104, 48, 16);
            var diffRow2 = _ui.Row(col, 8f);
            // Empty button label \u2014 the visible "\u2654 Grandmaster" is a centred crown+text
            // group built below. The empty label still absorbs StyleToggle's recolour
            // (so the crown keeps its own colour); the text is recoloured in SetDiff.
            _diffBtns[Difficulty.Grandmaster] = _ui.Button(diffRow2, "", () => SetDiff(Difficulty.Grandmaster), UIFactory.Panel, UIFactory.Muted, 214, 48, 16);
            BuildGrandmasterLabel(_diffBtns[Difficulty.Grandmaster]);

            _nameSetLabel = _ui.Label(col, "", 16, UIFactory.Muted);
            _nameInput = _ui.Input(col, "NAME (OPTIONAL)", 7);
            _permWarn = _ui.Label(col, "If you would like to save your scores, please enter a name. (It's permanent once set.)", 13, UIFactory.Amber);
            _menuWarn = _ui.Label(col, "", 14, UIFactory.Amber);

            _ui.Button(col, "PLAY", OnPlay, UIFactory.Gold, UIFactory.Bg, 230, 56, 23, clickSfx: Sfx.PlaySelect);
            _dailyBtn = _ui.Button(col, "Daily Challenge", () => OnDailyPressed(), UIFactory.Panel, UIFactory.Gold, 240, 46, 17);
            var btnRow = _ui.Row(col, 10f);
            _ui.Button(btnRow, "Leaderboard", () => _gm.ShowLeaderboard(), UIFactory.Panel, UIFactory.Gold, 150, 46, 16);
            _ui.Button(btnRow, "Settings", () => ShowSettings(), UIFactory.Panel, UIFactory.Gold, 150, 46, 16);

            // Shop entry, top-right corner of the menu. It's anchored manually
            // (no layout group here), so the size must be set on the RectTransform
            // directly, because the Button's width/height args only apply inside a layout.
            var shop = _ui.Button(screen.transform, "Shop", () => ShowMarketplace(), UIFactory.Panel, UIFactory.Gold, 60, 32, 15);
            _menuShopRt = shop.GetComponent<RectTransform>();
            _menuShopRt.anchorMin = _menuShopRt.anchorMax = new Vector2(1f, 1f);
            _menuShopRt.pivot = new Vector2(1f, 1f);
            _menuShopRt.sizeDelta = new Vector2(60f, 32f);
            _menuShopRt.anchoredPosition = new Vector2(-14f, -14f); // repositioned by LayoutMenu

            // Customization entry, top-left corner (mirrors the Shop anchoring).
            var custom = _ui.Button(screen.transform, "Customize", () => ShowCustomization(), UIFactory.Panel, UIFactory.Gold, 108, 32, 15);
            _menuCustomRt = custom.GetComponent<RectTransform>();
            _menuCustomRt.anchorMin = _menuCustomRt.anchorMax = new Vector2(0f, 1f);
            _menuCustomRt.pivot = new Vector2(0f, 1f);
            _menuCustomRt.sizeDelta = new Vector2(108f, 32f);
            _menuCustomRt.anchoredPosition = new Vector2(14f, -14f); // repositioned by LayoutMenu

            // Shillings count, top-right, just to the left of the Shop button.
            _menuShillings = _ui.Label(screen.transform, "", 20, UIFactory.Gold, FontStyles.Normal);
            _menuShillings.alignment = TextAlignmentOptions.Right;
            _menuShillings.textWrappingMode = TextWrappingModes.NoWrap;
            _menuShillingsRt = _menuShillings.rectTransform;
            _menuShillingsRt.anchorMin = _menuShillingsRt.anchorMax = new Vector2(1f, 1f);
            _menuShillingsRt.pivot = new Vector2(1f, 1f);
            _menuShillingsRt.sizeDelta = new Vector2(200f, 32f);
            _menuShillingsRt.anchoredPosition = new Vector2(-82f, -14f); // repositioned by LayoutMenu

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
            // The Grandmaster text lives in a custom crown+text group, so recolour it
            // here (StyleToggle only touches the button's own, empty, label).
            if (_gmText != null) _gmText.color = d == Difficulty.Grandmaster ? UIFactory.Gold : UIFactory.Muted;
        }

        // Builds the "♔ Grandmaster" label as a centred horizontal group: a gold crown
        // glyph (chess glyph font, since U+2654 isn't in the pixel UI font; not bold)
        // beside the bold difficulty text, mirroring the "● Easy/Medium/Hard" buttons.
        private void BuildGrandmasterLabel(Button btn)
        {
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(btn.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            var hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 7f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var fit = content.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var crown = _ui.Label(content.transform, "♔", 22, UIFactory.Gold, FontStyles.Normal, 40);
            if (_theme != null && _theme.glyphFont != null) crown.font = _theme.glyphFont;
            crown.alignment = TextAlignmentOptions.Center;
            crown.textWrappingMode = TextWrappingModes.NoWrap; crown.raycastTarget = false;
            crown.GetComponent<LayoutElement>().preferredWidth = -1;

            _gmText = _ui.Label(content.transform, "Grandmaster", 16, UIFactory.Muted, FontStyles.Normal, 200);
            _gmText.alignment = TextAlignmentOptions.Center;
            _gmText.textWrappingMode = TextWrappingModes.NoWrap; _gmText.raycastTarget = false;
            _gmText.GetComponent<LayoutElement>().preferredWidth = -1;
        }
        private void RefreshNameUI()
        {
            bool locked = SaveData.IsNameLocked();
            _nameSetLabel.gameObject.SetActive(locked);
            _nameInput.gameObject.SetActive(!locked);
            _permWarn.gameObject.SetActive(!locked);
            if (locked) _nameSetLabel.text = "Playing as " + SaveData.GetName();
            // Empty warning is hidden (not just blank) so, when the name is locked,
            // "Playing as…" sits centered between the Grandmaster and Play buttons
            // instead of being pushed up by an invisible spacer row.
            SetMenuWarn("", UIFactory.Muted);
            if (_menuShillings != null) _menuShillings.text = _gm.Shillings + " shillings";
            if (_dailyBtn != null)
            {
                var dl = _dailyBtn.GetComponentInChildren<TMP_Text>();
                if (dl != null) dl.text = "Daily Challenge (" + SaveData.DailyAttemptsLeft() + " left)";
            }
        }

        // Set the menu warning line, hiding it entirely when blank so it adds no
        // layout space (keeps "Playing as…" centered between the buttons).
        private void SetMenuWarn(string text, Color color)
        {
            if (_menuWarn == null) return;
            _menuWarn.text = text;
            _menuWarn.color = color;
            _menuWarn.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private void OnPlay()
        {
            if (_checkingName) return;
            // Name already chosen and locked, so straight to play.
            if (SaveData.IsNameLocked()) { StartWithTutorial(_selMode, _selDiff); return; }

            var raw = (_nameInput.text ?? "").Trim();
            if (raw.Length > 7) raw = raw.Substring(0, 7);

            // The name is optional. Empty → just play (scores won't be saved).
            if (string.IsNullOrEmpty(raw)) { StartWithTutorial(_selMode, _selDiff); return; }

            // A name was entered, so validate and claim it (permanent), then play.
            if (!NameFilter.IsAllowed(raw)) { SetMenuWarn("Please choose a different name.", UIFactory.Error); return; }

            _checkingName = true;
            SetMenuWarn("Checking name\u2026", UIFactory.Muted);
            _gm.CheckNameAvailable(raw, available =>
            {
                _checkingName = false;
                if (!available)
                {
                    SetMenuWarn("That name's taken. Pick another.", UIFactory.Error);
                    return;
                }
                SaveData.SetName(raw);   // permanent from here on
                RefreshNameUI();
                StartWithTutorial(_selMode, _selDiff);
            });
        }

        // First time a player opens Trap or Battle, walk the mode's tutorial pages,
        // then start the run. Subsequent runs (and restarts) skip straight to play.
        private void StartWithTutorial(GameMode mode, Difficulty diff)
        {
            if (mode == GameMode.Trap && PlayerPrefs.GetInt("kg_seen_trap", 0) == 0)
            {
                _gm.StartTrapTutorial(diff, replay: false); // interactive; sets the flag on finish
                return;
            }
            if (mode == GameMode.Battle && PlayerPrefs.GetInt("kg_seen_battle", 0) == 0)
            {
                _gm.StartBattleTutorial(diff, replay: false); // interactive; sets the flag on finish
                return;
            }
            _gm.StartGame(mode, diff);
        }

        // ── HUD ──────────────────────────────────────────────────────────────
        private void BuildHud()
        {
            var canvas = _ui.CreateCanvas("HUD", 90, new Vector2(HudRefW, HudRefH));
            _hudCanvas = canvas;
            _hud = canvas.gameObject;

            // Solid bars top and bottom. Created FIRST so they sit behind the HUD content.
            // The board is letterboxed between them (see HudTopInsetFrac/HudBottomInsetFrac),
            // and their heights + the content positions are set every frame by LayoutHud so
            // they follow the device safe area (notch / control-center swipe / gesture bar).
            _topBarRt = MakeHudBar(canvas.transform, top: true);
            _bottomBarRt = MakeHudBar(canvas.transform, top: false);

            _scoreText = _ui.Label(canvas.transform, "0", 36, UIFactory.Gold, FontStyles.Normal, 200);
            _scoreRt = _scoreText.rectTransform;
            _scoreRt.anchorMin = _scoreRt.anchorMax = new Vector2(0, 1); _scoreRt.pivot = new Vector2(0, 1);
            _scoreRt.anchoredPosition = new Vector2(24, -20); // repositioned by LayoutHud
            _scoreText.alignment = TextAlignmentOptions.TopLeft;

            // Adventure move counter + level name (top-center); hidden outside adventure.
            _advMovesLabel = _ui.Label(canvas.transform, "", 20, UIFactory.Gold, FontStyles.Normal);
            _advMovesLabel.alignment = TextAlignmentOptions.Top; _advMovesLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _advMovesRt = _advMovesLabel.rectTransform;
            _advMovesRt.anchorMin = _advMovesRt.anchorMax = new Vector2(0.5f, 1f); _advMovesRt.pivot = new Vector2(0.5f, 1f);
            _advMovesRt.sizeDelta = new Vector2(360, 60); _advMovesRt.anchoredPosition = new Vector2(0, -18); // repositioned by LayoutHud
            _advMovesLabel.gameObject.SetActive(false);

            BuildBossHud(canvas.transform);

            // Mode/difficulty badge (top-centre), pixel-art pill matching the buttons/bars.
            // Positioned in line with the score row by LayoutHud; hidden in adventure/daily.
            var badgeGo = new GameObject("ModeBadge", typeof(RectTransform), typeof(Image),
                                         typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            badgeGo.transform.SetParent(canvas.transform, false);
            _modeBadgeRt = badgeGo.GetComponent<RectTransform>();
            _modeBadgeRt.anchorMin = _modeBadgeRt.anchorMax = new Vector2(0.5f, 1f); _modeBadgeRt.pivot = new Vector2(0.5f, 1f);
            _modeBadgeRt.anchoredPosition = new Vector2(0, -18); // repositioned by LayoutHud
            var badgeImg = badgeGo.GetComponent<Image>();
            badgeImg.sprite = _ui.ButtonBg(); badgeImg.type = Image.Type.Sliced; badgeImg.color = UIFactory.Panel;
            badgeImg.pixelsPerUnitMultiplier = 2f; badgeImg.raycastTarget = false;
            var badgeHlg = badgeGo.GetComponent<HorizontalLayoutGroup>();
            badgeHlg.padding = new RectOffset(16, 16, 5, 5); badgeHlg.childAlignment = TextAnchor.MiddleCenter;
            badgeHlg.childControlWidth = true; badgeHlg.childControlHeight = true;
            badgeHlg.childForceExpandWidth = false; badgeHlg.childForceExpandHeight = false;
            var badgeFit = badgeGo.GetComponent<ContentSizeFitter>();
            badgeFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            badgeFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _modeBadge = _ui.Label(badgeGo.transform, "", 16, UIFactory.Muted, FontStyles.Normal, 240);
            _modeBadge.alignment = TextAlignmentOptions.Center; _modeBadge.textWrappingMode = TextWrappingModes.NoWrap; _modeBadge.raycastTarget = false;
            _modeBadge.GetComponent<LayoutElement>().preferredWidth = -1;
            badgeGo.SetActive(false);

            // Tall-map preview scrollbar (right edge). Drag to look around the map
            // without moving the knight; releases back to following the knight.
            var sbTrack = new GameObject("TallScroll", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbTrack.transform.SetParent(canvas.transform, false);
            var sbRt = sbTrack.GetComponent<RectTransform>();
            sbRt.anchorMin = sbRt.anchorMax = new Vector2(1f, 0.5f); sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.sizeDelta = new Vector2(24f, 440f); sbRt.anchoredPosition = new Vector2(-6f, 0f);
            // Pixel-art bar material, identical to the volume sliders and the queen's
            // health bar: the button 9-slice at 2x ppu, Panel track under a Gold handle.
            var sbImg = sbTrack.GetComponent<Image>();
            sbImg.sprite = _ui.ButtonBg(); sbImg.type = Image.Type.Sliced;
            sbImg.color = UIFactory.Panel; sbImg.pixelsPerUnitMultiplier = 2f;
            _tallScrollbar = sbTrack.GetComponent<Scrollbar>();
            _tallScrollbar.direction = Scrollbar.Direction.TopToBottom;

            var sbHandle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            sbHandle.transform.SetParent(sbTrack.transform, false);
            var sbHRt = sbHandle.GetComponent<RectTransform>();
            sbHRt.anchorMin = Vector2.zero; sbHRt.anchorMax = Vector2.one;
            sbHRt.offsetMin = new Vector2(3f, 0f); sbHRt.offsetMax = new Vector2(-3f, 0f);
            var sbHImg = sbHandle.GetComponent<Image>();
            sbHImg.sprite = _ui.ButtonBg(); sbHImg.type = Image.Type.Sliced;
            sbHImg.color = UIFactory.Gold; sbHImg.pixelsPerUnitMultiplier = 2f;

            // Gold pixel border on the knob, matching the slider handles.
            var sbHBorder = new GameObject("Border", typeof(RectTransform), typeof(Image));
            sbHBorder.transform.SetParent(sbHandle.transform, false);
            var sbHBRt = sbHBorder.GetComponent<RectTransform>();
            sbHBRt.anchorMin = Vector2.zero; sbHBRt.anchorMax = Vector2.one;
            sbHBRt.offsetMin = Vector2.zero; sbHBRt.offsetMax = Vector2.zero;
            var sbHBImg = sbHBorder.GetComponent<Image>();
            sbHBImg.sprite = _ui.Border(); sbHBImg.type = Image.Type.Sliced;
            sbHBImg.color = UIFactory.Gold; sbHBImg.pixelsPerUnitMultiplier = 2f;
            sbHBImg.raycastTarget = false;

            _tallScrollbar.handleRect = sbHRt; _tallScrollbar.targetGraphic = sbHImg;
            _tallScrollbar.onValueChanged.AddListener(OnTallScrollChanged);
            sbTrack.SetActive(false);

            // Transform form badge: the enemy piece you've become, in a ring, just
            // below the score. Hidden unless transformed.
            _xformBadge = new GameObject("XformBadge", typeof(RectTransform));
            _xformBadge.transform.SetParent(canvas.transform, false);
            _xformBadgeRt = _xformBadge.GetComponent<RectTransform>();
            _xformBadgeRt.anchorMin = _xformBadgeRt.anchorMax = new Vector2(0, 1); _xformBadgeRt.pivot = new Vector2(0, 1);
            _xformBadgeRt.sizeDelta = new Vector2(50, 50);
            _xformBadgeRt.anchoredPosition = new Vector2(20, -70); // repositioned by LayoutHud (kept 50 below the score)
            _xformBadgeRing = MakeCircleImage(_xformBadge.transform, 50, UIFactory.Gold);       // coloured ring
            MakeCircleImage(_xformBadge.transform, 42, new Color(0.08f, 0.08f, 0.14f, 0.96f));   // dark inner disc
            _xformBadgeGlyph = _ui.Label(_xformBadge.transform, "", 26, UIFactory.Gold, FontStyles.Normal, 50);
            if (_theme != null && _theme.glyphFont != null) _xformBadgeGlyph.font = _theme.glyphFont;
            var xgRt = _xformBadgeGlyph.rectTransform;
            xgRt.anchorMin = Vector2.zero; xgRt.anchorMax = Vector2.one; xgRt.offsetMin = Vector2.zero; xgRt.offsetMax = Vector2.zero;
            _xformBadgeGlyph.alignment = TextAlignmentOptions.Center;
            _xformBadgeGlyph.textWrappingMode = TextWrappingModes.NoWrap;
            _xformBadge.SetActive(false);

            var restart = _ui.Button(canvas.transform, "", () => _gm.RestartGame(), UIFactory.Panel, UIFactory.Gold, 40, 40, 18);
            AddIconChild(restart.gameObject, UISprites.Refresh(), UIFactory.Gold, 24); // theme gold
            _restartRt = restart.GetComponent<RectTransform>();
            _restartRt.anchorMin = _restartRt.anchorMax = new Vector2(1, 1); _restartRt.pivot = new Vector2(1, 1);
            _restartRt.sizeDelta = new Vector2(40, 40);
            _restartRt.anchoredPosition = new Vector2(-64, -16); // repositioned by LayoutHud

            var pause = _ui.Button(canvas.transform, "II", () => _gm.PauseGame(), UIFactory.Panel, UIFactory.Gold, 40, 40, 32);
            // The button label normally auto-shrinks to a tight inset; pin it so the
            // pause glyph renders at the full doubled size (32, was 16) and fills the
            // button instead of being capped by the inset width.
            var pauseLabel = pause.GetComponentInChildren<TMP_Text>();
            if (pauseLabel != null)
            {
                pauseLabel.enableAutoSizing = false;
                pauseLabel.fontSize = 32;
                UIFactory.Stretch(pauseLabel.rectTransform);
            }
            _pauseRt = pause.GetComponent<RectTransform>();
            _pauseRt.anchorMin = _pauseRt.anchorMax = new Vector2(1, 1); _pauseRt.pivot = new Vector2(1, 1);
            _pauseRt.sizeDelta = new Vector2(40, 40);
            _pauseRt.anchoredPosition = new Vector2(-16, -16); // repositioned by LayoutHud

            // Powerup activation buttons, directly under the pause/restart pair (Battle/Trap
            // only; shown/updated by LayoutHud). Same icons as the shop's Powerups tab.
            _divineBtn = MakePowerupButton(canvas.transform, Powerups.DivineIntervention, out _divineBtnRt, out _divineCount, out _divineRadial);
            _chronoBtn = MakePowerupButton(canvas.transform, Powerups.Chronokinesis, out _chronoBtnRt, out _chronoCount, out _chronoRadial);
            // Adventure-only Divine Path button (takes the same slot as chrono; only one HUD is up at a time).
            _pathBtn = MakePowerupButton(canvas.transform, Powerups.DivinePath, out _pathBtnRt, out _pathCount, out _pathRadial);

            // Enemy-move timer (battle only): "ENEMY MOVE" label above a filling bar.
            _enemyBarRoot = new GameObject("EnemyBar", typeof(RectTransform));
            _enemyBarRoot.transform.SetParent(canvas.transform, false);
            _enemyBarRt = _enemyBarRoot.GetComponent<RectTransform>();
            _enemyBarRt.anchorMin = _enemyBarRt.anchorMax = new Vector2(0.5f, 0); _enemyBarRt.pivot = new Vector2(0.5f, 0);
            _enemyBarRt.sizeDelta = new Vector2(320, 40);
            _enemyBarRt.anchoredPosition = new Vector2(0, 48); // repositioned each frame for safe area

            var lbl = _ui.Label(_enemyBarRoot.transform, "ENEMY MOVE", 11, UIFactory.Muted);
            lbl.alignment = TextAlignmentOptions.Center; lbl.textWrappingMode = TextWrappingModes.NoWrap;
            var lRt = lbl.rectTransform;
            lRt.anchorMin = lRt.anchorMax = new Vector2(0.5f, 0); lRt.pivot = new Vector2(0.5f, 0);
            lRt.sizeDelta = new Vector2(200, 18); lRt.anchoredPosition = new Vector2(0, 16);

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(_enemyBarRoot.transform, false);
            var trRt = track.GetComponent<RectTransform>();
            trRt.anchorMin = trRt.anchorMax = new Vector2(0.5f, 0); trRt.pivot = new Vector2(0.5f, 0);
            trRt.sizeDelta = new Vector2(300, 7); trRt.anchoredPosition = new Vector2(0, 2);
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

        // HUD canvas reference resolution (canvas units).
        private const float HudRefW = 640f, HudRefH = 1138f;
        // Top bar = a safe offset (max of the notch and a swipe-guard so interactive
        // elements clear the control-center / notification pull-down zone) + a content row.
        private const float HudTopContentH = 52f;   // room for the score / pause / restart row
        private const float HudTopSwipeGuard = 34f; // min gap from the very top edge
        // Bottom bar ≈ the Android 3-button nav height; never smaller than the gesture inset.
        private const float HudBottomBarH = 76f;

        // A full-width SOLID, STRAIGHT-edged rectangle pinned to the top or bottom edge,
        // created behind the HUD content. Height is set each frame by LayoutHud.
        private RectTransform MakeHudBar(Transform parent, bool top)
        {
            var go = new GameObject(top ? "TopBar" : "BottomBar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            float y = top ? 1f : 0f;
            rt.anchorMin = new Vector2(0f, y); rt.anchorMax = new Vector2(1f, y);
            rt.pivot = new Vector2(0.5f, y);
            rt.sizeDelta = new Vector2(0f, HudTopContentH);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = BoardView.UnitSquare(); // plain square, no rounded corners
            img.type = Image.Type.Simple;
            img.color = new Color(0.075f, 0.075f, 0.137f, 1f); // solid #131323
            img.raycastTarget = false;
            return rt;
        }

        // Analytic CanvasScaler scale (ScaleWithScreenSize, match 0.5), so the letterbox
        // math is stable whether or not the HUD canvas is currently active.
        private static float HudScale()
        {
            if (Screen.height <= 0 || Screen.width <= 0) return 1f;
            float logW = Mathf.Log(Screen.width / HudRefW, 2f);
            float logH = Mathf.Log(Screen.height / HudRefH, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logW, logH, 0.5f));
        }

        // Top offset (canvas units): push content below the notch AND the top-edge swipe zone.
        private static float TopOffsetCanvas()
        {
            float scale = HudScale();
            float topSafe = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax) / scale;
            return Mathf.Max(topSafe, HudTopSwipeGuard);
        }
        private static float TopBarCanvas() => TopOffsetCanvas() + HudTopContentH;
        private static float BottomBarCanvas()
        {
            float botSafe = Mathf.Max(0f, Screen.safeArea.yMin) / HudScale();
            return Mathf.Max(HudBottomBarH, botSafe);
        }

        // Fractions of screen HEIGHT taken by the top and bottom bars, for the board
        // letterbox. The board is squished into the strip between them.
        public float HudTopInsetFrac()
        {
            if (Screen.height <= 0 || Screen.width <= 0) return 0f;
            return (TopBarCanvas() * HudScale()) / Screen.height;
        }
        public float HudBottomInsetFrac()
        {
            if (Screen.height <= 0 || Screen.width <= 0) return 0f;
            return (BottomBarCanvas() * HudScale()) / Screen.height;
        }

        // Repositions the safe-area-sensitive HUD elements every frame (cheap): bar heights,
        // the score / pause / restart row, the powerup buttons, the adventure label, and the
        // transform badge. Called from Update while the HUD is visible.
        private void LayoutHud()
        {
            float topOffset = TopOffsetCanvas();
            if (_topBarRt != null) _topBarRt.sizeDelta = new Vector2(0f, TopBarCanvas());
            if (_bottomBarRt != null) _bottomBarRt.sizeDelta = new Vector2(0f, BottomBarCanvas());

            float rowY = -(topOffset + 6f);          // top of the score / button row
            if (_scoreRt != null) _scoreRt.anchoredPosition = new Vector2(24f, rowY);
            if (_pauseRt != null) _pauseRt.anchoredPosition = new Vector2(-16f, rowY);
            if (_restartRt != null) _restartRt.anchoredPosition = new Vector2(-64f, rowY);
            if (_advMovesRt != null) _advMovesRt.anchoredPosition = new Vector2(0f, rowY + 2f);
            if (_modeBadgeRt != null) _modeBadgeRt.anchoredPosition = new Vector2(0f, rowY); // top-centre, in line with the row
            if (_bossHudRt != null) _bossHudRt.anchoredPosition = new Vector2(0f, rowY - 46f); // below the level/move label
            if (_xformBadgeRt != null) _xformBadgeRt.anchoredPosition = new Vector2(20f, rowY - 50f); // keep the ~50 gap under the score

            // Powerup buttons directly under pause / restart.
            float puY = rowY - 48f;
            if (_chronoBtnRt != null) _chronoBtnRt.anchoredPosition = new Vector2(-16f, puY);
            if (_divineBtnRt != null) _divineBtnRt.anchoredPosition = new Vector2(-64f, puY);
            if (_pathBtnRt != null) _pathBtnRt.anchoredPosition = new Vector2(-16f, puY); // adventure slot
        }

        // One in-game powerup button: a radial-wipe duration overlay (behind), the shop
        // icon, and a small owned-count badge. Tapping activates it via GameManager.
        // Position/visibility handled by LayoutHud + Update.
        private Button MakePowerupButton(Transform parent, PowerupInfo pu, out RectTransform rt, out TMP_Text count, out Image radial)
        {
            var btn = _ui.Button(parent, "", () => OnActivatePowerup(pu.Id), UIFactory.Panel, UIFactory.Gold, 40, 40, 12);
            rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(40, 40);

            // Radial-wipe duration indicator (behind the icon): a gold wedge that depletes
            // clockwise over the effect's duration. Same gold as the activation colour.
            var radialGo = new GameObject("Radial", typeof(RectTransform), typeof(Image));
            radialGo.transform.SetParent(btn.transform, false);
            radial = radialGo.GetComponent<Image>();
            radial.sprite = _ui.Circle();
            radial.type = Image.Type.Filled;
            radial.fillMethod = Image.FillMethod.Radial360;
            radial.fillOrigin = (int)Image.Origin360.Top;
            radial.fillClockwise = true;
            radial.color = new Color(UIFactory.Gold.r, UIFactory.Gold.g, UIFactory.Gold.b, 0.6f);
            radial.raycastTarget = false;
            var radRt = radial.rectTransform;
            radRt.anchorMin = Vector2.zero; radRt.anchorMax = Vector2.one; radRt.offsetMin = Vector2.zero; radRt.offsetMax = Vector2.zero;
            radialGo.SetActive(false);

            if (pu.Icon != null) AddIconChild(btn.gameObject, pu.Icon, Color.white, 30);

            // Owned-count badge, bottom-right corner of the button.
            count = _ui.Label(btn.transform, "", 12, UIFactory.Gold, FontStyles.Normal);
            var cRt = count.rectTransform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(1, 0); cRt.pivot = new Vector2(1, 0);
            cRt.sizeDelta = new Vector2(24, 16); cRt.anchoredPosition = new Vector2(2, -2);
            count.alignment = TextAlignmentOptions.BottomRight; count.raycastTarget = false;
            btn.gameObject.SetActive(false);
            return btn;
        }

        private void OnActivatePowerup(PowerupId id)
        {
            bool ok = id == PowerupId.DivinePath ? _gm.ActivateDivinePath() : _gm.ActivatePowerup(id);
            if (ok) UpdatePowerupButtons();
        }

        // "THE QUEEN" boss health bar (3 segments), top-centre; hidden outside a boss and
        // repositioned by LayoutHud so it clears the notch.
        private void BuildBossHud(Transform parent)
        {
            _bossHud = new GameObject("BossHud", typeof(RectTransform));
            _bossHud.transform.SetParent(parent, false);
            _bossHudRt = _bossHud.GetComponent<RectTransform>();
            _bossHudRt.anchorMin = _bossHudRt.anchorMax = new Vector2(0.5f, 1f); _bossHudRt.pivot = new Vector2(0.5f, 1f);
            _bossHudRt.sizeDelta = new Vector2(240, 40);
            _bossHudRt.anchoredPosition = new Vector2(0, -96); // repositioned by LayoutHud

            var lab = _ui.Label(_bossHud.transform, "THE QUEEN", 14, UIFactory.Hex("#FF6FA5"), FontStyles.Normal);
            var lRt = lab.rectTransform;
            lRt.anchorMin = lRt.anchorMax = new Vector2(0.5f, 1f); lRt.pivot = new Vector2(0.5f, 1f);
            lRt.sizeDelta = new Vector2(240, 18); lRt.anchoredPosition = Vector2.zero;
            lab.alignment = TextAlignmentOptions.Center; lab.raycastTarget = false;

            const int segs = 3; const float segW = 66f, segH = 12f, gap = 4f;
            float totalW = segs * segW + (segs - 1) * gap;
            _bossSegs = new Image[segs];
            for (int i = 0; i < segs; i++)
            {
                var go = new GameObject("seg" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_bossHud.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(segW, segH);
                rt.anchoredPosition = new Vector2(-totalW * 0.5f + segW * 0.5f + i * (segW + gap), -20f);
                var img = go.GetComponent<Image>();
                // Same pixel-art bar material as the volume sliders (ButtonBg 9-slice at 2x ppu).
                img.sprite = _ui.ButtonBg(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2f;
                img.raycastTarget = false;
                _bossSegs[i] = img;
            }
            _bossHud.SetActive(false);
        }

        // Show/update the queen's health (segments filled = HP left). Filled segments are
        // blue while she's shielded, red once she's vulnerable; empty segments are dark.
        public void SetBossHud(bool show, int health, int max, bool shielded)
        {
            if (_bossHud == null) return;
            if (_bossHud.activeSelf != show) _bossHud.SetActive(show);
            if (!show || _bossSegs == null) return;
            Color filled = shielded ? UIFactory.Hex("#4FA8FF") : UIFactory.Hex("#FF5A5A");
            Color empty = new Color(0.12f, 0.13f, 0.20f, 1f);
            for (int i = 0; i < _bossSegs.Length; i++)
                if (_bossSegs[i] != null)
                    _bossSegs[i].color = i < health ? filled : empty;
        }

        // Bring the menu's corner controls (Shop, Customize, shillings count) down to the
        // same screen depth as the in-game top bar, so they clear notches / cameras / the
        // top-edge swipe zone. Converts the in-game content depth (pixels) into this
        // canvas's units (the menu canvas uses a different reference resolution).
        private void LayoutMenu()
        {
            if (_menuCanvas == null) return;
            float menuScale = _menuCanvas.scaleFactor > 0f ? _menuCanvas.scaleFactor : 1f;
            float y = -((TopOffsetCanvas() + 6f) * HudScale()) / menuScale;
            if (_menuShopRt != null) _menuShopRt.anchoredPosition = new Vector2(-14f, y);
            if (_menuCustomRt != null) _menuCustomRt.anchoredPosition = new Vector2(14f, y);
            if (_menuShillingsRt != null) _menuShillingsRt.anchoredPosition = new Vector2(-82f, y);
        }

        // Show the powerup buttons only in Battle/Trap play, and reflect owned count / the
        // active countdown. Disabled (dimmed) when you own none or the effect is running.
        private void UpdatePowerupButtons()
        {
            bool show = _gm != null && _gm.PowerupsUsable;
            if (_chronoBtn != null && _chronoBtn.gameObject.activeSelf != show) _chronoBtn.gameObject.SetActive(show);
            if (_divineBtn != null && _divineBtn.gameObject.activeSelf != show) _divineBtn.gameObject.SetActive(show);
            if (show)
            {
                UpdatePowerupButton(_chronoBtn, _chronoCount, _chronoRadial, PowerupId.Chronokinesis, Powerups.Chronokinesis, ref _chronoCountShown);
                UpdatePowerupButton(_divineBtn, _divineCount, _divineRadial, PowerupId.DivineIntervention, Powerups.DivineIntervention, ref _divineCountShown);
            }

            // Divine Path button (adventure only; no timer, it stays on once activated).
            bool pathShow = _gm != null && _gm.PathUsable;
            if (_pathBtn != null && _pathBtn.gameObject.activeSelf != pathShow) _pathBtn.gameObject.SetActive(pathShow);
            if (pathShow && _pathBtn != null)
            {
                int owned = Powerups.DivinePath.Owned;
                bool active = _gm.DivinePathActive;
                if (_pathRadial != null && _pathRadial.gameObject.activeSelf) _pathRadial.gameObject.SetActive(false);
                SetCountLabel(_pathCount, ref _pathCountShown, owned, active);
                _pathBtn.interactable = owned > 0 && !active;
                if (_pathBtn.targetGraphic is Image img)
                    img.color = active ? UIFactory.Gold
                              : (owned > 0 ? UIFactory.Panel : new Color(0.12f, 0.13f, 0.20f, 0.6f));
            }
        }

        // These run every frame the HUD is up. Assigning TMP text allocates a string AND
        // rebuilds the glyph mesh even when the value is identical, so only write on change.
        // -1 = "showing nothing" (the powerup is active), -2 = never written yet.
        private int _chronoCountShown = -2, _divineCountShown = -2, _pathCountShown = -2;
        private static void SetCountLabel(TMP_Text label, ref int shown, int owned, bool active)
        {
            if (label == null) return;
            int want = active ? -1 : owned;
            if (want == shown) return;
            shown = want;
            label.text = active ? "" : owned.ToString();
        }

        private void UpdatePowerupButton(Button btn, TMP_Text count, Image radial, PowerupId id, PowerupInfo pu,
                                         ref int shown)
        {
            if (btn == null) return;
            int owned = pu.Owned;
            bool active = _gm.PowerupActive(id);
            // While active: the radial wipe shows time left (no number). Otherwise: owned count.
            if (radial != null)
            {
                if (radial.gameObject.activeSelf != active) radial.gameObject.SetActive(active);
                if (active && pu.Duration > 0f) radial.fillAmount = Mathf.Clamp01(_gm.PowerupRemaining(id) / pu.Duration);
            }
            SetCountLabel(count, ref shown, owned, active);
            btn.interactable = owned > 0 && !active;
            if (btn.targetGraphic is Image img)
                img.color = owned > 0 || active ? UIFactory.Panel : new Color(0.12f, 0.13f, 0.20f, 0.6f);
        }

        // Show/update the enemy-move timer. Called every frame from GameManager.
        public void SetEnemyBar(bool show, float progress)
        {
            if (_enemyBarRoot == null) return;
            if (_enemyBarRoot.activeSelf != show) _enemyBarRoot.SetActive(show);
            if (!show || _enemyBarFill == null) return;
            // Lift the bar to sit just above the bottom bar (which already covers the
            // device's gesture/nav inset) so it's never clipped or hidden.
            if (_enemyBarRt != null)
                _enemyBarRt.anchoredPosition = new Vector2(0, BottomBarCanvas() + 12f);
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
            _ui.Label(col, "PAUSED", 28, UIFactory.Gold, FontStyles.Normal);
            _ui.Button(col, "Resume", () => _gm.ResumeGame(), UIFactory.Gold, UIFactory.Bg);
            _pauseLevelSelectBtn = _ui.Button(col, "Level Select", () => _gm.ExitAdventureToLevelSelect(), UIFactory.Panel, UIFactory.Gold).gameObject;
            _pauseLevelSelectBtn.SetActive(false);
            _ui.Button(col, "How to Play", () => ShowHowTo(returnToPause: true), UIFactory.Panel, UIFactory.Gold);
            _pauseShakeToggle = _ui.Button(col, "Screen Shake", () => ToggleScreenShake(), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Restart", () => _gm.RestartGame(), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Main Menu", () => _gm.ToMenu(), UIFactory.Panel, UIFactory.Gold);
            UpdateScreenShakeToggles();
        }

        // ── Game over ────────────────────────────────────────────────────────
        private void BuildGameOver()
        {
            var canvas = _ui.CreateCanvas("GameOver", 110);
            _gameOver = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, new Color(0.08f, 0.08f, 0.16f, 0.95f));
            var col = _ui.Column(screen.transform, 12f);
            _ui.Label(col, "GAME OVER", 28, UIFactory.Gold, FontStyles.Normal);
            _gameOverScore = _ui.Label(col, "Score: 0", 24, UIFactory.Gold, FontStyles.Normal);
            _goReward = _ui.Label(col, "", 13, UIFactory.Gold);
            _savedName = _ui.Label(col, "", 16, UIFactory.Muted);
            _goClaimRow = _ui.Column(col, 6f).gameObject;
            _goNameInput = _ui.Input(_goClaimRow.transform, "NAME (OPTIONAL)", 7);
            _ui.Button(_goClaimRow.transform, "Save score", () => OnClaimScore(), UIFactory.Gold, UIFactory.Bg, 200, 44, 14);
            _trappedBtn = _ui.Button(col, "Was I trapped?", () => ShowTrapped(), UIFactory.Panel, UIFactory.Gold, 220, 44, 14);

            _reviveRow = _ui.Column(col, 6f).gameObject;
            _ui.Label(_reviveRow.transform, "Extra life?", 13, UIFactory.Muted);
            var rr = _ui.Row(_reviveRow.transform, 8f);
            _reviveWatchBtn = _ui.Button(rr, "Watch ad", () => _gm.WatchAdForLife(), UIFactory.Gold, UIFactory.Bg, 160, 42, 14);
            _reviveSpendBtn = _ui.Button(rr, "Spend 5", OnReviveSpend, UIFactory.Panel, UIFactory.Gold, 160, 42, 14);

            _goBalance = _ui.Label(col, "", 15, UIFactory.Muted);
            _ui.Button(col, "Restart", () => _gm.RestartGame(), UIFactory.Gold, UIFactory.Bg);
            _ui.Button(col, "Home", () => _gm.ToMenu(), UIFactory.Panel, UIFactory.Gold);
        }

        // Claim an anonymous run from the game-over screen: validate + claim the
        // name (permanent), then submit the just-finished score under it.
        private void OnClaimScore()
        {
            if (_checkingName) return;
            var raw = (_goNameInput.text ?? "").Trim();
            if (raw.Length > 7) raw = raw.Substring(0, 7);
            if (string.IsNullOrEmpty(raw)) { _savedName.color = UIFactory.Amber; _savedName.text = "Enter a name to save."; return; }
            if (!NameFilter.IsAllowed(raw)) { _savedName.color = UIFactory.Error; _savedName.text = "Please choose a different name."; return; }
            _checkingName = true;
            _savedName.color = UIFactory.Muted; _savedName.text = "Checking name\u2026";
            _gm.CheckNameAvailable(raw, available =>
            {
                _checkingName = false;
                if (!available) { _savedName.color = UIFactory.Error; _savedName.text = "That name's taken. Pick another."; return; }
                SaveData.SetName(raw);
                _gm.SubmitLastRun();
                if (_goClaimRow != null) _goClaimRow.SetActive(false);
                _savedName.color = UIFactory.Hex("#3CDC78");
                _savedName.text = "Saved as " + raw + " \u2713";
                RefreshNameUI();
            });
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
            _countdownText.fontStyle = FontStyles.Normal;
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
            _ui.Label(col, "Daily Challenge", 27, UIFactory.Gold, FontStyles.Normal);
            _ui.Label(col, "Everyone plays the SAME board each day, the exact same layout for every player.", 15, UIFactory.Text, FontStyles.Normal, 320);
            _ui.Label(col, "You get 10 attempts per day. They reset at midnight, and there are no extra lives.", 15, UIFactory.Text, FontStyles.Normal, 320);
            _ui.Label(col, "No powerups are allowed, everyone competes on equal footing.", 15, UIFactory.Text, FontStyles.Normal, 320);
            _ui.Label(col, "Finish in the daily top 5 to earn shillings.", 13, UIFactory.Muted, FontStyles.Normal, 320);
            _dailyIntroAttempts = _ui.Label(col, "", 15, UIFactory.Gold, FontStyles.Normal);
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
            _ui.Label(col, "Daily Challenge Results", 25, UIFactory.Gold, FontStyles.Normal);
            _dailyRewardText = _ui.Label(col, "", 17, UIFactory.Text, FontStyles.Normal, 330);
            _ui.Button(col, "Claim", () => ShowMenu(), UIFactory.Gold, UIFactory.Bg, 220, 52, 16);
        }
        public void ShowDailyReward(int place, string dateLabel)
        {
            HideAll();
            string when = string.IsNullOrEmpty(dateLabel)
                ? "your last Daily Challenge"
                : "the Daily Challenge on " + dateLabel;
            _dailyRewardText.text =
                "Congrats! You finished " + Ordinal(place) + " in " + when + ".\n\n" +
                DailyPrizes.Describe(place);
            _dailyReward.SetActive(true);
            _gm.SetBanner(true);
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
            _ui.Label(col, "Settings", 28, UIFactory.Gold, FontStyles.Normal);
            _ui.Label(col, "Music Volume", 15, UIFactory.Muted, FontStyles.Normal);
            // The music slider slides 0..1 across the full track, but maps to the capped
            // 0..MaxMusicVolume range, so full-right is 10% actual volume.
            _ui.Slider(col, SfxManager.MusicVolume / SfxManager.MaxMusicVolume,
                       v => SfxManager.MusicVolume = v * SfxManager.MaxMusicVolume, 300, 32);
            _ui.Label(col, "SFX Volume", 15, UIFactory.Muted, FontStyles.Normal);
            // Same as the music slider: 0..1 across the track maps to the capped
            // 0..MaxSfxVolume range, so full-right is 10% actual volume.
            _ui.Slider(col, SfxManager.SfxVolume / SfxManager.MaxSfxVolume,
                       v => SfxManager.SfxVolume = v * SfxManager.MaxSfxVolume, 300, 32);
            _ui.Button(col, "How to Play", () => ShowHowTo(returnToPause: false), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Achievements", () => ShowAchievements(), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Report a Bug", () => ShowBugReport(), UIFactory.Panel, UIFactory.Gold);
            _cbToggle = _ui.Button(col, "Colorblind Mode", () => ToggleColorblind(), UIFactory.Panel, UIFactory.Gold);
            _cbToggleLabel = _cbToggle.GetComponentInChildren<TMP_Text>();
            UpdateColorblindLabel();
            _shakeToggle = _ui.Button(col, "Screen Shake", () => ToggleScreenShake(), UIFactory.Panel, UIFactory.Gold);
            UpdateScreenShakeToggles();
            _ui.Button(col, "Tutorial", () => ShowTutorialPick(), UIFactory.Panel, UIFactory.Gold);
            // Debug builds only: open the LevelPlay Test Suite to verify ad networks and
            // read this device's advertising ID. Stripped from release builds.
            if (Debug.isDebugBuild)
                _ui.Button(col, "Ad Test Suite", () => _gm.LaunchAdTestSuite(), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Back", () => ShowMenu(), UIFactory.Gold, UIFactory.Bg, 220, 50, 22);
        }
        private void ToggleColorblind()
        {
            SaveData.SetColorblind(!SaveData.IsColorblind());
            UpdateColorblindLabel();
        }
        private void UpdateColorblindLabel()
        {
            // Reflect the on/off state with the gold selection border (and gold text),
            // the same way the menu's selected mode/difficulty buttons show selection.
            if (_cbToggle != null) UIFactory.StyleToggle(_cbToggle, SaveData.IsColorblind());
        }
        // Screen shake lives in both Settings and the Pause menu, so flipping it in one
        // place restyles both toggles to keep them in sync.
        private void ToggleScreenShake()
        {
            SaveData.SetScreenShake(!SaveData.IsScreenShake());
            UpdateScreenShakeToggles();
        }
        private void UpdateScreenShakeToggles()
        {
            bool on = SaveData.IsScreenShake();
            if (_shakeToggle != null) UIFactory.StyleToggle(_shakeToggle, on);
            if (_pauseShakeToggle != null) UIFactory.StyleToggle(_pauseShakeToggle, on);
        }
        private void ShowSettings() { HideAll(); _settings.SetActive(true); _gm.SetBanner(true); UpdateScreenShakeToggles(); }

        // ── Report a Bug ─────────────────────────────────────────────────────
        // A pixel-styled feedback box. Whatever the player types is handed to
        // GameManager.SendBugReport, which files it to the Supabase bug_reports table.
        private void BuildBugReport()
        {
            var canvas = _ui.CreateCanvas("BugReport", 111);
            _bugReport = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 16f);
            _ui.Label(col, "Report a Bug", 28, UIFactory.Gold, FontStyles.Normal);
            _ui.Label(col, "Tell us what went wrong. The more detail, the better.",
                      13, UIFactory.Muted, FontStyles.Normal, 360);

            _bugInput = _ui.Input(col, "DESCRIBE THE BUG...", 600, 360f, 220f);
            _bugInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            if (_bugInput.textComponent != null)
                _bugInput.textComponent.alignment = TextAlignmentOptions.TopLeft;
            if (_bugInput.placeholder is TMP_Text ph)
                ph.alignment = TextAlignmentOptions.TopLeft;

            var row = _ui.Row(col, 14f);
            _ui.Button(row, "Send", () => OnSendBug(), UIFactory.Gold, UIFactory.Bg);
            _ui.Button(row, "Back", () => ShowSettings(), UIFactory.Panel, UIFactory.Gold);
            _bugReport.SetActive(false);
        }
        private void ShowBugReport()
        {
            HideAll();
            if (_bugInput != null) _bugInput.text = "";
            _bugReport.SetActive(true);
            _gm.SetBanner(true);
        }
        private void OnSendBug()
        {
            var text = (_bugInput != null ? _bugInput.text : "") ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowToast("Type a description first.");
                return;
            }
            _gm.SendBugReport(text.Trim(),
                onOk:   () => ShowToast("Thanks! Your report was sent."),
                onFail: () => ShowToast("Couldn't send. Check your connection and try again."));
            if (_bugInput != null) _bugInput.text = "";
            ShowSettings();
        }

        // ── Interactive tutorial banner (caption + Skip, plus Continue on info) ──
        private void BuildTutorial()
        {
            var canvas = _ui.CreateCanvas("Tutorial", 110);
            _tutorial = canvas.gameObject;

            var panel = new GameObject("TutCaption", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var pRt = panel.GetComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0f); pRt.pivot = new Vector2(0.5f, 0f);
            pRt.sizeDelta = new Vector2(480, 134); pRt.anchoredPosition = new Vector2(0, 128);
            var pImg = panel.GetComponent<Image>();
            pImg.sprite = _ui.Fill(); pImg.type = Image.Type.Sliced;
            pImg.color = new Color(0.06f, 0.07f, 0.13f, 0.92f);
            pImg.raycastTarget = false; // don't steal board taps

            _tutCaption = _ui.Label(panel.transform, "", 17, UIFactory.Text, FontStyles.Normal, 440);
            var cRt = _tutCaption.rectTransform;
            cRt.anchorMin = Vector2.zero; cRt.anchorMax = Vector2.one;
            cRt.offsetMin = new Vector2(16, 10); cRt.offsetMax = new Vector2(-16, -10);
            _tutCaption.alignment = TextAlignmentOptions.Center;

            var skip = _ui.Button(canvas.transform, "Skip", () => _gm.TutorialSkip(), UIFactory.Panel, UIFactory.Gold, 76, 40, 14);
            var skRt = skip.GetComponent<RectTransform>();
            skRt.anchorMin = skRt.anchorMax = new Vector2(1, 1); skRt.pivot = new Vector2(1, 1);
            skRt.sizeDelta = new Vector2(76, 40); skRt.anchoredPosition = new Vector2(-16, -16);

            var cont = _ui.Button(canvas.transform, "Continue", () => _gm.TutorialContinue(), UIFactory.Gold, UIFactory.Bg, 190, 52, 16);
            _tutContinueBtn = cont.gameObject;
            var coRt = cont.GetComponent<RectTransform>();
            coRt.anchorMin = coRt.anchorMax = new Vector2(0.5f, 0f); coRt.pivot = new Vector2(0.5f, 0f);
            coRt.sizeDelta = new Vector2(190, 52); coRt.anchoredPosition = new Vector2(0, 66);

            _tutorial.SetActive(false);
        }
        public void ShowTutorial(string caption, bool continueBtn)
        {
            if (_tutCaption != null) _tutCaption.text = caption;
            if (_tutContinueBtn != null) _tutContinueBtn.SetActive(continueBtn);
            if (_tutorial != null) _tutorial.SetActive(true);
        }
        public void HideTutorial() { if (_tutorial != null) _tutorial.SetActive(false); }

        // Chooser: pick which mode's interactive tutorial to (re)play.
        private void BuildTutorialPick()
        {
            var canvas = _ui.CreateCanvas("TutorialPick", 106);
            _tutPick = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 14f);
            _ui.Label(col, "Tutorials", 28, UIFactory.Gold, FontStyles.Normal);
            _ui.Button(col, "Trap Tutorial", () => _gm.StartTrapTutorial(Difficulty.Easy, replay: true), UIFactory.Gold, UIFactory.Bg, 240, 52, 16);
            _ui.Button(col, "Battle Tutorial", () => _gm.StartBattleTutorial(Difficulty.Easy, replay: true), UIFactory.Gold, UIFactory.Bg, 240, 52, 16);
            _ui.Button(col, "Back", () => ShowSettings(), UIFactory.Panel, UIFactory.Gold);
            _tutPick.SetActive(false);
        }
        private void ShowTutorialPick() { HideAll(); _tutPick.SetActive(true); _gm.SetBanner(true); }

        // ── Adventure mode UI ────────────────────────────────────────────────
        // Star / icon helpers. The universal chess-glyph font (DejaVu) has no star
        // or restart-arrow in its atlas, so we draw them as sprites instead.
        private void AddIconChild(GameObject btn, Sprite sprite, Color color, float size)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(btn.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>(); img.sprite = sprite; img.color = color; img.raycastTarget = false;
        }
        // "7-♔" tile label: "<world>-" in the UI font + the crown glyph in the pixel chess
        // glyph font (same crown the leaderboard uses for the top ranks), both gold. Placed
        // by hand (a layout group sizes by the glyph's advance and mis-centres it). Tune the
        // two X offsets to shift the number and the crown horizontally.
        private const float CrownTileSixX = -7f;    // number X offset; raise toward 0 to pull it right
        private const float CrownTileCrownX = 11f;  // crown X offset
        private void BuildCrownTileLabel(Button btn, string world)
        {
            var six = _ui.Label(btn.transform, world + "-", 22, UIFactory.Gold, FontStyles.Normal, 84);
            var sRt = six.rectTransform;
            sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.sizeDelta = new Vector2(84, 84); sRt.anchoredPosition = new Vector2(CrownTileSixX, 0f);
            six.alignment = TextAlignmentOptions.Center; six.textWrappingMode = TextWrappingModes.NoWrap; six.raycastTarget = false;

            var crown = _ui.Label(btn.transform, "♔", 24, UIFactory.Gold, FontStyles.Normal, 84);
            if (_theme != null && _theme.glyphFont != null) crown.font = _theme.glyphFont;
            var cRt = crown.rectTransform;
            cRt.anchorMin = cRt.anchorMax = cRt.pivot = new Vector2(0.5f, 0.5f);
            cRt.sizeDelta = new Vector2(84, 84); cRt.anchoredPosition = new Vector2(CrownTileCrownX, 0f);
            crown.alignment = TextAlignmentOptions.Center; crown.textWrappingMode = TextWrappingModes.NoWrap; crown.raycastTarget = false;
        }

        private RectTransform StarRow(Transform parent, int earned, float size = 30f)
        {
            var row = _ui.Row(parent, 3f);
            FillStarRow(row, earned, size);
            return row;
        }
        private List<RectTransform> FillStarRow(RectTransform row, int earned, float size = 30f)
        {
            for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
            var frames = StarFrames();
            var stars = new List<RectTransform>();
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(row, false);
                var le = go.AddComponent<LayoutElement>(); le.preferredWidth = size; le.preferredHeight = size;
                var img = go.GetComponent<Image>();
                bool got = i < earned;
                if (frames.Length > 0)
                {
                    // Pixel star: earned ones play the animation in full colour; unearned
                    // ones sit dim on the first frame.
                    img.sprite = frames[0]; img.preserveAspect = true;
                    img.color = got ? Color.white : new Color(0.30f, 0.32f, 0.38f, 1f);
                    if (got) go.AddComponent<VfxUiPreview>().Play(frames, 12f);
                }
                else
                {
                    img.sprite = UISprites.Star();
                    img.color = got ? UIFactory.Gold : new Color(0.30f, 0.32f, 0.38f, 1f);
                }
                stars.Add((RectTransform)go.transform);
            }
            return stars;
        }

        // Resources/UI/Star.png is a 13-frame animation strip; slice + cache it once.
        private Sprite[] _starFrames;
        private Sprite[] StarFrames()
        {
            if (_starFrames != null) return _starFrames;
            var tex = Resources.Load<Texture2D>("UI/Star");
            if (tex == null) { _starFrames = new Sprite[0]; return _starFrames; }
            const int frames = 13;
            int fw = tex.width / frames, fh = tex.height;
            _starFrames = new Sprite[frames];
            for (int i = 0; i < frames; i++)
                _starFrames[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, fh), new Vector2(0.5f, 0.5f), fh);
            return _starFrames;
        }

        public void SetAdventureHud(bool on, int moves, string levelName, int enemiesLeft = -1)
        {
            if (_advMovesLabel != null)
            {
                _advMovesLabel.gameObject.SetActive(on);
                if (on)
                    _advMovesLabel.text = enemiesLeft >= 0
                        ? levelName + "\nMoves: " + moves + "   Enemies: " + enemiesLeft
                        : levelName + "\nMoves: " + moves;
            }
            if (_modeBadgeRt != null) _modeBadgeRt.gameObject.SetActive(!on); // hide the mode badge in adventure
            if (_scoreText != null) _scoreText.gameObject.SetActive(!on); // hide trap score in adventure
        }
        private void BuildAdventureResult()
        {
            var canvas = _ui.CreateCanvas("AdvResult", 116);
            _advResult = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, new Color(0.08f, 0.08f, 0.16f, 0.96f));
            var col = _ui.Column(screen.transform, 14f);
            _advResultTitle = _ui.Label(col, "", 26, UIFactory.Gold, FontStyles.Normal);
            _advResultStarRow = _ui.Row(col, 6f);
            // Reward indicator: shows the 500-shilling bonus for a 3-star clear.
            _advResultReward = _ui.Label(col, "", 15, UIFactory.Gold, FontStyles.Normal, 320);
            _advResultInfo = _ui.Label(col, "", 15, UIFactory.Text, FontStyles.Normal, 320);
            _advNextBtn = _ui.Button(col, "Next Level", () => OnNextLevel(), UIFactory.Gold, UIFactory.Bg, 200, 50, 16).gameObject;
            _ui.Button(col, "Retry", () => _gm.RetryAdventure(), UIFactory.Panel, UIFactory.Gold, 200, 50, 16);
            _ui.Button(col, "Level Select", () => ShowLevelSelect(), UIFactory.Panel, UIFactory.Gold, 200, 46, 15);

            // Shillings counter, top-right (mirrors the home menu); revealed and
            // counted up when a 3-star clear awards the 500 bonus.
            _advResultShillings = _ui.Label(screen.transform, "", 15, UIFactory.Gold, FontStyles.Normal);
            _advResultShillings.alignment = TextAlignmentOptions.Right;
            _advResultShillings.textWrappingMode = TextWrappingModes.NoWrap;
            var advShillRt = _advResultShillings.rectTransform;
            advShillRt.anchorMin = advShillRt.anchorMax = new Vector2(1f, 1f);
            advShillRt.pivot = new Vector2(1f, 1f);
            advShillRt.sizeDelta = new Vector2(260f, 32f);
            advShillRt.anchoredPosition = new Vector2(-14f, -14f);
            _advResultShillings.gameObject.SetActive(false);

            _advResult.SetActive(false);
        }
        private void OnNextLevel() { if (_advNextLevel != null) _gm.StartAdventureLevel(_advNextLevel); }
        private static LevelData NextLevel(LevelData cur)
        {
            var all = AdventureLevels.All;
            for (int i = 0; i < all.Length - 1; i++)
                if (all[i] == cur)
                {
                    var next = all[i + 1];
                    return IsWorldAvailable(WorldOf(next)) ? next : null; // stop at the beta's last world
                }
            return null;
        }
        public void ShowAdventureResult(LevelData lvl, int moves, int stars, int reward = 0, int balanceBefore = 0)
        {
            HideAll();
            _gm.SetBanner(true);
            if (_advResultCo != null) { StopCoroutine(_advResultCo); _advResultCo = null; }
            if (_advResultTitle != null) _advResultTitle.text = "Level Complete!";
            var starRects = FillStarRow(_advResultStarRow, stars);
            if (_advResultInfo != null)
                _advResultInfo.text = "Solved in " + moves + (moves == 1 ? " move" : " moves") +
                    "\n3 stars: " + lvl.ThreeStar + " moves or fewer";
            if (_advResultReward != null)
            {
                _advResultReward.gameObject.SetActive(true);
                if (stars == 3)
                {
                    _advResultReward.color = UIFactory.Gold;
                    _advResultReward.text = reward > 0 ? "Bonus:  +500 shillings!" : "Perfect clear!";
                }
                else
                {
                    _advResultReward.color = UIFactory.Muted;
                    _advResultReward.text = "Earn 3 stars for +500 shillings";
                }
            }
            if (_advResultShillings != null) _advResultShillings.gameObject.SetActive(false);
            _advNextLevel = NextLevel(lvl);
            if (_advNextBtn != null) _advNextBtn.SetActive(_advNextLevel != null);
            _advResult.SetActive(true);
            _advResultCo = StartCoroutine(AdvResultRoutine(starRects, stars, reward, balanceBefore));
        }
        // Stars pop in one-by-one; a 3-star clear then reveals the top-right shilling
        // counter and ticks it up by the 500 reward.
        private System.Collections.IEnumerator AdvResultRoutine(List<RectTransform> starRects, int stars, int reward, int balanceBefore)
        {
            for (int i = 0; i < starRects.Count && i < stars; i++)
                starRects[i].localScale = Vector3.zero; // collapse earned stars so they can pop

            yield return new WaitForSecondsRealtime(0.18f);
            for (int i = 0; i < starRects.Count && i < stars; i++)
            {
                yield return PopIn(starRects[i], 0.24f);
                yield return new WaitForSecondsRealtime(0.14f);
            }

            if (stars == 3 && reward > 0 && _advResultShillings != null)
            {
                yield return new WaitForSecondsRealtime(0.15f);
                int target = balanceBefore + reward;
                _advResultShillings.gameObject.SetActive(true);
                _advResultShillings.text = balanceBefore + " shillings   <color=#57BD79>+" + reward + "</color>";

                float dur = 0.7f, t = 0f;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    int val = Mathf.RoundToInt(Mathf.Lerp(balanceBefore, target, Mathf.Clamp01(t / dur)));
                    _advResultShillings.text = val + " shillings   <color=#57BD79>+" + reward + "</color>";
                    yield return null;
                }
                _advResultShillings.text = target + " shillings";
            }
            _advResultCo = null;
        }
        // easeOutBack scale-in for a satisfying overshoot pop.
        private static System.Collections.IEnumerator PopIn(RectTransform rt, float dur)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / dur);
                float bp = p - 1f;
                float s = 1f + c3 * bp * bp * bp + c1 * bp * bp;
                rt.localScale = Vector3.one * s;
                yield return null;
            }
            rt.localScale = Vector3.one;
        }
        public void ShowAdventureFail(LevelData lvl)
        {
            HideAll();
            _gm.SetBanner(true);
            if (_advResultCo != null) { StopCoroutine(_advResultCo); _advResultCo = null; }
            if (_advResultTitle != null) _advResultTitle.text = "Try Again";
            FillStarRow(_advResultStarRow, 0);
            if (_advResultReward != null) _advResultReward.gameObject.SetActive(false);
            if (_advResultShillings != null) _advResultShillings.gameObject.SetActive(false);
            if (_advResultInfo != null) _advResultInfo.text = "You didn't make it. Give it another go!";
            if (_advNextBtn != null) _advNextBtn.SetActive(false); // no advancing after a fail
            _advResult.SetActive(true);
        }

        // ── Adventure front door: intro + paged level select ─────────────────
        private void OnAdventurePressed()
        {
            if (PlayerPrefs.GetInt("kg_seen_adventure", 0) == 0) { HideAll(); _advIntro.SetActive(true); }
            else ShowLevelSelect();
        }
        private void BuildAdventureIntro()
        {
            var canvas = _ui.CreateCanvas("AdvIntro", 105);
            _advIntro = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 16f);
            _ui.Label(col, "Adventure", 30, UIFactory.Gold, FontStyles.Normal);
            _ui.Label(col, "Hand-crafted chess puzzles. Move the knight in L-shapes to reach the goal or capture every enemy, in as few moves as you can. Fewer moves earns more stars, up to three.", 15, UIFactory.Text, FontStyles.Normal, 340);
            _ui.Label(col, "Some enemies bob between two squares each time you move. Watch the red outline, it shows where a piece will move next.", 14, UIFactory.Muted, FontStyles.Normal, 340);
            _ui.Label(col, "Adventure has a single difficulty, so there's no difficulty to choose.", 13, UIFactory.Amber, FontStyles.Normal, 340);
            _ui.Button(col, "Continue", () => { PlayerPrefs.SetInt("kg_seen_adventure", 1); PlayerPrefs.Save(); ShowLevelSelect(); }, UIFactory.Gold, UIFactory.Bg, 220, 52, 18);
            _advIntro.SetActive(false);
        }
        private void BuildLevelSelect()
        {
            var canvas = _ui.CreateCanvas("LevelSelect", 104);
            _levelSelect = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 16f);
            _ui.Label(col, "Adventure", 30, UIFactory.Gold, FontStyles.Normal);
            _levelPageLabel = _ui.Label(col, "", 16, UIFactory.Muted);
            _levelRow = _ui.Column(col, 10f); // holds two rows of level icons (1-3 / 4-6)
            var nav = _ui.Row(col, 12f);
            _ui.Button(nav, "\u2039 Prev", () => AdvPageFlip(-1), UIFactory.Panel, UIFactory.Gold, 120, 44, 15);
            _ui.Button(nav, "Next \u203A", () => AdvPageFlip(1), UIFactory.Panel, UIFactory.Gold, 120, 44, 15);
            _ui.Button(col, "Back", () => ShowMenu(), UIFactory.Panel, UIFactory.Gold, 200, 46, 15);

            var worlds = new List<string>();
            foreach (var lvl in AdventureLevels.All)
            {
                var w = lvl.Id.Split('-')[0];
                if (!worlds.Contains(w)) worlds.Add(w);
            }
            _advWorlds = worlds.ToArray();
            _levelSelect.SetActive(false);
        }
        public void ShowLevelSelect() { HideAll(); RenderLevelSelect(); _levelSelect.SetActive(true); _gm.SetBanner(true); }
        private void AdvPageFlip(int d)
        {
            int next = _advPage + d;
            // Later worlds stay a mystery: you can't scroll to a world until the
            // previous one is cleared (every level in it earned at least one star).
            if (next >= UnlockedWorldCount())
            {
                ShowToast("Earn a star on every level here to unlock the next world.");
                return;
            }
            _advPage = next;
            RenderLevelSelect();
        }
        // Highest world available to play. All seven worlds now ship; lower this to gate
        // later worlds again (e.g. for a closed beta). Worlds still unlock strictly in
        // order via the star chain (IsLevelUnlocked) up to this cap.
        private const int BetaMaxWorld = 7;

        // Debug switch: unlocks every world and level (bypasses both the beta world gate
        // and the star-chain). Must stay false in any shipped build.
        private static readonly bool DebugUnlockAllLevels = false;

        private static bool IsWorldAvailable(string world)
            => DebugUnlockAllLevels || !int.TryParse(world, out var n) || n <= BetaMaxWorld;
        private static string WorldOf(LevelData lvl) => lvl.Id.Split('-')[0];

        // A level is unlocked if it's the very first, or the level immediately
        // before it (across world boundaries) was cleared with at least one star.
        // Levels in a not-yet-shipped world are always locked.
        private static bool IsLevelUnlocked(LevelData lvl)
        {
            if (DebugUnlockAllLevels) return true;
            if (!IsWorldAvailable(WorldOf(lvl))) return false;
            var all = AdventureLevels.All;
            for (int i = 0; i < all.Length; i++)
                if (all[i] == lvl)
                    return i == 0 || SaveData.GetStars(all[i - 1].Id) >= 1;
            return false;
        }
        // Number of worlds the player can currently reach. Worlds unlock strictly
        // in order, so this is the count up to (and including) the first world
        // whose opening level isn't yet unlocked.
        private int UnlockedWorldCount()
        {
            if (_advWorlds == null) return 0;
            int count = 0;
            foreach (var world in _advWorlds)
            {
                if (!IsWorldAvailable(world)) break; // later worlds ship in the full release
                LevelData first = null;
                foreach (var lvl in AdventureLevels.All)
                    if (lvl.Id.Split('-')[0] == world) { first = lvl; break; }
                if (first == null || !IsLevelUnlocked(first)) break;
                count++;
            }
            return Mathf.Max(1, count);
        }
        private void RenderLevelSelect()
        {
            if (_advWorlds == null || _advWorlds.Length == 0) return;
            int unlockedWorlds = UnlockedWorldCount();
            _advPage = Mathf.Clamp(_advPage, 0, unlockedWorlds - 1);
            string world = _advWorlds[_advPage];
            // Denominator shows only the worlds unlocked so far, keeping the total a mystery.
            _levelPageLabel.text = "World " + world + "   (" + (_advPage + 1) + " / " + unlockedWorlds + ")";
            for (int i = _levelRow.childCount - 1; i >= 0; i--) Destroy(_levelRow.GetChild(i).gameObject);

            // Collect this world's levels in order, then lay them out three per row
            // (1-3 on top, 4-6 below).
            var levels = new List<LevelData>();
            foreach (var lvl in AdventureLevels.All)
                if (lvl.Id.Split('-')[0] == world) levels.Add(lvl);

            RectTransform gridRow = null;
            for (int i = 0; i < levels.Count; i++)
            {
                if (i % 3 == 0) gridRow = _ui.Row(_levelRow, 14f);
                var lvl = levels[i];
                var cell = _ui.Column(gridRow, 6f);
                if (IsLevelUnlocked(lvl))
                {
                    // The queen-boss level shows "<world>-♔" (crown glyph, like the leaderboard).
                    var lbtn = _ui.Button(cell, lvl.IsBoss() ? "" : lvl.Id, () => _gm.StartAdventureLevel(lvl), UIFactory.Panel, UIFactory.Gold, 84, 84, 22, clickSfx: Sfx.PlaySelect);
                    if (lvl.IsBoss()) BuildCrownTileLabel(lbtn, WorldOf(lvl));
                    StarRow(cell, SaveData.GetStars(lvl.Id), 22f);
                }
                else
                {
                    // Locked: a dimmed, non-interactive tile showing a padlock.
                    var btn = _ui.Button(cell, "", () => ShowToast("Clear the previous level to unlock this one."),
                        new Color(0.12f, 0.13f, 0.20f, 1f), UIFactory.Muted, 84, 84, 22);
                    AddIconChild(btn.gameObject, UISprites.Lock(), UIFactory.Muted, 34);
                    StarRow(cell, 0, 22f); // spacer so locked/unlocked tiles line up
                }
            }
        }

        // ── Lootbox marketplace ──────────────────────────────────────────────
        private void BuildMarketplace()
        {
            var canvas = _ui.CreateCanvas("Market", 108);
            _market = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 10f);
            _ui.Label(col, "Shop", 28, UIFactory.Gold, FontStyles.Normal);
            _marketBalance = _ui.Label(col, "", 20, UIFactory.Gold, FontStyles.Normal);
            _ui.Button(col, "Buy Shillings", () => ShowShillingsShop(), UIFactory.Panel, UIFactory.Gold, 200, 40, 17);

            var tabs = _ui.Row(col, 10f);
            _tabLootbox = _ui.Button(tabs, "Lootbox", () => SetShopTab(0), UIFactory.Panel, UIFactory.Muted, 132, 42, 17);
            _tabPowerups = _ui.Button(tabs, "Powerups", () => SetShopTab(1), UIFactory.Panel, UIFactory.Muted, 132, 42, 17);

            // ── Lootbox tab ──
            _shopLootbox = _ui.Column(col, 10f);
            BuildChest(_shopLootbox, () => ShowLootboxOdds());
            _ui.Label(_shopLootbox, "Tap the chest for drop odds", 13, UIFactory.Muted);
            _ui.Button(_shopLootbox, "Open  (" + LootboxCost + " sh)", () => OnOpenLootbox(), UIFactory.Gold, UIFactory.Bg, 240, 54, 19);

            // ── Powerups tab ──
            _shopPowerups = _ui.Column(col, 12f);
            BuildPowerupRow(_shopPowerups, Powerups.Chronokinesis, 0);
            BuildPowerupRow(_shopPowerups, Powerups.DivineIntervention, 1);
            BuildPowerupRow(_shopPowerups, Powerups.DivinePath, 2);

            // Compact status line for errors ("not enough shillings") / confirmations.
            _marketMsg = _ui.Label(col, "", 17, UIFactory.Gold, FontStyles.Normal, 300);

            _ui.Button(col, "Back", () => ShowMenu(), UIFactory.Gold, UIFactory.Bg, 220, 50, 22);
            _market.SetActive(false);

            BuildShillingsShop();
            SetShopTab(0);
        }

        // One shop row for a powerup: icon, name + blurb + owned count, and a Buy button.
        private void BuildPowerupRow(Transform parent, PowerupInfo pu, int idx)
        {
            var row = _ui.Row(parent, 10f);
            var iconGo = new GameObject("PuIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(row, false);
            var le = iconGo.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 56f; le.preferredHeight = le.minHeight = 56f;
            var img = iconGo.GetComponent<Image>(); img.raycastTarget = false;
            if (pu.Icon != null) { img.sprite = pu.Icon; img.preserveAspect = true; }
            else { img.sprite = _ui.Fill(); img.color = UIFactory.Panel; }

            var info = _ui.Column(row, 2f);
            _ui.Label(info, pu.Name, 18, UIFactory.Gold, FontStyles.Normal, 210);
            _ui.Label(info, pu.Blurb, 13, UIFactory.Muted, FontStyles.Normal, 210);
            _powerupOwned[idx] = _ui.Label(info, "", 14, UIFactory.Text, FontStyles.Normal, 210);

            var pcopy = pu;
            _ui.Button(row, pu.Cost + " sh", () => OnBuyPowerup(pcopy), UIFactory.Gold, UIFactory.Bg, 96, 50, 16);
        }

        private void OnBuyPowerup(PowerupInfo pu)
        {
            if (!SaveData.TrySpendShillings(pu.Cost))
            {
                _marketMsg.color = UIFactory.Error; _marketMsg.text = "Not enough shillings.";
                return;
            }
            SaveData.AddPowerup(pu.Key, 1);
            _marketMsg.color = UIFactory.Gold; _marketMsg.text = "Bought " + pu.Name + "!";
            RebuildMarket();
        }

        private void SetShopTab(int tab)
        {
            _shopTab = tab;
            if (_shopLootbox != null) _shopLootbox.gameObject.SetActive(tab == 0);
            if (_shopPowerups != null) _shopPowerups.gameObject.SetActive(tab == 1);
            if (_tabLootbox != null) UIFactory.StyleToggle(_tabLootbox, tab == 0);
            if (_tabPowerups != null) UIFactory.StyleToggle(_tabPowerups, tab == 1);
            if (_marketMsg != null) _marketMsg.text = "";
            RebuildMarket();
        }

        // ── Buy Shillings (real-money bundles) ───────────────────────────────
        // Consumable IAP via _gm.Iap. Prices come from the store (falling back to the
        // catalog price until fetched); grants and failures arrive as events, so a
        // purchase completed on a previous launch still updates the balance here.
        private readonly Dictionary<string, Button> _shillingBuyButtons = new Dictionary<string, Button>();

        private void BuildShillingsShop()
        {
            var canvas = _ui.CreateCanvas("ShillingsShop", 109);
            _shillingsShop = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 12f);
            _ui.Label(col, "Buy Shillings", 28, UIFactory.Gold, FontStyles.Normal);
            _shillingsShopBalance = _ui.Label(col, "", 18, UIFactory.Gold, FontStyles.Normal);
            if (_gm.Iap is StubIapService)
                _ui.Label(col, "Purchases are simulated (no real charge until full release).", 13, UIFactory.Muted, FontStyles.Normal, 340);
            foreach (var b in IapCatalog.Shillings) BuildShillingBundle(col, b.ProductId, b.Shillings);
            _ui.Button(col, "Back", () => ShowMarketplace(), UIFactory.Gold, UIFactory.Bg, 220, 50, 22);
            _shillingsShop.SetActive(false);

            _gm.Iap.OnReady += RefreshShillingPrices;
            _gm.Iap.OnPurchased += OnShillingsPurchased;
            _gm.Iap.OnPurchaseFailed += OnShillingsPurchaseFailed;
        }

        private void BuildShillingBundle(Transform parent, string productId, int amount)
        {
            var row = _ui.Row(parent, 12f);
            _ui.Label(row, amount.ToString("N0") + " sh", 20, UIFactory.Gold, FontStyles.Normal, 180);
            var btn = _ui.Button(row, _gm.Iap.PriceString(productId), () => _gm.Iap.Buy(productId),
                                 UIFactory.Gold, UIFactory.Bg, 130, 48, 18);
            _shillingBuyButtons[productId] = btn;
        }

        // Store prices arrived: update each button's label from the fetched catalog.
        private void RefreshShillingPrices()
        {
            foreach (var kv in _shillingBuyButtons)
            {
                var lab = kv.Value != null ? kv.Value.GetComponentInChildren<TMP_Text>() : null;
                if (lab != null) lab.text = _gm.Iap.PriceString(kv.Key);
            }
        }

        private void OnShillingsPurchased(string productId)
        {
            int amount = IapCatalog.ShillingsFor(productId); // already granted by the service
            if (_shillingsShopBalance != null) _shillingsShopBalance.text = _gm.Shillings + " shillings";
            ShowToast("Purchased " + amount.ToString("N0") + " shillings");
        }

        private void OnShillingsPurchaseFailed(string productId, bool cancelled)
        {
            if (!cancelled) ShowToast("Purchase failed. You were not charged.");
        }

        private void ShowShillingsShop()
        {
            HideAll();
            if (_shillingsShopBalance != null) _shillingsShopBalance.text = _gm.Shillings + " shillings";
            _shillingsShop.SetActive(true);
            _gm.SetBanner(true);
        }
        // Red-chest button that replaces the old "?" placeholder. It sits idle on the
        // closed frame; the open animation plays once, on demand, when the Open button
        // is pressed (see OpenChestThenReveal). Tapping the chest opens the drop odds.
        private Sprite[] _chestFrames;
        private Image _chestImg;
        private void BuildChest(Transform parent, System.Action onTap)
        {
            var go = new GameObject("ChestButton", typeof(RectTransform), typeof(Image),
                                    typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 132f;
            le.preferredHeight = le.minHeight = 120f;
            var img = go.GetComponent<Image>();
            var frames = ChestFrames();
            if (frames.Length > 0)
            {
                img.sprite = frames[0]; // closed chest, held until Open is pressed
                img.preserveAspect = true;
                _chestImg = img;
            }
            else
            {
                // Fallback to the old placeholder look if the sheet is missing.
                img.sprite = _ui.Fill(); img.type = Image.Type.Sliced; img.color = UIFactory.Panel;
            }
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (onTap != null) btn.onClick.AddListener(() => onTap());
        }

        // First 12 frames of the lootbox sheet (the red/gold chest in its top-left
        // corner), sorted naturally (0,1,…,11). Cached; empty array on a miss.
        private Sprite[] ChestFrames()
        {
            if (_chestFrames != null) return _chestFrames;
            var all = Resources.LoadAll<Sprite>("Lootbox/Fantasy RPG (Toony) 32x32");
            if (all != null && all.Length > 0)
            {
                var list = new List<Sprite>(all);
                list.Sort((a, b) => VfxPlayer.NaturalCompare(a.name, b.name));
                _chestFrames = list.GetRange(0, Mathf.Min(12, list.Count)).ToArray();
            }
            else _chestFrames = new Sprite[0];
            return _chestFrames;
        }
        private void ShowMarketplace()
        {
            HideAll();
            if (_chestCo != null) { StopCoroutine(_chestCo); _chestCo = null; }
            var frames = ChestFrames();
            if (_chestImg != null && frames.Length > 0) _chestImg.sprite = frames[0]; // reset to closed
            RebuildMarket();
            _marketMsg.text = "";
            _market.SetActive(true);
            _gm.SetBanner(true);
        }
        private void RebuildMarket()
        {
            if (_marketBalance != null) _marketBalance.text = _gm.Shillings + " shillings";
            for (int i = 0; i < Powerups.All.Length && i < _powerupOwned.Length; i++)
                if (_powerupOwned[i] != null) _powerupOwned[i].text = "Owned: " + Powerups.All[i].Owned;
        }
        private void OnOpenLootbox()
        {
            if (_chestCo != null) return; // an open is already playing out
            if (!SaveData.TrySpendShillings(LootboxCost))
            {
                _marketMsg.color = UIFactory.Error; _marketMsg.text = "Not enough shillings.";
                return;
            }
            SfxManager.Play(Sfx.LootboxOpen); // fires the moment the box is opened
            // A small slice of opens award a rare movement VFX; otherwise the winner is
            // a uniform-random Shop skin. The winner is rolled now but the reveal is
            // held: the chest plays its open animation, then the shop scrolls into the
            // reel (see OpenChestThenReveal). Both prize kinds ride the same scroller.
            Prize won; bool dupe;
            if (Vfx.All.Length > 0 && UnityEngine.Random.value < VfxDropChance)
            {
                var v = Vfx.All[UnityEngine.Random.Range(0, Vfx.All.Length)];
                dupe = SaveData.OwnsVfx(v.Id);
                SaveData.AddOwnedVfx(v.Id);
                won = new Prize { vfx = v };
            }
            else
            {
                var s = Skins.Shop[UnityEngine.Random.Range(0, Skins.Shop.Length)];
                dupe = SaveData.OwnsShopSkin(s.Id);
                SaveData.AddOwnedSkin(s.Id);
                won = new Prize { skin = s };
            }
            RebuildMarket();
            var wonPrize = won; bool wonDupe = dupe; // capture for the deferred prepare
            _chestCo = StartCoroutine(OpenChestThenReveal(() => PrepareCaseOpen(wonPrize, wonDupe), StartCaseSpin));
        }

        // Plays the chest-open animation once, then scrolls the shop up and hands off to
        // the reveal screen the caller prepared (a spinning reel, or a plain VFX reveal).
        private System.Collections.IEnumerator OpenChestThenReveal(System.Action prepare, System.Action onArrive)
        {
            yield return PlayChestOpen();
            yield return new WaitForSecondsRealtime(0.12f); // let the open frame land
            prepare();                                      // build the reveal, still off-screen
            SfxManager.PlayLootboxScroll(); // scroll sting: pauses menu music, fades it back after
            yield return ScrollDownTransition(_market, _caseOpen, 0.28f);
            onArrive?.Invoke();                             // start the reel spin (skins only)
            _chestCo = null;
        }

        // Advance the chest through its 12 frames once, ending on the fully-open frame.
        private System.Collections.IEnumerator PlayChestOpen()
        {
            var frames = ChestFrames();
            if (_chestImg == null || frames.Length == 0) yield break;
            var wait = new WaitForSecondsRealtime(1f / 16f);
            for (int i = 0; i < frames.Length; i++)
            {
                _chestImg.sprite = frames[i];
                yield return wait;
            }
        }

        // Fast downward slide: the current screen exits off the bottom while the next
        // descends into place from above. Both are full-screen "Screen" children of
        // their canvases, translated by offsetting their stretched rects. Unscaled time.
        private System.Collections.IEnumerator ScrollDownTransition(GameObject fromCanvas, GameObject toCanvas, float dur)
        {
            toCanvas.SetActive(true);
            var from = fromCanvas.transform.GetChild(0) as RectTransform;
            var to = toCanvas.transform.GetChild(0) as RectTransform;
            float h = from != null ? from.rect.height : 0f;
            if (h < 1f) h = 1200f;
            SetScreenY(to, h);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dur), 3f); // easeOutCubic
                SetScreenY(from, Mathf.Lerp(0f, -h, e));
                SetScreenY(to, Mathf.Lerp(h, 0f, e));
                yield return null;
            }
            SetScreenY(to, 0f);
            SetScreenY(from, 0f);
            fromCanvas.SetActive(false);
        }
        private static void SetScreenY(RectTransform rt, float y)
        {
            if (rt == null) return;
            rt.offsetMin = new Vector2(0f, y);
            rt.offsetMax = new Vector2(0f, y);
        }

        // Chance a single lootbox open yields a VFX instead of a skin (25% VFX / 75%
        // skins). Spread across the VFX pool, each effect stays rarer than each skin.
        private const float VfxDropChance = 0.25f;

        // ── CS:GO-style case opening ─────────────────────────────────────────
        private void BuildCaseOpen()
        {
            var canvas = _ui.CreateCanvas("CaseOpen", 114);
            _caseOpen = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, new Color(0.05f, 0.05f, 0.11f, 1f));
            var col = _ui.Column(screen.transform, 18f);
            _ui.Label(col, "Opening Lootbox", 24, UIFactory.Gold, FontStyles.Normal);

            // Viewport: masks the scrolling reel to a fixed window.
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            vpGo.transform.SetParent(col, false);
            _caseViewport = vpGo.GetComponent<RectTransform>();
            _caseViewport.sizeDelta = new Vector2(CaseViewW, CaseCellH + 12f);
            var vple = vpGo.AddComponent<LayoutElement>();
            vple.preferredWidth = CaseViewW; vple.preferredHeight = CaseCellH + 12f;
            var vpImg = vpGo.GetComponent<Image>();
            vpImg.sprite = _ui.Fill(); vpImg.type = Image.Type.Sliced;
            vpImg.color = new Color(0f, 0f, 0f, 0.35f);

            // Reel: cells are positioned manually and the whole strip is scrolled by
            // animating anchoredPosition.x.
            var reelGo = new GameObject("Reel", typeof(RectTransform));
            reelGo.transform.SetParent(_caseViewport, false);
            _caseReel = reelGo.GetComponent<RectTransform>();
            _caseReel.anchorMin = _caseReel.anchorMax = _caseReel.pivot = new Vector2(0.5f, 0.5f);
            _caseReel.anchoredPosition = Vector2.zero;

            // Fixed centre marker: a gold square outline + a bright tick, drawn on
            // top of the reel (added after it, still inside the mask).
            var markGo = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            markGo.transform.SetParent(vpGo.transform, false);
            var mrt = markGo.GetComponent<RectTransform>();
            mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.sizeDelta = new Vector2(CaseCellW, CaseCellH + 6f);
            var mimg = markGo.GetComponent<Image>();
            mimg.sprite = _ui.Border(); mimg.type = Image.Type.Sliced;
            mimg.color = UIFactory.Gold; mimg.raycastTarget = false;

            var tickGo = new GameObject("Tick", typeof(RectTransform), typeof(Image));
            tickGo.transform.SetParent(vpGo.transform, false);
            var trt = tickGo.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(4f, CaseCellH + 6f);
            var timg = tickGo.GetComponent<Image>();
            timg.sprite = _ui.Fill(); timg.color = UIFactory.Gold; timg.raycastTarget = false;

            _caseResultLabel = _ui.Label(col, "", 18, UIFactory.Gold, FontStyles.Normal, 360);
            _caseContinueBtn = _ui.Button(col, "Continue", () => ShowMarketplace(), UIFactory.Gold, UIFactory.Bg, 220, 50, 17).gameObject;

            _caseOpen.SetActive(false);
        }
        // Builds one reel cell for a prize. Skins show their knight glyph; VFX show a
        // still first frame (never animated while scrolling). Returns the VFX Image so
        // the caller can animate it once it's the landed winner, or null for skins.
        private Image BuildCaseCell(RectTransform reel, int index, Prize prize)
        {
            var accent = prize.Accent;
            var cellGo = new GameObject("Cell", typeof(RectTransform), typeof(Image));
            cellGo.transform.SetParent(reel, false);
            var rt = cellGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CaseCellW - 14f, CaseCellH - 12f);
            rt.anchoredPosition = new Vector2(index * CaseCellW, 0f);
            var bg = cellGo.GetComponent<Image>();
            bg.sprite = _ui.Fill(); bg.type = Image.Type.Sliced;
            var fill = accent; fill.a = 0.16f; bg.color = fill;

            var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(rt, false);
            var brt = borderGo.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bimg = borderGo.GetComponent<Image>();
            bimg.sprite = _ui.Border(); bimg.type = Image.Type.Sliced;
            var bc = accent; bc.a = 0.85f; bimg.color = bc; bimg.raycastTarget = false;

            Image vfxImg = null;
            var frames = prize.IsVfx ? VfxPlayer.Frames(prize.vfx) : null;
            if (prize.IsVfx && frames != null && frames.Length > 0)
            {
                // Static first frame. The effect only comes alive once it's won.
                var vfxGo = new GameObject("Vfx", typeof(RectTransform), typeof(Image));
                vfxGo.transform.SetParent(rt, false);
                var vrt = vfxGo.GetComponent<RectTransform>();
                vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0.5f, 0.5f);
                vrt.sizeDelta = new Vector2(CaseCellW - 30f, CaseCellW - 30f);
                vrt.anchoredPosition = new Vector2(0f, 14f);
                vfxImg = vfxGo.GetComponent<Image>();
                vfxImg.sprite = frames[0];
                vfxImg.preserveAspect = true;
                vfxImg.raycastTarget = false;
            }
            else
            {
                // Skins (and any VFX whose frames are missing) show the knight glyph.
                var glyph = _ui.Label(rt, "♞", 62, accent, FontStyles.Normal, CaseCellW);
                if (_theme != null && _theme.glyphFont != null) glyph.font = _theme.glyphFont;
                var grt = glyph.rectTransform;
                grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
                grt.sizeDelta = new Vector2(CaseCellW, 96f);
                grt.anchoredPosition = new Vector2(0f, 14f);
            }

            var name = _ui.Label(rt, prize.Name, 13, Color.white, FontStyles.Normal, CaseCellW - 18f);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            var nrt = name.rectTransform;
            nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0.5f, 0f);
            nrt.sizeDelta = new Vector2(CaseCellW - 18f, 26f);
            nrt.anchoredPosition = new Vector2(0f, 10f);
            return vfxImg;
        }
        // Builds the reel and activates the case screen off-screen, but holds the spin:
        // the scroll transition slides it into view first, then StartCaseSpin runs.
        private void PrepareCaseOpen(Prize won, bool dupe)
        {
            if (_caseCo != null) { StopCoroutine(_caseCo); _caseCo = null; }
            for (int i = _caseReel.childCount - 1; i >= 0; i--) Destroy(_caseReel.GetChild(i).gameObject);
            _caseViewport.gameObject.SetActive(true);
            _winnerVfxImg = null;

            const int N = 56;
            int winIndex = N - 8; // leave a tail of cells to the right of the winner
            for (int i = 0; i < N; i++)
            {
                // The reel mixes skins and (statically shown) VFX; the winning cell is
                // the actual prize. Only the winner is animated, after the spin lands.
                var prize = (i == winIndex) ? won : RandomFillerPrize();
                var vfxImg = BuildCaseCell(_caseReel, i, prize);
                if (i == winIndex) _winnerVfxImg = vfxImg;
            }
            _caseReel.anchoredPosition = new Vector2(-3f * CaseCellW, 0f); // show cells during scroll-in

            _caseResultLabel.text = "";
            _caseResultLabel.gameObject.SetActive(false);
            _caseContinueBtn.SetActive(false);
            _caseOpen.SetActive(true);
            _pendingWin = winIndex; _pendingWon = won; _pendingDupe = dupe;
        }
        // A random reel filler: mostly skins, with an occasional VFX so both prize kinds
        // are visible while the reel scrolls (matching the ~1-in-4 VFX drop rate).
        private Prize RandomFillerPrize()
        {
            if (Vfx.All.Length > 0 && UnityEngine.Random.value < VfxDropChance)
                return new Prize { vfx = Vfx.All[UnityEngine.Random.Range(0, Vfx.All.Length)] };
            var pool = Skins.Shop;
            return new Prize { skin = pool[UnityEngine.Random.Range(0, pool.Length)] };
        }
        private void StartCaseSpin() { _caseCo = StartCoroutine(SpinReel(_pendingWin, _pendingWon, _pendingDupe)); }
        private System.Collections.IEnumerator SpinReel(int winIndex, Prize won, bool dupe)
        {
            // Start a few cells in, then decelerate to rest with the winner centred
            // under the marker (small random offset so it doesn't stop dead-centre,
            // but always well within the winning cell).
            float startX = -3f * CaseCellW;
            float landOffset = UnityEngine.Random.Range(-CaseCellW * 0.28f, CaseCellW * 0.28f);
            float endX = -winIndex * CaseCellW + landOffset;
            _caseReel.anchoredPosition = new Vector2(startX, 0f);

            float dur = 4.6f, t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float e = 1f - Mathf.Pow(1f - u, 5f); // easeOutQuint: long, satisfying slowdown
                _caseReel.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, e), 0f);
                yield return null;
            }
            _caseReel.anchoredPosition = new Vector2(endX, 0f);

            yield return new WaitForSecondsRealtime(0.3f);

            // Bring the won VFX to life on its landed cell. The effect only animates
            // once it's actually been won (fillers and the pre-land winner stay still).
            if (won.IsVfx && _winnerVfxImg != null)
            {
                var frames = VfxPlayer.Frames(won.vfx);
                if (frames != null && frames.Length > 0)
                    _winnerVfxImg.gameObject.AddComponent<VfxUiPreview>().Play(frames, won.vfx.Fps);
            }

            _caseResultLabel.color = dupe ? UIFactory.Muted : UIFactory.Gold;
            _caseResultLabel.text = won.IsVfx
                ? (dupe ? (won.Name + " VFX  (duplicate)") : ("You unlocked the " + won.Name + " movement VFX!"))
                : (dupe ? (won.Name + "  (duplicate)") : ("You got " + won.Name + "!"));
            _caseResultLabel.gameObject.SetActive(true);
            _caseContinueBtn.SetActive(true);
            _caseCo = null;
        }
        private void BuildLootboxOdds()
        {
            var canvas = _ui.CreateCanvas("Odds", 109);
            _marketOdds = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 8f);
            _ui.Label(col, "Drop Odds", 26, UIFactory.Gold, FontStyles.Normal);
            _ui.Label(col, "swipe ← →  (Skins / VFX)", 12, UIFactory.Muted);

            // Two horizontally-swipeable pages: skins, then the individual VFX odds.
            const float pageW = 380f, pageH = 300f;
            var pagerGo = new GameObject("Pager", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            pagerGo.transform.SetParent(col, false);
            var pagerLe = pagerGo.AddComponent<LayoutElement>();
            pagerLe.preferredWidth = pageW; pagerLe.preferredHeight = pageH;
            var pagerRt = pagerGo.GetComponent<RectTransform>();
            pagerRt.sizeDelta = new Vector2(pageW, pageH);
            pagerGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
            var sr = pagerGo.GetComponent<ScrollRect>();
            sr.horizontal = true; sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.inertia = false; sr.scrollSensitivity = 0f;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(pagerGo.transform, false);
            content.anchorMin = new Vector2(0f, 0f); content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.sizeDelta = new Vector2(pageW * 2f, 0f);
            content.anchoredPosition = Vector2.zero;
            sr.viewport = pagerRt; sr.content = content;

            string skinPct = ((1f - VfxDropChance) * 100f / Skins.Shop.Length).ToString("0.#") + "%";
            var skinsPage = OddsPage(content, 0, pageW, "Skins");
            foreach (var s in Skins.Shop) OddsRow(skinsPage, s.Name, skinPct, s.Color, null);

            string vfxPct = (VfxDropChance * 100f / Mathf.Max(1, Vfx.All.Length)).ToString("0.##") + "%";
            var vfxPage = OddsPage(content, 1, pageW, "Movement VFX");
            foreach (var vf in Vfx.All)
            {
                var frames = VfxPlayer.Frames(vf);
                OddsRow(vfxPage, vf.Name, vfxPct, Color.clear,
                        (frames != null && frames.Length > 0) ? frames[0] : null);
            }

            _ui.Button(col, "Back", () => ShowMarketplace(), UIFactory.Gold, UIFactory.Bg, 220, 50, 22);
            _marketOdds.SetActive(false);
        }
        // One page of the swipeable odds pager: a vertical stack with a header.
        private Transform OddsPage(RectTransform content, int index, float pageW, string title)
        {
            var pageGo = new GameObject("Page" + index, typeof(RectTransform));
            pageGo.transform.SetParent(content, false);
            var prt = pageGo.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.sizeDelta = new Vector2(pageW, 0f);
            prt.anchoredPosition = new Vector2(index * pageW, 0f);
            var v = pageGo.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter; v.spacing = 6f;
            v.padding = new RectOffset(0, 0, 10, 0);
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            _ui.Label(pageGo.transform, title, 16, UIFactory.Gold, FontStyles.Normal);
            return pageGo.transform;
        }
        // A "swatch/icon · name · percent" row shared by both odds pages.
        private void OddsRow(Transform parent, string name, string pct, Color swatch, Sprite icon)
        {
            var row = _ui.Row(parent, 8f);
            var sw = new GameObject("sw", typeof(RectTransform), typeof(Image));
            sw.transform.SetParent(row, false);
            var le = sw.AddComponent<LayoutElement>(); le.preferredWidth = 22; le.preferredHeight = 22;
            var img = sw.GetComponent<Image>();
            if (icon != null) { img.sprite = icon; img.preserveAspect = true; }
            else { img.sprite = _ui.Fill(); img.color = swatch; }
            _ui.Label(row, name, 14, UIFactory.Text, FontStyles.Normal, 150);
            _ui.Label(row, pct, 14, UIFactory.Gold, FontStyles.Normal, 70);
        }
        private void ShowLootboxOdds() { HideAll(); _marketOdds.SetActive(true); _gm.SetBanner(true); }

        // ── Customization grid (all skins, owned + locked) ───────────────────
        private void BuildCustomization()
        {
            var canvas = _ui.CreateCanvas("Custom", 107);
            _custom = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);

            // Full-screen transparent catcher: tapping empty space dismisses the
            // popup. Created before the content so grid buttons render on top of it.
            var catchGo = new GameObject("Catcher", typeof(RectTransform), typeof(Image), typeof(Button));
            catchGo.transform.SetParent(screen.transform, false);
            var cRt = catchGo.GetComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero; cRt.anchorMax = Vector2.one; cRt.offsetMin = Vector2.zero; cRt.offsetMax = Vector2.zero;
            catchGo.GetComponent<Image>().color = new Color(0, 0, 0, 0); // invisible but raycastable
            var cBtn = catchGo.GetComponent<Button>(); cBtn.transition = Selectable.Transition.None;
            cBtn.onClick.AddListener(() => DismissSkinPopup());
            _skinPopupCatcher = catchGo; catchGo.SetActive(false);

            var col = _ui.Column(screen.transform, 12f);
            _ui.Label(col, "Customization", 26, UIFactory.Gold, FontStyles.Normal);

            var tabs = _ui.Row(col, 10f);
            _tabSkins = _ui.Button(tabs, "Skins", () => SetCustomTab(0), UIFactory.Panel, UIFactory.Muted, 130, 42, 15);
            _tabVfx = _ui.Button(tabs, "VFX", () => SetCustomTab(1), UIFactory.Panel, UIFactory.Muted, 130, 42, 15);

            _customGrid = _ui.Column(col, 10f);
            _ui.Button(col, "Back", () => ShowMenu(), UIFactory.Gold, UIFactory.Bg, 220, 50, 22);

            _skinPopup = new GameObject("SkinPopup", typeof(RectTransform), typeof(Image));
            _skinPopup.transform.SetParent(canvas.transform, false);
            var pRt = _skinPopup.GetComponent<RectTransform>();
            pRt.sizeDelta = new Vector2(230, 104); pRt.pivot = new Vector2(0.5f, 0f);
            var pImg = _skinPopup.GetComponent<Image>();
            pImg.sprite = _ui.Fill(); pImg.type = Image.Type.Sliced; pImg.color = new Color(0.10f, 0.11f, 0.18f, 0.98f);
            var pcol = _ui.Column(_skinPopup.transform, 5f);
            _skinPopupName = _ui.Label(pcol, "", 15, UIFactory.Gold, FontStyles.Normal, 210);
            _skinPopupInfo = _ui.Label(pcol, "", 11, UIFactory.Muted, FontStyles.Normal, 210);
            _skinPopupEquip = _ui.Button(pcol, "Equip", () => OnEquipFromPopup(), UIFactory.Gold, UIFactory.Bg, 120, 34, 13).gameObject;
            _skinPopup.SetActive(false);
            _custom.SetActive(false);
        }
        private void ShowCustomization() { HideAll(); RebuildCustomization(); _custom.SetActive(true); _gm.SetBanner(true); }
        private void SetCustomTab(int tab) { _customTab = tab; RebuildCustomization(); }
        private void RebuildCustomization()
        {
            for (int i = _customGrid.childCount - 1; i >= 0; i--) Destroy(_customGrid.GetChild(i).gameObject);
            DismissSkinPopup();
            UIFactory.StyleToggle(_tabSkins, _customTab == 0);
            UIFactory.StyleToggle(_tabVfx, _customTab == 1);
            if (_customTab == 0) RebuildSkinGrid(); else RebuildVfxGrid();
        }
        private void RebuildSkinGrid()
        {
            string equipped = SaveData.GetSkin();
            RectTransform row = null; int perRow = 4, n = 0;
            foreach (var s in Skins.All())
            {
                if (n % perRow == 0) row = _ui.Row(_customGrid, 10f);
                n++;
                bool owned = Skins.IsOwned(s.Id);
                // A Mastery skin stays a mystery until earned: a "?" tile with no name or
                // colour revealed, so the reward is a surprise.
                bool mystery = s.Source == SkinSource.Mastery && !owned;
                var cell = _ui.Column(row, 3f);
                var sk = s;
                RectTransform brt = null;
                var btn = _ui.Button(cell, "", () => OnSkinTap(sk, brt), UIFactory.Panel, UIFactory.Gold, 66, 66, 10);
                brt = btn.GetComponent<RectTransform>();
                // The near-black Nightfall knight barely reads on the dark tile, so sit a
                // soft red glow behind it (added before the glyph so it stays underneath).
                if (!mystery && s.Id == Skins.Secret.Id) AddSkinGlow(btn.transform, 72f);
                // the actual knight, tinted with the skin colour (dimmed if not owned);
                // a mystery skin shows a "?" instead until it's unlocked.
                var tint = mystery ? UIFactory.Muted
                                   : (owned ? s.Color : new Color(s.Color.r, s.Color.g, s.Color.b, 0.20f));
                var glyph = _ui.Label(btn.transform, mystery ? "?" : "\u265E", 40, tint, FontStyles.Normal);
                if (!mystery && _theme != null && _theme.glyphFont != null) glyph.font = _theme.glyphFont; // DejaVu Sans (has the chess glyphs)
                var grt = glyph.rectTransform;
                grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
                glyph.alignment = TextAlignmentOptions.Center; glyph.raycastTarget = false;
                _ui.Label(cell, equipped == s.Id ? "Equipped" : (mystery ? "???" : s.Name), 12,
                    equipped == s.Id ? UIFactory.Gold : (owned ? UIFactory.Text : UIFactory.Muted), FontStyles.Normal, 70);
            }
        }
        // A soft red radial glow, centered in the parent, sized `size`. Used behind the
        // near-black Nightfall knight so it reads on dark backgrounds.
        private void AddSkinGlow(Transform parent, float size)
        {
            var go = new GameObject("skinGlow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UISprites.SoftGlow(); img.raycastTarget = false;
            var c = Skins.GlowOf(Skins.Secret.Id);
            img.color = new Color(c.r, c.g, c.b, 0.85f); // soft-glow fades to 0, so peak can be stronger
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
        }
        private void RebuildVfxGrid()
        {
            string equipped = SaveData.GetVfx();
            RectTransform row = null; int perRow = 4, n = 0;
            foreach (var v in Vfx.Grid())
            {
                if (n % perRow == 0) row = _ui.Row(_customGrid, 10f);
                n++;
                bool owned = Vfx.IsOwned(v.Id);
                var cell = _ui.Column(row, 3f);
                var vf = v;
                RectTransform brt = null;
                var btn = _ui.Button(cell, "", () => OnVfxTap(vf, brt), UIFactory.Panel, UIFactory.Gold, 66, 66, 10);
                brt = btn.GetComponent<RectTransform>();

                // Preview: the first frame of the effect (dimmed if not owned). "None"
                // shows a dash. A missing/failed load falls back to the dash too.
                var frames = v.Id == Vfx.NoneId ? null : VfxPlayer.Frames(v);
                if (frames != null && frames.Length > 0)
                {
                    var icon = new GameObject("icon", typeof(RectTransform), typeof(Image));
                    icon.transform.SetParent(btn.transform, false);
                    var irt = icon.GetComponent<RectTransform>();
                    irt.anchorMin = new Vector2(0.5f, 0.5f); irt.anchorMax = new Vector2(0.5f, 0.5f);
                    irt.pivot = new Vector2(0.5f, 0.5f); irt.sizeDelta = new Vector2(48, 48);
                    irt.anchoredPosition = Vector2.zero;
                    var img = icon.GetComponent<Image>();
                    img.sprite = frames[0]; img.preserveAspect = true; img.raycastTarget = false;
                    img.color = owned ? Color.white : new Color(1f, 1f, 1f, 0.22f);
                    icon.AddComponent<VfxUiPreview>().Play(frames, v.Fps); // loop the animation
                }
                else
                {
                    var lab = _ui.Label(btn.transform, v.Id == Vfx.NoneId ? "-" : "?", 24,
                        owned ? UIFactory.Text : UIFactory.Muted, FontStyles.Normal);
                    UIFactory.Stretch(lab.rectTransform);
                    lab.alignment = TextAlignmentOptions.Center; lab.raycastTarget = false;
                }
                _ui.Label(cell, equipped == v.Id ? "Equipped" : v.Name, 12,
                    equipped == v.Id ? UIFactory.Gold : (owned ? UIFactory.Text : UIFactory.Muted), FontStyles.Normal, 70);
            }
        }
        private void OnVfxTap(VfxInfo v, RectTransform swatch)
        {
            _popupIsVfx = true; _popupVfx = v; _popupSkin = null;
            bool owned = Vfx.IsOwned(v.Id);
            bool equipped = SaveData.GetVfx() == v.Id;
            _skinPopupName.text = v.Name;
            _skinPopupInfo.text = owned ? (equipped ? "Equipped" : "Owned")
                                        : "Find it in a lootbox (rare).";
            _skinPopupEquip.SetActive(owned && !equipped);
            _skinPopup.transform.position = swatch.position + new Vector3(0, 78f, 0);
            _skinPopupCatcher.SetActive(true);
            _skinPopup.transform.SetAsLastSibling();
            _skinPopup.SetActive(true);
        }
        private void DismissSkinPopup()
        {
            if (_skinPopup != null) _skinPopup.SetActive(false);
            if (_skinPopupCatcher != null) _skinPopupCatcher.SetActive(false);
        }
        private void OnSkinTap(SkinInfo s, RectTransform swatch)
        {
            _popupIsVfx = false; _popupSkin = s; _popupVfx = null;
            bool owned = Skins.IsOwned(s.Id);
            bool equipped = SaveData.GetSkin() == s.Id;
            bool mystery = s.Source == SkinSource.Mastery && !owned;
            _skinPopupName.text = mystery ? "???" : s.Name;
            _skinPopupInfo.text = owned ? (equipped ? "Equipped" : "Owned") : SkinRequirement(s);
            _skinPopupEquip.SetActive(owned && !equipped);
            _skinPopup.transform.position = swatch.position + new Vector3(0, 78f, 0);
            _skinPopupCatcher.SetActive(true);
            _skinPopup.transform.SetAsLastSibling(); // keep the popup above the catcher
            _skinPopup.SetActive(true);
        }
        private void OnEquipFromPopup()
        {
            if (_popupIsVfx)
            {
                if (_popupVfx == null) return;
                _gm.EquipVfx(_popupVfx.Id);
            }
            else
            {
                if (_popupSkin == null) return;
                _gm.EquipSkin(_popupSkin.Id);
            }
            DismissSkinPopup();
            RebuildCustomization();
        }
        private static string SkinRequirement(SkinInfo s)
        {
            if (s.Source == SkinSource.Default) return "Default skin.";
            if (s.Source == SkinSource.Shop) return "Find it in a lootbox.";
            // Keep the total number of levels a mystery (like the world select does).
            if (s.Source == SkinSource.Mastery) return "3-star every Adventure level to unlock.";
            foreach (var a in Achievements.All) if (a.SkinId == s.Id) return a.Desc;
            return "";
        }
        private void OnBuy(SkinInfo s) { } // removed: skins are lootbox-only now

        // ── Achievements ─────────────────────────────────────────────────────
        private void BuildAchievements()
        {
            var canvas = _ui.CreateCanvas("Achievements", 106);
            _ach = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 12f);
            _ui.Label(col, "Achievements", 26, UIFactory.Gold, FontStyles.Normal);
            _ui.Label(col, "Earn skins by completing challenges on Hard or Grandmaster.", 12, UIFactory.Muted);
            _achList = _ui.Column(col, 16f);
            _ui.Button(col, "Back", () => ShowSettings(), UIFactory.Gold, UIFactory.Bg);
        }
        private void ShowAchievements() { HideAll(); RebuildAchievements(); _ach.SetActive(true); _gm.SetBanner(true); }
        private void RebuildAchievements()
        {
            for (int i = _achList.childCount - 1; i >= 0; i--) Destroy(_achList.GetChild(i).gameObject);
            string active = SaveData.GetSkin();

            AchRow("Classic Gold", "The piece you start with.", "gold", true, active == "gold");
            foreach (var d in Achievements.All)
                AchRow(d.SkinName + ": " + d.Name, d.Desc, d.SkinId, Achievements.IsUnlocked(d.Id), active == d.SkinId);
        }
        private void AchRow(string title, string desc, string skinId, bool unlocked, bool equipped)
        {
            var row = _ui.Row(_achList, 8f);
            var meta = _ui.Column(row, 2f);
            _ui.Label(meta, title, 14, unlocked ? UIFactory.Text : UIFactory.Muted, FontStyles.Normal, 220);
            _ui.Label(meta, desc, 11, UIFactory.Muted, FontStyles.Normal, 220);
            if (!unlocked) _ui.Label(row, "Locked", 12, UIFactory.Muted, FontStyles.Normal, 80);
            else if (equipped) _ui.Label(row, "Equipped", 12, UIFactory.Hex("#3CDC78"), FontStyles.Normal, 80);
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
            _howtoTitle = _ui.Label(col, "", 26, UIFactory.Gold, FontStyles.Normal);

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
            _howtoPrev = _ui.Button(nav, "<", () => FlipHowTo(-1), UIFactory.Panel, UIFactory.Gold, 58, 46, 20, clickSfx: Sfx.PageTurn);
            _howtoPager = _ui.Label(nav, "", 12, UIFactory.Muted, FontStyles.Normal, 60);
            _howtoNext = _ui.Button(nav, ">", () => FlipHowTo(1), UIFactory.Panel, UIFactory.Gold, 58, 46, 20, clickSfx: Sfx.PageTurn);
            _ui.Button(col, "Got it", () => OnHowToDone(), UIFactory.Gold, UIFactory.Bg);
        }
        private void ShowHowTo(bool returnToPause)
        {
            _howtoReturnPause = returnToPause; _howtoOnDone = null;
            _howtoPages = AllHowTo; _howtoIdx = 0;
            HideAll(); RenderHowTo(); _howto.SetActive(true);
            _gm.SetBanner(true);
        }
        // First-time mode tutorial: walk a subset of pages, then run onDone.
        private void ShowHowToSubset(int[] pages, System.Action onDone)
        {
            _howtoReturnPause = false; _howtoOnDone = onDone;
            _howtoPages = pages; _howtoIdx = 0;
            HideAll(); RenderHowTo(); _howto.SetActive(true);
        }
        private void OnHowToDone()
        {
            var done = _howtoOnDone; _howtoOnDone = null;
            _howto.SetActive(false);
            if (done != null) done();
            else if (_howtoReturnPause) _gm.ShowPauseScreen();
            else ShowSettings();
        }
        private void FlipHowTo(int d)
        {
            _howtoIdx = Mathf.Clamp(_howtoIdx + d, 0, _howtoPages.Length - 1);
            RenderHowTo();
        }
        private void RenderHowTo()
        {
            int page = _howtoPages[_howtoIdx];
            var p = GameContent.HowTo[page];
            _howtoTitle.text = p.title; _howtoBody.text = p.body;
            if (_art != null && _howtoArt != null) _art.Build(page, _howtoArt);
            _howtoPager.text = (_howtoIdx + 1) + " / " + _howtoPages.Length;
            _howtoPrev.interactable = _howtoIdx > 0;
            _howtoNext.interactable = _howtoIdx < _howtoPages.Length - 1;
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
            _trappedCanvasRt = canvas.GetComponent<RectTransform>();
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 15f);
            _trappedPanel = col;

            _trappedVerdict = _ui.Label(col, "", 45, UIFactory.Gold, FontStyles.Normal);
            _trappedSub = _ui.Label(col, "", 20, UIFactory.Muted, FontStyles.Normal, 480);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(col, false);
            var scrollLe = scrollGo.AddComponent<LayoutElement>();
            // A little wider than the grid so move badges on the edge tiles can extend PAST
            // the grid (into the seamless dark margin) instead of being clipped. The mask
            // still clips vertically, so badges vanish when scrolled off the viewport. (Kept
            // modest so it doesn't inflate the panel width and over-shrink the fit.)
            scrollLe.preferredWidth = GameConfig.Cols * CellPx + 2 * CellPx;
            scrollLe.preferredHeight = 640;
            _trappedScroll = scrollGo.AddComponent<ScrollRect>();
            _trappedScroll.horizontal = false; _trappedScroll.vertical = true;
            _trappedScroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            UIFactory.Stretch(vpRt);
            viewport.AddComponent<Image>().color = UIFactory.Bg; // seamless with the screen so the wider mask is invisible
            viewport.AddComponent<RectMask2D>();
            _trappedScroll.viewport = vpRt;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _trappedContent = content.GetComponent<RectTransform>();
            _trappedContent.anchorMin = _trappedContent.anchorMax = new Vector2(0.5f, 1f);
            _trappedContent.pivot = new Vector2(0.5f, 1f);
            _trappedScroll.content = _trappedContent;

            _ui.Label(col, "Green = a path you could have taken (in order). Blue = your last moves.", 17, UIFactory.Muted, FontStyles.Normal, 480);
            _ui.Button(col, "Back", CloseTrapped, UIFactory.Panel, UIFactory.Gold, 300, 66, 21);
        }
        private void ShowTrapped() { _gameOver.SetActive(false); _trapped.SetActive(true); Populate(_analysis); FitTrapped(); }
        private void CloseTrapped() { _trapped.SetActive(false); _gameOver.SetActive(true); }

        // Uniformly scale the whole screen to fit the device: ~10% smaller by default, and
        // more when the panel would otherwise overflow a narrow (sides) or short/tablet
        // (bottom) viewport. The panel is centre-anchored, so scaling keeps it centred.
        private void FitTrapped()
        {
            if (_trappedPanel == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_trappedPanel);
            float pw = _trappedPanel.rect.width, ph = _trappedPanel.rect.height;
            if (pw <= 1f || ph <= 1f) return;
            // Visible area in canvas units (default 533x948 reference, match 0.5).
            float logW = Mathf.Log(Screen.width / 533f, 2f);
            float logH = Mathf.Log(Screen.height / 948f, 2f);
            float sf = Mathf.Pow(2f, Mathf.Lerp(logW, logH, 0.5f));
            if (sf <= 0f) return;
            float cw = Screen.width / sf, ch = Screen.height / sf;
            // 0.9 = the ~10% shrink; the other two terms shrink further to fit within 94% of
            // the visible area (leaves a margin for notches / rounded corners).
            float scale = Mathf.Min(0.9f, (cw * 0.94f) / pw, (ch * 0.94f) / ph);
            _trappedPanel.localScale = new Vector3(scale, scale, 1f);
        }

        private void Populate(DeathAnalysis a)
        {
            for (int i = _trappedContent.childCount - 1; i >= 0; i--) Destroy(_trappedContent.GetChild(i).gameObject);

            if (!a.Escaped)
            {
                _trappedVerdict.text = "Yep"; _trappedVerdict.color = UIFactory.Hex("#FF8A5C");
                _trappedSub.text = "Genuinely trapped. No legal jump out of that square.";
            }
            else if (a.Reason == EscapeReason.DeepBack)
            {
                _trappedVerdict.text = "Nope"; _trappedVerdict.color = UIFactory.Hex("#FFD060");
                _trappedSub.text = "Escapable, but the only line out doubled back through tiles already off-screen.";
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
            // Group the last-10 move numbers by cell, so a tile the knight landed on
            // more than once lists every move number (e.g. "1,3,5") in one badge instead
            // of stacking badges where a later move hides an earlier one.
            var cellMoves = new Dictionary<(int, int), List<int>>();
            foreach (var tr in a.Trail)
            {
                if (!InRange(tr.relRow)) continue;
                var key = (tr.col, tr.relRow);
                if (!cellMoves.TryGetValue(key, out var list)) { list = new List<int>(); cellMoves[key] = list; }
                list.Add(tr.moveNum);
            }
            foreach (var kv in cellMoves)
            {
                kv.Value.Sort();
                Badge(kv.Key.Item1, kv.Key.Item2 - relTop, string.Join(",", kv.Value));
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
            rt.sizeDelta = new Vector2(len, 5f);
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
            t.alignment = TextAlignmentOptions.Center; t.fontStyle = FontStyles.Normal;
        }
        private void Badge(int col, int rowIdx, string text)
        {
            var go = new GameObject("badge", typeof(RectTransform));
            go.transform.SetParent(_trappedContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            go.AddComponent<Image>().color = BadgeBg;

            // Let the layout system size the pill to the text (reliable, unlike reading
            // preferredWidth at construction time): a HorizontalLayoutGroup adds the
            // padding and a ContentSizeFitter shrinks the pill to hug the numbers. The
            // pivot is top-left, so a wider "1,3,5" pill extends right past the tile.
            int px = Mathf.RoundToInt(CellPx * 0.05f), py = Mathf.RoundToInt(CellPx * 0f);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(px, px, py, py);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var lab = new GameObject("n", typeof(RectTransform));
            lab.transform.SetParent(go.transform, false);
            var t = lab.AddComponent<TextMeshProUGUI>();
            if (_ui.Font) t.font = _ui.Font;
            t.text = text; t.color = BadgeText; t.fontSize = CellPx * 0.4f;
            t.alignment = TextAlignmentOptions.Center; t.fontStyle = FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;

            // Pin the pill flush to the tile's top-left corner (pivot is top-left) so it
            // clears the green path number, which sits centred in the tile.
            rt.anchoredPosition = new Vector2(col * CellPx, -rowIdx * CellPx);
        }
    }
}
