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
    public partial class UIManager : MonoBehaviour
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
        private Button _legacySettingsToggle, _profileSaveBtn;
        private TMP_Text _runSummaryLabel, _modeDescription;
        private GameObject _runOptions, _profile;
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
        private Scrollbar _tallScrollbar;
        private bool _settingTallBar;
        private bool _tallScrollUserChanged;
        private GameObject _custom, _skinPopup, _skinPopupEquip, _skinPopupBuy, _skinPopupCatcher;
        private RectTransform _customGrid;
        private TMP_Text _skinPopupName, _skinPopupInfo;
        private SkinInfo _popupSkin;
        private VfxInfo _popupVfx;
        private bool _popupIsVfx;
        private int _customTab; // 0 = Skins, 1 = VFX
        private Button _tabSkins, _tabVfx;
        private Button _tabShopSkins, _tabShopVfx, _tabPowerups;
        private RectTransform _shopSkins, _shopVfx, _shopPowerups;
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
            BuildSettings(); BuildProfile(); BuildBugReport(); BuildAchievements(); BuildHowTo(); BuildToast();
            BuildMarketplace();
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
        private Button _cbToggle;
        private TMP_Text _cbToggleLabel;
        private Button _shakeToggle, _pauseShakeToggle, _effectsToggle, _pauseEffectsToggle;
        private Button _hapticsToggle, _pauseHapticsToggle;
        private GameObject _tutorial, _tutContinueBtn, _tutPick;
        private TMP_Text _tutCaption;
        private TMP_Text _advMovesLabel, _advResultInfo, _advResultTitle;
        private TMP_Text _biomeLabel;
        private int _biomeShown = -1;
        private static readonly string[] BiomeNames = { "THE ROYAL GARDENS", "THE GILDED CROSSING", "THE MOONSTONE KEEP" };
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
        // Safe-area layout: bars + the elements LayoutHud repositions each frame.
        private RectTransform _topBarRt, _bottomBarRt, _scoreRt, _advMovesRt, _restartRt, _pauseRt, _modeBadgeRt;
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
        private RectTransform _menuSettingsRt, _menuShillingsRt, _menuNavRt, _menuContent, _menuColumn, _runOptionsColumn;
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
            UpdateLegacyTransformBadge();
            if (_hud != null && _hud.activeSelf && _scoreText != null && _gm.Score != _lastScoreShown)
            {
                _lastScoreShown = _gm.Score;
                _scoreText.text = _gm.Score.ToString();
            }
            bool relayout = ScreenChanged();
            if (_hud != null && _hud.activeSelf)
            {
                if (relayout) LayoutHud();
                if (!LegacyStyle) UpdateModernHud();
                int biome = (int)_gm.CurrentBiome;
                if (_biomeLabel != null && LegacyStyle)
                {
                    _biomeShown = biome;
                    string caption = BiomeNames[biome];
                    if (_gm.CurrentMode == GameMode.Trap && !_gm.BoardHudAdventure && !_gm.BoardHudTutorial)
                    {
                        float seconds = _gm.SurvivalSeconds;
                        caption = seconds < 60f ? "GARDENS · CROSSING IN " + Mathf.CeilToInt(60f - seconds) + "s" :
                            seconds < 90f ? "CROSSING · KEEP IN " + Mathf.CeilToInt(90f - seconds) + "s" : "THE MOONSTONE KEEP";
                    }
                    if (_biomeLabel.text != caption) _biomeLabel.text = caption;
                }
                UpdatePowerupButtons();
            }
            if (_menu != null && _menu.activeSelf && relayout) LayoutMenu();
        }

        // ── Visibility ───────────────────────────────────────────────────────
        private void HideAll()
        {
            _forceLayout = true;   // whichever screen comes up next gets one layout pass
            _menu.SetActive(false); _hud.SetActive(false); _pause.SetActive(false);
            _gameOver.SetActive(false); _trapped.SetActive(false); _settings.SetActive(false);
            _ach.SetActive(false); _howto.SetActive(false);
            if (_bugReport != null) _bugReport.SetActive(false);
            if (_runOptions != null) _runOptions.SetActive(false);
            if (_profile != null) _profile.SetActive(false);
            if (_market != null) _market.SetActive(false);
            if (_shillingsShop != null) _shillingsShop.SetActive(false);
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
                _modeBadge.text = (_gm.CurrentMode == GameMode.Trap ? "TRAP" : "BATTLE") + " · " + _gm.CurrentDifficulty.ToString();
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
                _goReward.text = runReward > 0 ? "+" + runReward.ToString("N0") + " shillings collected" : "";
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
                if (lbl != null) lbl.text = "Spend " + reviveCost.ToString("N0");
                _reviveSpendBtn.interactable = shillings >= reviveCost;
            }
            // The free ad life is offered once per run, and only when an ad is loaded.
            // Otherwise the row falls back to the shilling revive on its own.
            if (_reviveWatchBtn != null) _reviveWatchBtn.gameObject.SetActive(canRevive && adLife);
            if (_goBalance != null) { _goBalance.color = UIFactory.Muted; _goBalance.text = shillings.ToString("N0") + " shillings"; }
            _gameOver.SetActive(true);
        }

        // ── Menu ─────────────────────────────────────────────────────────────
        private void BuildMenu()
        {
            if (LegacyStyle) { BuildLegacyMenu(); return; }
            var canvas = _ui.CreateCanvas("Menu", 100);
            _menu = canvas.gameObject;
            _menuCanvas = canvas;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            _menuContent = new GameObject("HomeContent", typeof(RectTransform)).GetComponent<RectTransform>();
            _menuContent.SetParent(screen.transform, false);
            UIFactory.Stretch(_menuContent);
            var col = _ui.Column(_menuContent, 12f);
            _menuColumn = col;

            {
                var hero = new GameObject("GraphicKnight", typeof(RectTransform), typeof(LayoutElement));
                hero.transform.SetParent(col, false);
                var plate = new GameObject("Paper", typeof(RectTransform), typeof(Image));
                plate.transform.SetParent(hero.transform, false);
                UIFactory.Stretch(plate.GetComponent<RectTransform>());
                var paper = plate.GetComponent<Image>();
                paper.sprite = UISprites.Circle(); paper.color = UIFactory.Gold;
                paper.raycastTarget = false;
                var inset = new GameObject("CrestInset", typeof(RectTransform), typeof(Image));
                inset.transform.SetParent(hero.transform, false);
                var insetRect = inset.GetComponent<RectTransform>();
                UIFactory.Stretch(insetRect);
                insetRect.offsetMin = Vector2.one * 2f;
                insetRect.offsetMax = Vector2.one * -2f;
                var insetImage = inset.GetComponent<Image>();
                insetImage.sprite = UISprites.Circle(); insetImage.color = UIFactory.Panel;
                insetImage.raycastTarget = false;
                _menuHero = hero.GetComponent<RectTransform>();
                var art = new GameObject("Knight", typeof(RectTransform), typeof(RawImage));
                art.transform.SetParent(hero.transform, false);
                UIFactory.Stretch(art.GetComponent<RectTransform>());
                var portrait = art.GetComponent<RawImage>();
                portrait.raycastTarget = false;
                _menuKnight = art.AddComponent<MenuKnightPreview>();
                _menuKnight.Initialize(_gm, _theme, portrait);
                var size = hero.GetComponent<LayoutElement>();
                size.preferredWidth = 158; size.preferredHeight = 158;
            }
            _ui.Label(col, "KNIGHT'S GAUNTLET", 38, UIFactory.Gold, FontStyles.Normal, 390);
            MenuSpace(col, 20);
            var play = MenuButton(col, "PLAY", OnPlay, 310, 68, 25, primary: true);
            play.name = "Play";

            var change = MenuButton(col, "", ShowRunOptions, 350, 46, 16, textOnly: true);
            change.name = "ChangeRunOptions";
            _runSummaryLabel = change.GetComponentInChildren<TMP_Text>();
            MenuSpace(col, 10);
            MenuButton(col, "Adventure", OnAdventurePressed, 310, 58, 20).name = "Adventure";
            _dailyBtn = MenuButton(col, "Daily Challenge", OnDailyPressed, 310, 48, 16, textOnly: true);
            _dailyBtn.name = "DailyChallenge";

            var settings = MenuButton(screen.transform, "", ShowSettings, 48, 48, 18, textOnly: true);
            settings.name = "Settings";
            AddIconChild(settings.gameObject, RoyalIcons.Get("settings"), UIFactory.Muted, 28);
            _menuSettingsRt = settings.GetComponent<RectTransform>();
            _menuSettingsRt.anchorMin = _menuSettingsRt.anchorMax = _menuSettingsRt.pivot = new Vector2(0, 1);
            _menuSettingsRt.sizeDelta = new Vector2(48, 48);

            var wallet = _ui.Row(screen.transform, 8);
            wallet.name = "Wallet";
            _menuShillingsRt = wallet;
            wallet.anchorMin = wallet.anchorMax = wallet.pivot = new Vector2(1, 1);
            var coin = new GameObject("Coin", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            coin.transform.SetParent(wallet, false);
            coin.GetComponent<Image>().sprite = UISprites.Circle();
            coin.GetComponent<Image>().color = UIFactory.Gold;
            coin.GetComponent<Image>().raycastTarget = false;
            var coinSize = coin.GetComponent<LayoutElement>();
            coinSize.preferredWidth = coinSize.preferredHeight = 18;
            _menuShillings = _ui.Label(wallet, "", 18, UIFactory.Text);
            _menuShillings.textWrappingMode = TextWrappingModes.NoWrap;
            _menuShillings.GetComponent<LayoutElement>().preferredWidth = -1;

            _menuNavRt = _ui.Row(screen.transform, 8);
            _menuNavRt.name = "HomeNavigation";
            _menuNavRt.anchorMin = _menuNavRt.anchorMax = _menuNavRt.pivot = new Vector2(.5f, 0);
            MenuButton(_menuNavRt, "Shop", ShowMarketplace, 104, 52, 16, textOnly: true);
            MenuButton(_menuNavRt, "Customize", ShowCustomization, 136, 52, 16, textOnly: true);
            MenuButton(_menuNavRt, "Scores", () => _gm.ShowLeaderboard(), 104, 52, 16, textOnly: true);

            BuildRunOptions();
            var savedMode = (GameMode)PlayerPrefs.GetInt("kg_menu_mode", (int)GameMode.Trap);
            var savedDiff = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt("kg_menu_difficulty", (int)Difficulty.Medium), 0, 3);
            SetMode(savedMode == GameMode.Battle ? GameMode.Battle : GameMode.Trap);
            SetDiff(savedDiff);
        }

        // Home controls share quieter surfaces; the gold fill belongs to Play.
        private Button MenuButton(Transform parent, string text, System.Action action, float width,
            float height, float fontSize, bool primary = false, bool textOnly = false)
        {
            var button = _ui.Button(parent, text, action,
                primary ? UIFactory.Gold : textOnly ? Color.clear : UIFactory.Panel,
                primary ? UIFactory.Bg : UIFactory.Text, width, height, fontSize,
                clickSfx: primary ? Sfx.PlaySelect : Sfx.TabSelect);
            var outline = button.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            var fillet = button.transform.Find("GoldFillet");
            if (fillet != null) fillet.gameObject.SetActive(false);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.font = LegacyStyle ? UIFactory.PixelFont() : UIFactory.BodyFont();
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSizeMin = fontSize * UIFactory.FontScale * .85f;
            return button;
        }

        private static void MenuSpace(Transform parent, float height)
        {
            var spacer = new GameObject("Space", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private void BuildRunOptions()
        {
            var canvas = _ui.CreateCanvas("RunOptions", 104);
            _runOptions = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 16);
            _runOptionsColumn = col;
            _ui.Label(col, "YOUR NEXT RUN", 30, UIFactory.Gold);
            _ui.Label(col, "Choose a mode", 16, UIFactory.Muted);
            var modeRow = _ui.Row(col, 10);
            _trapBtn = MenuButton(modeRow, "Trap", () => SetMode(GameMode.Trap), 152, 60, 19);
            _battleBtn = MenuButton(modeRow, "Battle", () => SetMode(GameMode.Battle), 152, 60, 19);
            _modeDescription = _ui.Label(col, "", 14, UIFactory.Muted, width: 320);
            MenuSpace(col, 8);
            _speedLabel = _ui.Label(col, "SCROLL SPEED", 14, UIFactory.Muted);
            var row = _ui.Row(col, 8);
            _diffBtns[Difficulty.Easy] = MenuButton(row, "Easy", () => SetDiff(Difficulty.Easy), 152, 52, 16);
            _diffBtns[Difficulty.Medium] = MenuButton(row, "Medium", () => SetDiff(Difficulty.Medium), 152, 52, 16);
            var secondRow = _ui.Row(col, 8);
            _diffBtns[Difficulty.Hard] = MenuButton(secondRow, "Hard", () => SetDiff(Difficulty.Hard), 152, 52, 16);
            _diffBtns[Difficulty.Grandmaster] = MenuButton(secondRow, "Grandmaster", () => SetDiff(Difficulty.Grandmaster), 152, 52, 16);
            MenuSpace(col, 14);
            MenuButton(col, "Done", () => _runOptions.SetActive(false), 310, 56, 18, primary: true).name = "Done";
            _runOptions.SetActive(false);
        }

        private void ShowRunOptions()
        {
            _runOptions.SetActive(true);
            LayoutMenu();
        }

        private static void StyleMenuChoice(Button button, bool selected)
        {
            var border = button.transform.Find("Border");
            if (border != null) border.gameObject.SetActive(selected);
            button.GetComponent<Image>().color = selected ? UIFactory.Hex("#243956") : UIFactory.Hex("#111E32");
            button.GetComponentInChildren<TMP_Text>().color = selected ? UIFactory.Gold : UIFactory.Muted;
        }

        private void RefreshRunSummary()
        {
            if (_runSummaryLabel != null)
                _runSummaryLabel.text = _selMode + " · " + _selDiff + "   <color=#DDBA68>Change ›</color>";
        }

        private void SetMode(GameMode m)
        {
            _selMode = m;
            if (LegacyStyle)
            {
                UIFactory.StyleToggle(_trapBtn, m == GameMode.Trap);
                UIFactory.StyleToggle(_battleBtn, m == GameMode.Battle);
                if (_playButtonLabel != null) _playButtonLabel.text = m == GameMode.Trap ? "Play Trap" : "Play Battle";
            }
            else
            {
                StyleMenuChoice(_trapBtn, m == GameMode.Trap);
                StyleMenuChoice(_battleBtn, m == GameMode.Battle);
            }
            if (_speedLabel != null) _speedLabel.text = m == GameMode.Trap ? "SCROLL SPEED" : "ENEMY DIFFICULTY";
            if (_modeDescription != null) _modeDescription.text = m == GameMode.Trap ? "Keep moving. Avoid the obstacles." : "Capture enemies. Keep your streak alive.";
            PlayerPrefs.SetInt("kg_menu_mode", (int)m);
            PlayerPrefs.Save();
            RefreshRunSummary();
        }

        public void RefreshLegacyViewToggle()
        {
            if (_legacySettingsToggle == null) return;
            _legacySettingsToggle.GetComponentInChildren<TMP_Text>().text = "Legacy Mode: " + (_gm.Legacy2DActive ? "ON" : "OFF");
            UIFactory.StyleToggle(_legacySettingsToggle, _gm.Legacy2DActive);
        }

        private void SetDiff(Difficulty d)
        {
            _selDiff = d;
            foreach (var kv in _diffBtns)
                if (LegacyStyle) UIFactory.StyleToggle(kv.Value, kv.Key == d);
                else StyleMenuChoice(kv.Value, kv.Key == d);
            if (_gmText != null) _gmText.color = d == Difficulty.Grandmaster ? UIFactory.Gold : UIFactory.Muted;
            PlayerPrefs.SetInt("kg_menu_difficulty", (int)d);
            PlayerPrefs.Save();
            RefreshRunSummary();
        }

        private void RefreshNameUI()
        {
            RefreshLegacyNameUI();
            bool locked = SaveData.IsNameLocked();
            _nameSetLabel.gameObject.SetActive(locked);
            _nameInput.gameObject.SetActive(!locked);
            _permWarn.gameObject.SetActive(!locked);
            if (_profileSaveBtn != null) _profileSaveBtn.gameObject.SetActive(!locked);
            if (locked) _nameSetLabel.text = SaveData.GetName();
            // Keep the profile compact when there is no validation message.
            SetMenuWarn("", UIFactory.Muted);
            if (_menuShillings != null) _menuShillings.text = LegacyStyle ? _gm.Shillings + " shillings" : _gm.Shillings.ToString("N0");
            if (_dailyBtn != null)
            {
                var dl = _dailyBtn.GetComponentInChildren<TMP_Text>();
                if (dl != null) dl.text = LegacyStyle ? "Daily Challenge (" + SaveData.DailyAttemptsLeft() + " left)" : "Daily Challenge  <color=#A9B5CC>· " + SaveData.DailyAttemptsLeft() + " left</color>";
            }
        }

        // Hide empty profile validation messages so they take no layout space.
        private void SetMenuWarn(string text, Color color)
        {
            if (_menuWarn == null) return;
            _menuWarn.text = text;
            _menuWarn.color = color;
            _menuWarn.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private void OnPlay()
        {
            if (_menuReveal != null) return;
            if (LegacyStyle) StartWithTutorial(_selMode, _selDiff);
            else _menuReveal = StartCoroutine(RevealFromMenu());
        }

        private void SaveProfileName()
        {
            if (_checkingName) return;
            if (SaveData.IsNameLocked()) return;

            var raw = (_nameInput.text ?? "").Trim();
            if (raw.Length > 7) raw = raw.Substring(0, 7);

            if (string.IsNullOrEmpty(raw)) { SetMenuWarn("Enter a name to save your scores.", UIFactory.Muted); return; }

            // Validate and claim the permanent score name from the profile screen.
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
                SetMenuWarn("Your scores will be saved as " + raw + ".", UIFactory.Muted);
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
            if (LegacyStyle) BuildLegacyHud(); else BuildModernHud();
        }

        // HUD canvas reference resolution (canvas units).
        private const float HudRefW = 640f, HudRefH = 1138f;

        // Top bar = a safe offset (max of the notch and a swipe-guard so interactive
        // elements clear the control-center / notification pull-down zone) + a content row.
        private static float HudTopContentH => UIFactory.LegacyStyle ? 52f : 108f;
        private const float HudTopSwipeGuard = 34f; // min gap from the very top edge
        // Bottom bar ≈ the Android 3-button nav height; never smaller than the gesture inset.
        private const float HudBottomBarH = 76f;

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
        private float BottomBarCanvas()
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

        // Repositions the safe-area-sensitive HUD elements when the layout changes: bar heights,
        // the score / pause / restart row, the powerup buttons, the adventure label, and the
        // transform badge. Called from Update while the HUD is visible.
        private void LayoutHud()
        {
            if (LegacyStyle) LayoutLegacyHud(); else LayoutModernHud();
        }

        // One in-game powerup button: a radial-wipe duration overlay (behind), the shop
        // icon, and a small owned-count badge. Tapping activates it via GameManager.
        // Position/visibility handled by LayoutHud + Update.
        private Button MakePowerupButton(Transform parent, PowerupInfo pu, out RectTransform rt, out TMP_Text count, out Image radial)
        {
            var btn = _ui.Button(parent, "", () => OnActivatePowerup(pu.Id), UIFactory.Panel, UIFactory.Gold, 62, 62, 12);
            rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(62, 62);

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

            if (pu.Icon != null) AddIconChild(btn.gameObject, pu.Icon, UIFactory.Gold, 36);

            // Owned-count badge, bottom-right corner of the button.
            count = _ui.Label(btn.transform, "", 15, UIFactory.Gold, FontStyles.Normal);
            var cRt = count.rectTransform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(1, 0); cRt.pivot = new Vector2(1, 0);
            cRt.sizeDelta = new Vector2(30, 20); cRt.anchoredPosition = new Vector2(-5, 4);
            count.alignment = TextAlignmentOptions.BottomRight; count.raycastTarget = false;
            btn.gameObject.SetActive(false);
            return btn;
        }

        private void OnActivatePowerup(PowerupId id)
        {
            bool ok = id == PowerupId.DivinePath ? _gm.ActivateDivinePath() : _gm.ActivatePowerup(id);
            if (ok) UpdatePowerupButtons();
        }

        // Show/update the queen's health (segments filled = HP left). Filled segments are
        // blue while she's shielded, red once she's vulnerable; empty segments are dark.
        public void SetBossHud(bool show, int health, int max, bool shielded)
        {
            if (_bossHud == null) return;
            if (_bossHud.activeSelf != show) _bossHud.SetActive(show);
            if (!show || _bossSegs == null) return;
            if (_hudBossLabel != null)
                HudValue(_hudBossLabel, "THE QUEEN · " + (shielded ? "SHIELDED" : "VULNERABLE") + "  " + health + "/" + max);
            Color filled = shielded ? UIFactory.Hex("#4FA8FF") : UIFactory.Hex("#FF5A5A");
            Color empty = new Color(0.12f, 0.13f, 0.20f, 1f);
            for (int i = 0; i < _bossSegs.Length; i++)
                if (_bossSegs[i] != null)
                    _bossSegs[i].color = i < health ? filled : empty;
        }

        // Keep the home hierarchy between the safe header and the navigation/ad area.
        private void LayoutMenu()
        {
            if (LegacyStyle) { LayoutLegacyMenu(); return; }
            if (_menuCanvas == null) return;
            float menuScale = _menuCanvas.scaleFactor > 0f ? _menuCanvas.scaleFactor : 1f;
            float top = Mathf.Max(24, (Screen.height - Screen.safeArea.yMax) / menuScale + 12);
            float bottom = Mathf.Max(96, Screen.safeArea.yMin / menuScale + 72);
            float left = Screen.safeArea.xMin / menuScale + 24;
            float right = (Screen.width - Screen.safeArea.xMax) / menuScale + 24;
            _menuSettingsRt.anchoredPosition = new Vector2(left, -top);
            _menuShillingsRt.anchoredPosition = new Vector2(-right, -top - 10);
            _menuNavRt.anchoredPosition = new Vector2((left - right) * .5f, bottom);
            _menuContent.offsetMin = new Vector2(left, bottom + 80);
            _menuContent.offsetMax = new Vector2(-right, -top - 72);
            Canvas.ForceUpdateCanvases();
            FitMenuColumn(_menuColumn, _menuContent.rect.width, _menuContent.rect.height);
            float width = Screen.safeArea.width / menuScale - 32;
            _menuNavRt.localScale = Vector3.one * Mathf.Min(1, width / 360f);
            if (_runOptions.activeSelf)
                FitMenuColumn(_runOptionsColumn, width, Screen.safeArea.height / menuScale - 140);
        }

        private static void FitMenuColumn(RectTransform column, float width, float height)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(column);
            float scale = Mathf.Min(1, Mathf.Max(1, width) / Mathf.Max(1, column.rect.width),
                Mathf.Max(1, height) / Mathf.Max(1, column.rect.height));
            column.localScale = Vector3.one * scale;
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
            if (LegacyStyle) { SetLegacyEnemyBar(show, progress); return; }
            if (_enemyBarRoot == null) return;
            if (_enemyBarRoot.activeSelf != show) _enemyBarRoot.SetActive(show);
            if (!show || _enemyBarFill == null) return;
            _enemyBarFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
            var img = _enemyBarFill.GetComponent<Image>();
            if (img != null) img.color = EnemyBarColor(progress);
        }

        // Green → gold → orange → red as the next enemy move approaches.
        private static readonly Color EnemyGreen = new Color32(0x3C,0xDC,0x78,255);
        private static readonly Color EnemyGold = new Color32(0xF0,0xC0,0x40,255);
        private static readonly Color EnemyOrange = new Color32(0xFF,0x8A,0x3C,255);
        private static readonly Color EnemyRed = new Color32(0xFF,0x40,0x40,255);
        private static Color EnemyBarColor(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.4f) return Color.Lerp(EnemyGreen, EnemyGold, t / 0.4f);
            if (t < 0.75f) return Color.Lerp(EnemyGold, EnemyOrange, (t - 0.4f) / 0.35f);
            return Color.Lerp(EnemyOrange, EnemyRed, (t - 0.75f) / 0.25f);
        }

        // ── Pause ────────────────────────────────────────────────────────────
        private void BuildPause()
        {
            if (!LegacyStyle) { BuildPolishedPause(); return; }
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
            _pauseHapticsToggle = _ui.Button(col, "Capture Vibration", () => ToggleCaptureHaptics(), UIFactory.Panel, UIFactory.Gold);
            _pauseEffectsToggle = _ui.Button(col, "Reduce Effects", () => ToggleReducedEffects(), UIFactory.Panel, UIFactory.Gold);
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
            if (!LegacyStyle) { BuildPolishedSettings(); return; }
            var canvas = _ui.CreateCanvas("Settings", 105);
            _settings = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 14f);
            _ui.Label(col, "Settings", 28, UIFactory.Gold, FontStyles.Normal);
            _legacySettingsToggle = _ui.Button(col, "", () => _gm.ToggleLegacy2D(), UIFactory.Panel, UIFactory.Gold, 300, 48, 18);
            _legacySettingsToggle.name = "LegacyModeToggle";
            RefreshLegacyViewToggle();
            _ui.Button(col, "Player Profile", ShowProfile, UIFactory.Panel, UIFactory.Gold);
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
            _hapticsToggle = _ui.Button(col, "Capture Vibration", () => ToggleCaptureHaptics(), UIFactory.Panel, UIFactory.Gold);
            _effectsToggle = _ui.Button(col, "Reduce Effects", () => ToggleReducedEffects(), UIFactory.Panel, UIFactory.Gold);
            UpdateScreenShakeToggles();
            _ui.Button(col, "Tutorial", () => ShowTutorialPick(), UIFactory.Panel, UIFactory.Gold);
            // Debug builds only: open the LevelPlay Test Suite to verify ad networks and
            // read this device's advertising ID. Stripped from release builds.
            if (Debug.isDebugBuild)
                _ui.Button(col, "Ad Test Suite", () => _gm.LaunchAdTestSuite(), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Back", () => ShowMenu(), UIFactory.Gold, UIFactory.Bg, 220, 50, 22);
        }

        private void BuildProfile()
        {
            var canvas = _ui.CreateCanvas("PlayerProfile", 106);
            _profile = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 18);
            _ui.Label(col, "PLAYER PROFILE", 30, UIFactory.Gold);
            _nameSetLabel = _ui.Label(col, "", 24, UIFactory.Text);
            _nameInput = _ui.Input(col, "NAME (OPTIONAL)", 7);
            _permWarn = _ui.Label(col, "Choose a name to save your scores.\nOnce saved, your name is permanent.", 14, UIFactory.Muted);
            _menuWarn = _ui.Label(col, "", 14, UIFactory.Amber);
            _profileSaveBtn = MenuButton(col, "Save Name", SaveProfileName, 280, 54, 18, primary: true);
            MenuButton(col, "Back", ShowSettings, 280, 50, 17, textOnly: true);
            _profile.SetActive(false);
        }

        private void ShowProfile()
        {
            HideAll();
            RefreshNameUI();
            _profile.SetActive(true);
            _gm.SetBanner(true);
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
            PreferenceState(_cbToggle, "Colorblind mode", SaveData.IsColorblind());
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
            PreferenceState(_shakeToggle, "Screen shake", on);
            PreferenceState(_pauseShakeToggle, "Screen shake", on);
            bool reduced = SaveData.IsReducedEffects();
            PreferenceState(_effectsToggle, "Reduce effects", reduced);
            PreferenceState(_pauseEffectsToggle, "Reduce effects", reduced);
            bool haptics = SaveData.IsCaptureHaptics();
            PreferenceState(_hapticsToggle, "Capture vibration", haptics);
            PreferenceState(_pauseHapticsToggle, "Capture vibration", haptics);
        }
        private void ToggleCaptureHaptics()
        {
            SaveData.SetCaptureHaptics(!SaveData.IsCaptureHaptics());
            UpdateScreenShakeToggles();
        }
        private void ToggleReducedEffects()
        {
            SaveData.SetReducedEffects(!SaveData.IsReducedEffects());
            UpdateScreenShakeToggles();
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
            if (LegacyStyle) BuildLegacyTutorial(); else BuildModernTutorial();
        }

        private void BuildLegacyTutorial()
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
                        ? levelName + "\n<size=70%><color=#DDBA68>" + moves + " MOVES   /   " + enemiesLeft + " ENEMIES</color></size>"
                        : levelName + "\n<size=70%><color=#DDBA68>" + moves + " MOVES</color></size>";
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
                _advResultShillings.text = balanceBefore.ToString("N0") + " shillings   <color=#57BD79>+" + reward.ToString("N0") + "</color>";

                float dur = 0.7f, t = 0f;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    int val = Mathf.RoundToInt(Mathf.Lerp(balanceBefore, target, Mathf.Clamp01(t / dur)));
                    _advResultShillings.text = val.ToString("N0") + " shillings   <color=#57BD79>+" + reward.ToString("N0") + "</color>";
                    yield return null;
                }
                _advResultShillings.text = target.ToString("N0") + " shillings";
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

        // ── Direct-purchase marketplace ──────────────────────────────────────
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
            _tabShopSkins = _ui.Button(tabs, "Skins", () => SetShopTab(0), UIFactory.Panel, UIFactory.Muted, 96, 42, 16);
            _tabShopVfx = _ui.Button(tabs, "VFX", () => SetShopTab(1), UIFactory.Panel, UIFactory.Muted, 76, 42, 16);
            _tabPowerups = _ui.Button(tabs, "Powerups", () => SetShopTab(2), UIFactory.Panel, UIFactory.Muted, 110, 42, 16);

            _shopSkins = _ui.Column(col, 9f);
            foreach (var skin in Skins.Shop) BuildShopCosmeticRow(_shopSkins, skin.Name, skin.Color, skin.Price,
                Skins.IsOwned(skin.Id), SaveData.GetSkin() == skin.Id, () => BuySkin(skin));

            _shopVfx = _ui.Column(col, 9f);
            foreach (var vfx in Vfx.All) BuildShopCosmeticRow(_shopVfx, vfx.Name, UIFactory.Hex("#B88CFF"), vfx.Price,
                Vfx.IsOwned(vfx.Id), SaveData.GetVfx() == vfx.Id, () => BuyVfx(vfx));

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

        private void BuildShopCosmeticRow(Transform parent, string name, Color swatchColor, int price, bool owned, bool equipped, System.Action buy)
        {
            var row = _ui.Row(parent, 8f);
            var swatch = new GameObject("CosmeticSwatch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            swatch.transform.SetParent(row, false);
            var le = swatch.GetComponent<LayoutElement>(); le.preferredWidth = le.minWidth = 48f; le.preferredHeight = le.minHeight = 48f;
            var img = swatch.GetComponent<Image>(); img.sprite = _ui.Circle(); img.color = UIFactory.Panel; img.raycastTarget = false;
            var piece = _ui.Label(swatch.transform, "\u265E", 34, swatchColor, FontStyles.Normal, 48);
            piece.font = _theme.glyphFont != null ? _theme.glyphFont : UIFactory.ChessFont();
            piece.alignment = TextAlignmentOptions.Center;
            UIFactory.Stretch(piece.rectTransform);
            piece.GetComponent<LayoutElement>().ignoreLayout = true;
            _ui.Label(row, name, 15, UIFactory.Text, FontStyles.Normal, 180);
            var status = owned ? (equipped ? "Equipped" : "Owned") : price + " sh";
            var button = _ui.Button(row, status, owned ? (System.Action)null : buy,
                owned ? UIFactory.Panel : UIFactory.Gold, owned ? UIFactory.Muted : UIFactory.Bg, 90, 44, 14);
            if (owned) button.interactable = false;
        }

        private void BuySkin(SkinInfo skin)
        {
            if (_gm.BuyShopSkin(skin))
            {
                _marketMsg.color = UIFactory.Gold; _marketMsg.text = "Purchased " + skin.Name + "!";
            }
            else { _marketMsg.color = UIFactory.Error; _marketMsg.text = "Not enough shillings."; }
            RebuildMarket();
        }

        private void BuyVfx(VfxInfo vfx)
        {
            if (vfx == null || Vfx.IsOwned(vfx.Id)) return;
            if (!SaveData.TrySpendShillings(vfx.Price))
            {
                _marketMsg.color = UIFactory.Error; _marketMsg.text = "Not enough shillings.";
                return;
            }
            SaveData.AddOwnedVfx(vfx.Id);
            _gm.EquipVfx(vfx.Id);
            _marketMsg.color = UIFactory.Gold; _marketMsg.text = "Purchased " + vfx.Name + "!";
            RebuildMarket();
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
            if (pu.Icon != null) { img.sprite = pu.Icon; img.preserveAspect = true; img.color = UIFactory.Gold; }
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
            if (_shopSkins != null) _shopSkins.gameObject.SetActive(tab == 0);
            if (_shopVfx != null) _shopVfx.gameObject.SetActive(tab == 1);
            if (_shopPowerups != null) _shopPowerups.gameObject.SetActive(tab == 2);
            if (_tabShopSkins != null) UIFactory.StyleToggle(_tabShopSkins, tab == 0);
            if (_tabShopVfx != null) UIFactory.StyleToggle(_tabShopVfx, tab == 1);
            if (_tabPowerups != null) UIFactory.StyleToggle(_tabPowerups, tab == 2);
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
            if (_shillingsShopBalance != null) _shillingsShopBalance.text = _gm.Shillings.ToString("N0") + " shillings";
            ShowToast("Purchased " + amount.ToString("N0") + " shillings");
        }

        private void OnShillingsPurchaseFailed(string productId, bool cancelled)
        {
            if (!cancelled) ShowToast("Purchase failed. You were not charged.");
        }

        private void ShowShillingsShop()
        {
            HideAll();
            if (_shillingsShopBalance != null) _shillingsShopBalance.text = _gm.Shillings.ToString("N0") + " shillings";
            _shillingsShop.SetActive(true);
            _gm.SetBanner(true);
        }
        private void ShowMarketplace()
        {
            HideAll();
            RebuildMarket();
            _marketMsg.text = "";
            _market.SetActive(true);
            _gm.SetBanner(true);
        }

        private void RebuildMarket()
        {
            if (_marketBalance != null) _marketBalance.text = _gm.Shillings.ToString("N0") + " shillings";
            if (_shopSkins != null)
            {
                for (int i = _shopSkins.childCount - 1; i >= 0; i--) Destroy(_shopSkins.GetChild(i).gameObject);
                foreach (var skin in Skins.Shop)
                {
                    var item = skin;
                    BuildShopCosmeticRow(_shopSkins, item.Name, item.Color, item.Price,
                        Skins.IsOwned(item.Id), SaveData.GetSkin() == item.Id, () => BuySkin(item));
                }
            }
            if (_shopVfx != null)
            {
                for (int i = _shopVfx.childCount - 1; i >= 0; i--) Destroy(_shopVfx.GetChild(i).gameObject);
                foreach (var vfx in Vfx.All)
                {
                    var item = vfx;
                    BuildShopCosmeticRow(_shopVfx, item.Name, UIFactory.Hex("#B88CFF"), item.Price,
                        Vfx.IsOwned(item.Id), SaveData.GetVfx() == item.Id, () => BuyVfx(item));
                }
            }
            for (int i = 0; i < Powerups.All.Length && i < _powerupOwned.Length; i++)
                if (_powerupOwned[i] != null) _powerupOwned[i].text = "Owned: " + Powerups.All[i].Owned;
        }
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
            pRt.sizeDelta = new Vector2(230, 142); pRt.pivot = new Vector2(0.5f, 0f);
            var pImg = _skinPopup.GetComponent<Image>();
            pImg.sprite = _ui.Fill(); pImg.type = Image.Type.Sliced; pImg.color = new Color(0.10f, 0.11f, 0.18f, 0.98f);
            var pcol = _ui.Column(_skinPopup.transform, 5f);
            _skinPopupName = _ui.Label(pcol, "", 15, UIFactory.Gold, FontStyles.Normal, 210);
            _skinPopupInfo = _ui.Label(pcol, "", 11, UIFactory.Muted, FontStyles.Normal, 210);
            _skinPopupBuy = _ui.Button(pcol, "Buy", () => OnBuyPopup(), UIFactory.Gold, UIFactory.Bg, 120, 34, 13).gameObject;
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
            _skinPopupInfo.text = owned ? (equipped ? "Equipped" : "Owned") : v.Price + " shillings";
            _skinPopupBuy.SetActive(!owned && v.Id != Vfx.NoneId);
            if (!owned && v.Id != Vfx.NoneId) _skinPopupBuy.GetComponentInChildren<TMP_Text>().text = "Buy " + v.Price + " sh";
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
            bool shopSkin = s.Source == SkinSource.Shop;
            _skinPopupBuy.SetActive(shopSkin && !owned);
            if (shopSkin && !owned) _skinPopupBuy.GetComponentInChildren<TMP_Text>().text = "Buy " + s.Price + " sh";
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
        private void OnBuyPopup()
        {
            bool bought = false;
            string name = "item";
            if (_popupIsVfx && _popupVfx != null)
            {
                name = _popupVfx.Name;
                if (SaveData.TrySpendShillings(_popupVfx.Price))
                {
                    SaveData.AddOwnedVfx(_popupVfx.Id);
                    _gm.EquipVfx(_popupVfx.Id);
                    bought = true;
                }
            }
            else if (_popupSkin != null)
            {
                name = _popupSkin.Name;
                bought = _gm.BuyShopSkin(_popupSkin);
            }
            if (!bought)
            {
                _skinPopupInfo.text = "Not enough shillings.";
                return;
            }
            ShowToast("Purchased " + name + "!");
            RebuildCustomization();
        }
        private static string SkinRequirement(SkinInfo s)
        {
            if (s.Source == SkinSource.Default) return "Default skin.";
            if (s.Source == SkinSource.Shop) return s.Price + " shillings";
            // Keep the total number of levels a mystery (like the world select does).
            if (s.Source == SkinSource.Mastery) return "3-star every Adventure level to unlock.";
            foreach (var a in Achievements.All) if (a.SkinId == s.Id) return a.Desc;
            return "";
        }
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
