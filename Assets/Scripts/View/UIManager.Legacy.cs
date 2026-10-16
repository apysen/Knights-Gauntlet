using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    public partial class UIManager
    {
        // Legacy menu and HUD layouts restored from the last pre-3D revision.
        private RectTransform _menuShopRt, _menuCustomRt;
        private TMP_Text _playButtonLabel, _gmText, _legacyNameSetLabel, _legacyPermWarn, _legacyMenuWarn;
        private TMP_InputField _legacyNameInput;
        private GameObject _xformBadge;
        private RectTransform _xformBadgeRt;
        private Image _xformBadgeRing;
        private TMP_Text _xformBadgeGlyph;
        private PieceType _xformShown = (PieceType)(-1);

        private void UpdateLegacyTransformBadge()
        {
            if (_xformBadge == null) return;
            bool show = LegacyStyle && _hud != null && _hud.activeSelf && _gm.TransformActive;
            _xformBadge.SetActive(show);
            if (show && _gm.PlayerForm != _xformShown)
            {
                _xformShown = _gm.PlayerForm;
                Color color = UIFactory.Hex(GameConfig.FormColorHex(_xformShown));
                _xformBadgeGlyph.text = GameConfig.Glyph(_xformShown);
                _xformBadgeGlyph.color = color;
                _xformBadgeRing.color = color;
            }
        }

        private bool LegacyStyle => _theme != null && !_theme.comicStyle;
        private void BuildLegacyMenu()
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
            BuildLegacyGrandmasterLabel(_diffBtns[Difficulty.Grandmaster]);

            _legacyNameSetLabel = _ui.Label(col, "", 16, UIFactory.Muted);
            _legacyNameInput = _ui.Input(col, "NAME (OPTIONAL)", 7);
            _legacyPermWarn = _ui.Label(col, "If you would like to save your scores, please enter a name. (It's permanent once set.)", 13, UIFactory.Amber);
            _legacyMenuWarn = _ui.Label(col, "", 14, UIFactory.Amber);

            _ui.Button(col, "PLAY", OnLegacyPlay, UIFactory.Gold, UIFactory.Bg, 230, 56, 23, clickSfx: Sfx.PlaySelect);
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
            _menuShopRt.anchoredPosition = new Vector2(-14f, -14f); // repositioned by LayoutLegacyMenu

            // Customization entry, top-left corner (mirrors the Shop anchoring).
            var custom = _ui.Button(screen.transform, "Customize", () => ShowCustomization(), UIFactory.Panel, UIFactory.Gold, 108, 32, 15);
            _menuCustomRt = custom.GetComponent<RectTransform>();
            _menuCustomRt.anchorMin = _menuCustomRt.anchorMax = new Vector2(0f, 1f);
            _menuCustomRt.pivot = new Vector2(0f, 1f);
            _menuCustomRt.sizeDelta = new Vector2(108f, 32f);
            _menuCustomRt.anchoredPosition = new Vector2(14f, -14f); // repositioned by LayoutLegacyMenu

            // Shillings count, top-right, just to the left of the Shop button.
            _menuShillings = _ui.Label(screen.transform, "", 20, UIFactory.Gold, FontStyles.Normal);
            _menuShillings.alignment = TextAlignmentOptions.Right;
            _menuShillings.textWrappingMode = TextWrappingModes.NoWrap;
            _menuShillingsRt = _menuShillings.rectTransform;
            _menuShillingsRt.anchorMin = _menuShillingsRt.anchorMax = new Vector2(1f, 1f);
            _menuShillingsRt.pivot = new Vector2(1f, 1f);
            _menuShillingsRt.sizeDelta = new Vector2(200f, 32f);
            _menuShillingsRt.anchoredPosition = new Vector2(-82f, -14f); // repositioned by LayoutLegacyMenu

            SetMode(_selMode); SetDiff(_selDiff);
        }

        private void BuildLegacyHud()
        {
            var canvas = _ui.CreateCanvas("HUD", 90, new Vector2(HudRefW, HudRefH));
            _hudCanvas = canvas;
            _hud = canvas.gameObject;

            // Solid bars top and bottom. Created FIRST so they sit behind the HUD content.
            // The board is letterboxed between them (see HudTopInsetFrac/HudBottomInsetFrac),
            // and their heights + the content positions are set every frame by LayoutLegacyHud so
            // they follow the device safe area (notch / control-center swipe / gesture bar).
            _topBarRt = MakeLegacyHudBar(canvas.transform, top: true);
            _bottomBarRt = MakeLegacyHudBar(canvas.transform, top: false);

            _scoreText = _ui.Label(canvas.transform, "0", 36, UIFactory.Gold, FontStyles.Normal, 200);
            _scoreRt = _scoreText.rectTransform;
            _scoreRt.anchorMin = _scoreRt.anchorMax = new Vector2(0, 1); _scoreRt.pivot = new Vector2(0, 1);
            _scoreRt.anchoredPosition = new Vector2(24, -20); // repositioned by LayoutLegacyHud
            _scoreText.alignment = TextAlignmentOptions.TopLeft;

            // Adventure move counter + level name (top-center); hidden outside adventure.
            _advMovesLabel = _ui.Label(canvas.transform, "", 20, UIFactory.Gold, FontStyles.Normal);
            _advMovesLabel.alignment = TextAlignmentOptions.Top; _advMovesLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _advMovesRt = _advMovesLabel.rectTransform;
            _advMovesRt.anchorMin = _advMovesRt.anchorMax = new Vector2(0.5f, 1f); _advMovesRt.pivot = new Vector2(0.5f, 1f);
            _advMovesRt.sizeDelta = new Vector2(360, 60); _advMovesRt.anchoredPosition = new Vector2(0, -18); // repositioned by LayoutLegacyHud
            _advMovesLabel.gameObject.SetActive(false);

            BuildLegacyBossHud(canvas.transform);

            // Mode/difficulty badge (top-centre), pixel-art pill matching the buttons/bars.
            // Positioned in line with the score row by LayoutLegacyHud; hidden in adventure/daily.
            var badgeGo = new GameObject("ModeBadge", typeof(RectTransform), typeof(Image),
                                         typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            badgeGo.transform.SetParent(canvas.transform, false);
            _modeBadgeRt = badgeGo.GetComponent<RectTransform>();
            _modeBadgeRt.anchorMin = _modeBadgeRt.anchorMax = new Vector2(0.5f, 1f); _modeBadgeRt.pivot = new Vector2(0.5f, 1f);
            _modeBadgeRt.anchoredPosition = new Vector2(0, -18); // repositioned by LayoutLegacyHud
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
            _xformBadgeRt.anchoredPosition = new Vector2(20, -70); // repositioned by LayoutLegacyHud (kept 50 below the score)
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
            AddIconChild(restart.gameObject, Resources.Load<Sprite>("LegacyUI/Retry") ?? UISprites.Refresh(), UIFactory.Gold, 24); // theme gold
            _restartRt = restart.GetComponent<RectTransform>();
            _restartRt.anchorMin = _restartRt.anchorMax = new Vector2(1, 1); _restartRt.pivot = new Vector2(1, 1);
            _restartRt.sizeDelta = new Vector2(40, 40);
            _restartRt.anchoredPosition = new Vector2(-64, -16); // repositioned by LayoutLegacyHud

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
            _pauseRt.anchoredPosition = new Vector2(-16, -16); // repositioned by LayoutLegacyHud

            // Powerup activation buttons, directly under the pause/restart pair (Battle/Trap
            // only; shown/updated by LayoutLegacyHud). Same icons as the shop's Powerups tab.
            _divineBtn = MakeLegacyPowerupButton(canvas.transform, Powerups.DivineIntervention, out _divineBtnRt, out _divineCount, out _divineRadial);
            _chronoBtn = MakeLegacyPowerupButton(canvas.transform, Powerups.Chronokinesis, out _chronoBtnRt, out _chronoCount, out _chronoRadial);
            // Adventure-only Divine Path button (takes the same slot as chrono; only one HUD is up at a time).
            _pathBtn = MakeLegacyPowerupButton(canvas.transform, Powerups.DivinePath, out _pathBtnRt, out _pathCount, out _pathRadial);

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

        private void LayoutLegacyHud()
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

        private void LayoutLegacyMenu()
        {
            if (_menuCanvas == null) return;
            float menuScale = _menuCanvas.scaleFactor > 0f ? _menuCanvas.scaleFactor : 1f;
            float y = -((TopOffsetCanvas() + 6f) * HudScale()) / menuScale;
            if (_menuShopRt != null) _menuShopRt.anchoredPosition = new Vector2(-14f, y);
            if (_menuCustomRt != null) _menuCustomRt.anchoredPosition = new Vector2(14f, y);
            if (_menuShillingsRt != null) _menuShillingsRt.anchoredPosition = new Vector2(-82f, y);
        }

        public void SetLegacyEnemyBar(bool show, float progress)
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

        private RectTransform MakeLegacyHudBar(Transform parent, bool top)
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

        private Button MakeLegacyPowerupButton(Transform parent, PowerupInfo pu, out RectTransform rt, out TMP_Text count, out Image radial)
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

        private void BuildLegacyBossHud(Transform parent)
        {
            _bossHud = new GameObject("BossHud", typeof(RectTransform));
            _bossHud.transform.SetParent(parent, false);
            _bossHudRt = _bossHud.GetComponent<RectTransform>();
            _bossHudRt.anchorMin = _bossHudRt.anchorMax = new Vector2(0.5f, 1f); _bossHudRt.pivot = new Vector2(0.5f, 1f);
            _bossHudRt.sizeDelta = new Vector2(240, 40);
            _bossHudRt.anchoredPosition = new Vector2(0, -96); // repositioned by LayoutLegacyHud

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

        private void BuildLegacyGrandmasterLabel(Button btn)
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

        private void OnLegacyPlay()
        {
            if (_checkingName) return;
            // Name already chosen and locked, so straight to play.
            if (SaveData.IsNameLocked()) { StartWithTutorial(_selMode, _selDiff); return; }

            var raw = (_legacyNameInput.text ?? "").Trim();
            if (raw.Length > 7) raw = raw.Substring(0, 7);

            // The name is optional. Empty → just play (scores won't be saved).
            if (string.IsNullOrEmpty(raw)) { StartWithTutorial(_selMode, _selDiff); return; }

            // A name was entered, so validate and claim it (permanent), then play.
            if (!NameFilter.IsAllowed(raw)) { SetLegacyMenuWarn("Please choose a different name.", UIFactory.Error); return; }

            _checkingName = true;
            SetLegacyMenuWarn("Checking name\u2026", UIFactory.Muted);
            _gm.CheckNameAvailable(raw, available =>
            {
                _checkingName = false;
                if (!available)
                {
                    SetLegacyMenuWarn("That name's taken. Pick another.", UIFactory.Error);
                    return;
                }
                SaveData.SetName(raw);   // permanent from here on
                RefreshNameUI();
                StartWithTutorial(_selMode, _selDiff);
            });
        }
        private void SetLegacyMenuWarn(string value, Color color)
        {
            if (_legacyMenuWarn == null) return;
            _legacyMenuWarn.text = value;
            _legacyMenuWarn.color = color;
            _legacyMenuWarn.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        private void RefreshLegacyNameUI()
        {
            if (_legacyNameInput == null) return;
            bool locked = SaveData.IsNameLocked();
            _legacyNameSetLabel.gameObject.SetActive(locked);
            _legacyNameInput.gameObject.SetActive(!locked);
            _legacyPermWarn.gameObject.SetActive(!locked);
            if (locked) _legacyNameSetLabel.text = "Playing as " + SaveData.GetName();
            SetLegacyMenuWarn("", UIFactory.Muted);
        }

        public void RebuildPresentation()
        {
            StopAllCoroutines();
            foreach (var canvas in _ui.Canvases)
            {
                if (canvas == null) continue;
                canvas.gameObject.SetActive(false);
                Destroy(canvas.gameObject);
            }
            _diffBtns.Clear();
            _lastScoreShown = int.MinValue;
            _toastCo = null;
            _advResultCo = null;
            _legacyNameInput = null;
            _playButtonLabel = _gmText = null;
            _biomeLabel = null;
            Init(_gm, _theme);
            ShowSettings();
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
    }
}
