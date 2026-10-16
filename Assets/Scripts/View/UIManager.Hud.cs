using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    public partial class UIManager
    {
        private RectTransform _hudHeader, _hudFooter, _hudStatus, _hudStatusRight, _hudTrack;
        private RectTransform _tutorialCard, _tutorialActions;
        private TMP_Text _hudContext, _hudStatusTitle, _hudStatusValue, _hudStatusHeading, _hudStatusDetail, _hudBossLabel;
        private Image _hudProgress;
        private readonly Image[] _hudStars = new Image[3];
        private float _hudContentWidth;
        private bool _hudWasTutorial;
        private bool _hudBattleLayout;
        private bool _battleMultiplierAlignmentDirty = true;
        private RectTransform _battleForm;
        private TMP_Text _battleFormGlyph;
        private Image _battleFormRadial;
        private readonly Image[] _battleCapturePips = new Image[GameConfig.TransformTrigger];

        // All modes share these two containers. Nothing is positioned from the
        // camera or from an enemy's world position, so the grid is stable.
        private void BuildModernHud()
        {
            _hudCanvas = _ui.CreateCanvas("HUD", 90, new Vector2(HudRefW, HudRefH));
            _hud = _hudCanvas.gameObject;
            _topBarRt = HudSurface(_hud.transform, "HeaderBackground", UIFactory.Bg);
            _bottomBarRt = HudSurface(_hud.transform, "FooterBackground", UIFactory.Bg);
            _topBarRt.anchorMin = new Vector2(0, 1); _topBarRt.anchorMax = Vector2.one; _topBarRt.pivot = new Vector2(.5f, 1);
            _bottomBarRt.anchorMin = Vector2.zero; _bottomBarRt.anchorMax = new Vector2(1, 0); _bottomBarRt.pivot = new Vector2(.5f, 0);
            _hudHeader = HudRect(_hud.transform, "HeaderContent", 0, 0, 540, 164);
            _hudHeader.anchorMin = _hudHeader.anchorMax = _hudHeader.pivot = new Vector2(.5f, 1);
            _hudFooter = HudRect(_hud.transform, "FooterContent", 0, 0, 540, 64);
            _hudFooter.anchorMin = _hudFooter.anchorMax = _hudFooter.pivot = new Vector2(.5f, 0);

            _modeBadge = HudText(_hudHeader, "Mode", "", 13, UIFactory.Muted, 0, 0, 360, 22);
            _modeBadgeRt = _modeBadge.rectTransform;
            _hudContext = HudText(_hudHeader, "Context", "", 13, UIFactory.Muted, 0, 0, 360, 22);
            _scoreText = HudText(_hudHeader, "Score", "0", 32, UIFactory.Text, 0, 24, 360, 44);
            _scoreRt = _scoreText.rectTransform;
            _advMovesLabel = HudText(_hudHeader, "LevelAndMoves", "", 22, UIFactory.Text, 0, 22, 360, 50);
            _advMovesRt = _advMovesLabel.rectTransform;
            _advMovesLabel.gameObject.SetActive(false);
            _restartRt = HudIconButton(_hudHeader, "Restart", "restart", () => _gm.RestartGame());
            _pauseRt = HudIconButton(_hudHeader, "Pause", "pause", () => _gm.PauseGame());
            var rule = HudSurface(_hudHeader, "HeaderDivider", UIFactory.Hex("#26354A"));
            HudPlace(rule, 0, 80, 540, 1);
            rule.anchorMax = new Vector2(1, 1); rule.sizeDelta = new Vector2(0, 1);
            _hudStatus = HudRect(_hudHeader, "ModeStatus", 0, 96, 540, 64);
            _hudStatus.anchorMax = new Vector2(1, 1); _hudStatus.sizeDelta = new Vector2(0, 64);
            _hudStatusTitle = HudText(_hudStatus, "MetricCaption", "", 11, UIFactory.Muted, 0, 0, 112, 18);
            _hudStatusValue = HudText(_hudStatus, "Metric", "", 27, UIFactory.Gold, 0, 21, 112, 36);
            for (int i = 0; i < 3; i++)
            {
                var star = HudSurface(_hudStatus, "Star" + i, UIFactory.Gold);
                star.GetComponent<Image>().sprite = UISprites.Star();
                HudPlace(star, i * 32, 25, 26, 26);
                _hudStars[i] = star.GetComponent<Image>();
            }
            _hudStatusRight = HudRect(_hudStatus, "StatusDetail", 132, 0, 360, 64);
            _hudStatusRight.anchorMax = new Vector2(1, 1); _hudStatusRight.sizeDelta = new Vector2(-132, 64);
            _hudStatusHeading = HudText(_hudStatusRight, "StatusHeading", "", 15, UIFactory.Text, 0, 0, 360, 22);
            _hudStatusDetail = HudText(_hudStatusRight, "StatusCaption", "", 13, UIFactory.Muted, 0, 24, 360, 20);
            StretchHudText(_hudStatusHeading); StretchHudText(_hudStatusDetail);
            _hudTrack = HudSurface(_hudStatusRight, "StatusTrack", UIFactory.Hex("#26354A"));
            HudPlace(_hudTrack, 0, 54, 360, 4); _hudTrack.anchorMax = new Vector2(1, 1); _hudTrack.sizeDelta = new Vector2(0, 4);
            _hudProgress = HudSurface(_hudTrack, "StatusProgress", UIFactory.Gold).GetComponent<Image>();
            UIFactory.Stretch(_hudProgress.rectTransform); _hudProgress.rectTransform.anchorMax = new Vector2(0, 1);
            BuildModernBoss(_hudStatusRight);
            BuildBattleFormIndicator();

            _biomeLabel = HudText(_hudFooter, "FooterCaption", "", 13, UIFactory.Muted, 0, 4, 300, 56);
            _biomeLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _biomeLabel.textWrappingMode = TextWrappingModes.Normal;
            _divineBtn = MakePowerupButton(_hudFooter, Powerups.DivineIntervention, out _divineBtnRt, out _divineCount, out _divineRadial);
            _chronoBtn = MakePowerupButton(_hudFooter, Powerups.Chronokinesis, out _chronoBtnRt, out _chronoCount, out _chronoRadial);
            _pathBtn = MakePowerupButton(_hudFooter, Powerups.DivinePath, out _pathBtnRt, out _pathCount, out _pathRadial);
            CompactPowerup(_divineBtn, _divineCount); CompactPowerup(_chronoBtn, _chronoCount); CompactPowerup(_pathBtn, _pathCount);
            _enemyBarRt = HudRect(_hudFooter, "EnemyTimer", 0, 6, 300, 52);
            _enemyBarRoot = _enemyBarRt.gameObject;
            var timerLabel = HudText(_enemyBarRt, "TimerCaption", "NEXT ENEMY MOVE", 12, UIFactory.Muted, 0, 0, 300, 22);
            StretchHudText(timerLabel);
            var timerTrack = HudSurface(_enemyBarRt, "TimerTrack", UIFactory.Hex("#26354A"));
            HudPlace(timerTrack, 0, 34, 300, 6); timerTrack.anchorMax = new Vector2(1, 1); timerTrack.sizeDelta = new Vector2(0, 6);
            _enemyBarFill = HudSurface(timerTrack, "TimerFill", UIFactory.Gold);
            UIFactory.Stretch(_enemyBarFill); _enemyBarFill.anchorMax = new Vector2(0, 1);
            _enemyBarRoot.SetActive(false);
            BuildModernTallScroll();
            LayoutModernHud();
        }

        private RectTransform HudRect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false); HudPlace(rt, x, y, width, height); return rt;
        }
        private static void HudPlace(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height);
        }
        private RectTransform HudSurface(Transform parent, string name, Color color)
        {
            var rt = HudRect(parent, name, 0, 0, 100, 100);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = BoardView.UnitSquare(); image.color = color; image.raycastTarget = false;
            return rt;
        }
        private TMP_Text HudText(Transform parent, string name, string value, float size, Color color,
            float x, float y, float width, float height)
        {
            var text = _ui.Label(parent, value, size, color, width: width);
            text.name = name; text.font = UIFactory.BodyFont(); text.fontSize = size;
            text.alignment = TextAlignmentOptions.MidlineLeft; text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = size * .82f;
            HudPlace(text.rectTransform, x, y, width, height);
            return text;
        }
        private static void StretchHudText(TMP_Text text)
        {
            text.rectTransform.anchorMax = new Vector2(1, 1);
            text.rectTransform.sizeDelta = new Vector2(-text.rectTransform.anchoredPosition.x, text.rectTransform.sizeDelta.y);
        }
        private RectTransform HudIconButton(Transform parent, string name, string icon, System.Action action)
        {
            var button = MenuButton(parent, "", action, 52, 52, 16);
            button.name = name; AddIconChild(button.gameObject, RoyalIcons.Get(icon), UIFactory.Text, 24);
            return (RectTransform)button.transform;
        }
        private static void CompactPowerup(Button button, TMP_Text count)
        {
            button.name = "Powerup";
            var outline = button.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
            var fillet = button.transform.Find("GoldFillet"); if (fillet != null) fillet.gameObject.SetActive(false);
            var icon = (RectTransform)button.transform.Find("Icon");
            if (icon != null) { icon.sizeDelta = new Vector2(26, 26); icon.anchoredPosition = new Vector2(-15, 0); }
            count.font = UIFactory.BodyFont(); count.fontSize = 15;
            count.alignment = TextAlignmentOptions.Center;
            count.rectTransform.anchorMin = count.rectTransform.anchorMax = count.rectTransform.pivot = new Vector2(.5f, .5f);
            count.rectTransform.sizeDelta = new Vector2(30, 28); count.rectTransform.anchoredPosition = new Vector2(18, 0);
        }
        private void BuildModernBoss(Transform parent)
        {
            _bossHudRt = HudRect(parent, "BossHealth", 0, 0, 300, 64);
            _bossHudRt.anchorMax = new Vector2(1, 1); _bossHudRt.sizeDelta = new Vector2(0, 64);
            _bossHud = _bossHudRt.gameObject;
            _hudBossLabel = HudText(_bossHudRt, "BossCaption", "THE QUEEN", 14, UIFactory.Text, 0, 0, 300, 22);
            StretchHudText(_hudBossLabel);
            _bossSegs = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var segment = HudSurface(_bossHudRt, "Health" + i, UIFactory.Gold);
                segment.anchorMin = new Vector2(i / 3f, 0); segment.anchorMax = new Vector2((i + 1) / 3f, 0);
                segment.pivot = new Vector2(0, 0); segment.anchoredPosition = new Vector2(0, 16);
                segment.sizeDelta = new Vector2(i < 2 ? -5 : 0, 12); _bossSegs[i] = segment.GetComponent<Image>();
            }
            _bossHud.SetActive(false);
        }
        private void BuildModernTallScroll()
        {
            var track = HudSurface(_hud.transform, "MapScroll", UIFactory.Hex("#26354A"));
            track.GetComponent<Image>().raycastTarget = true;
            track.anchorMin = track.anchorMax = track.pivot = new Vector2(1, .5f);
            track.sizeDelta = new Vector2(16, 300); track.anchoredPosition = new Vector2(-4, 0);
            _tallScrollbar = track.gameObject.AddComponent<Scrollbar>();
            _tallScrollbar.direction = Scrollbar.Direction.TopToBottom;
            var handle = HudSurface(track, "Handle", UIFactory.Gold); UIFactory.Stretch(handle);
            _tallScrollbar.handleRect = handle; _tallScrollbar.targetGraphic = handle.GetComponent<Image>();
            _tallScrollbar.onValueChanged.AddListener(OnTallScrollChanged); track.gameObject.SetActive(false);
        }
        private void BuildBattleFormIndicator()
        {
            _battleForm = HudRect(_hudStatus, "BattleForm", 116, 4, 44, 44);
            var track = HudSurface(_battleForm, "TimerTrack", UIFactory.Hex("#344157"));
            UIFactory.Stretch(track); track.GetComponent<Image>().sprite = UISprites.Circle();
            var timer = HudSurface(_battleForm, "FormTimer", UIFactory.Gold);
            UIFactory.Stretch(timer); _battleFormRadial = timer.GetComponent<Image>();
            _battleFormRadial.sprite = UISprites.Circle(); _battleFormRadial.type = Image.Type.Filled;
            _battleFormRadial.fillMethod = Image.FillMethod.Radial360;
            _battleFormRadial.fillOrigin = (int)Image.Origin360.Top; _battleFormRadial.fillClockwise = true;
            var center = HudSurface(_battleForm, "TimerInset", UIFactory.Bg);
            HudPlace(center, 3, 3, 38, 38); center.GetComponent<Image>().sprite = UISprites.Circle();
            _battleFormGlyph = HudText(_battleForm, "ChessIcon", "", 30, UIFactory.Gold, 6, 6, 32, 32);
            _battleFormGlyph.font = UIFactory.ChessFont(); _battleFormGlyph.alignment = TextAlignmentOptions.Center;
            for (int i = 0; i < _battleCapturePips.Length; i++)
            {
                var pip = HudSurface(_battleForm, "CapturePip" + i, UIFactory.Muted);
                HudPlace(pip, 12 + i * 8, 38, 4, 4); pip.GetComponent<Image>().sprite = UISprites.Circle();
                _battleCapturePips[i] = pip.GetComponent<Image>();
            }
            _battleForm.gameObject.SetActive(false);
        }
        private void LayoutModernHud()
        {
            if (_hudHeader == null) return;
            _battleMultiplierAlignmentDirty = true;
            float unit = HudScale();
            _hudContentWidth = Mathf.Min(720, Screen.safeArea.width / unit - 40);
            float center = (Screen.safeArea.center.x - Screen.width * .5f) / unit;
            _topBarRt.sizeDelta = new Vector2(0, TopBarCanvas()); _topBarRt.anchoredPosition = Vector2.zero;
            _bottomBarRt.sizeDelta = new Vector2(0, BottomBarCanvas()); _bottomBarRt.anchoredPosition = Vector2.zero;
            _hudHeader.sizeDelta = new Vector2(_hudContentWidth, 104);
            _hudHeader.anchoredPosition = new Vector2(center, -TopOffsetCanvas() - 2);
            const float footerHeight = 52;
            _hudFooter.sizeDelta = new Vector2(_hudContentWidth, footerHeight);
            _hudFooter.anchoredPosition = new Vector2(center, BottomBarCanvas() - footerHeight);
            float textWidth = _hudContentWidth - 132;
            _modeBadgeRt.sizeDelta = _hudContext.rectTransform.sizeDelta = new Vector2(textWidth, 18);
            HudPlace(_scoreRt, 0, _gm.BoardHudTutorial ? 18 : 24, textWidth, 34); _scoreText.fontSizeMax = 26; _scoreText.fontSizeMin = 21;
            HudPlace(_advMovesRt, 0, 17, textWidth, 36); _advMovesLabel.fontSizeMax = 18; _advMovesLabel.fontSizeMin = 15;
            HudPlace(_restartRt, _hudContentWidth - 100, 4, 44, 44);
            HudPlace(_pauseRt, _hudContentWidth - 44, 4, 44, 44);
            _scoreText.alignment = TextAlignmentOptions.BaselineLeft;
            var divider = (RectTransform)_hudHeader.Find("HeaderDivider");
            divider.anchoredPosition = new Vector2(0, -56);
            HudPlace(_hudStatus, 0, 64, 0, 40);
            _hudStatus.anchorMax = new Vector2(1, 1);
            _hudStatusRight.sizeDelta = new Vector2(-132, 40);
            HudPlace(_hudStatusTitle.rectTransform, 0, 0, 112, 14); _hudStatusTitle.fontSizeMax = 10; _hudStatusTitle.fontSizeMin = 9;
            _hudStatusTitle.alignment = TextAlignmentOptions.MidlineLeft;
            HudPlace(_hudStatusValue.rectTransform, 0, 14, 112, 26); _hudStatusValue.fontSizeMax = 21; _hudStatusValue.fontSizeMin = 17;
            _hudStatusValue.alignment = TextAlignmentOptions.MidlineLeft;
            for (int i = 0; i < 3; i++) HudPlace(_hudStars[i].rectTransform, i * 28, 17, 22, 22);
            _hudStatusHeading.rectTransform.anchoredPosition = Vector2.zero;
            _hudStatusHeading.rectTransform.sizeDelta = new Vector2(0, 18); _hudStatusHeading.fontSizeMax = 13;
            _hudStatusDetail.rectTransform.anchoredPosition = new Vector2(0, -18);
            _hudStatusDetail.rectTransform.sizeDelta = new Vector2(0, 17); _hudStatusDetail.fontSizeMax = 11;
            _hudStatusDetail.fontSizeMin = 10;
            _hudTrack.anchoredPosition = new Vector2(0, -38); _hudTrack.sizeDelta = new Vector2(0, 2);
            _bossHudRt.sizeDelta = new Vector2(0, 40);
            foreach (var segment in _bossSegs)
            {
                segment.rectTransform.anchoredPosition = new Vector2(0, 2);
                segment.rectTransform.sizeDelta = new Vector2(segment.rectTransform.sizeDelta.x, 10);
            }
            float buttonHeight = Mathf.Min(44, footerHeight - 8);
            HudPlace(_divineBtnRt, _hudContentWidth - 160, 4, 76, buttonHeight);
            HudPlace(_chronoBtnRt, _hudContentWidth - 76, 4, 76, buttonHeight);
            HudPlace(_pathBtnRt, _hudContentWidth - 76, 4, 76, buttonHeight);
            _enemyBarRt.sizeDelta = new Vector2(_hudContentWidth - 188, footerHeight - 8);
            _enemyBarRt.anchoredPosition = new Vector2(0, -4);
            var timerTrack = (RectTransform)_enemyBarRt.Find("TimerTrack");
            timerTrack.anchoredPosition = new Vector2(0, -(footerHeight - 18));
            _biomeLabel.rectTransform.sizeDelta = new Vector2(_hudContentWidth - 188, footerHeight - 8);
            if (_hudBattleLayout)
            {
                // Score, multiplier, form and navigation share one aligned row.
                // The camera's original header allowance is unchanged.
                float scoreWidth = _hudContentWidth * .5f - 112;
                _scoreRt.sizeDelta = new Vector2(scoreWidth, 34);
                _modeBadgeRt.sizeDelta = _hudContext.rectTransform.sizeDelta = new Vector2(scoreWidth, 18);
                _hudStatus.anchorMin = _hudStatus.anchorMax = _hudStatus.pivot = new Vector2(.5f, 1);
                _hudStatus.anchoredPosition = Vector2.zero;
                _hudStatus.sizeDelta = new Vector2(184, 60);
                HudPlace(_hudStatusTitle.rectTransform, 0, 0, 92, 18);
                _hudStatusTitle.alignment = TextAlignmentOptions.Center;
                _hudStatusTitle.fontSizeMax = _modeBadge.fontSizeMax; _hudStatusTitle.fontSizeMin = _modeBadge.fontSizeMin;
                HudPlace(_hudStatusValue.rectTransform, 0, 18, 92, 34);
                _hudStatusValue.alignment = TextAlignmentOptions.Baseline;
                _hudStatusValue.fontSizeMax = 26; _hudStatusValue.fontSizeMin = 21;
            }
            if (_tutorialCard != null) LayoutModernTutorial();
        }
        private static void HudValue(TMP_Text label, string value) { if (label.text != value) label.text = value; }
        private static string HudClock(float seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");

        private void UpdateModernHud()
        {
            _biomeShown = (int)_gm.CurrentBiome;
            bool tutorial = _gm.BoardHudTutorial;
            bool adventure = _advMovesLabel.gameObject.activeSelf;
            bool battle = !adventure && _gm.CurrentMode == GameMode.Battle;
            bool trap = !tutorial && !adventure && !battle;
            bool compactBattle = battle && !tutorial;
            if (_hudWasTutorial != tutorial || _hudBattleLayout != compactBattle)
            {
                _hudWasTutorial = tutorial; _hudBattleLayout = compactBattle; LayoutModernHud();
            }
            _hudStatus.gameObject.SetActive(!tutorial && !trap);
            _hudHeader.Find("HeaderDivider").gameObject.SetActive(!trap && !compactBattle);
            _topBarRt.sizeDelta = new Vector2(0, TopOffsetCanvas() + (trap || compactBattle ? 60 : HudTopContentH));
            _battleForm.gameObject.SetActive(compactBattle);
            _hudStatusRight.gameObject.SetActive(!compactBattle);
            _hudContext.gameObject.SetActive(!_modeBadgeRt.gameObject.activeSelf);
            HudValue(_hudContext, tutorial ? "TUTORIAL" : adventure ? "ADVENTURE" : "DAILY CHALLENGE");
            if (tutorial)
            {
                _modeBadgeRt.gameObject.SetActive(false); _hudContext.gameObject.SetActive(true);
                HudValue(_scoreText, _gm.CurrentMode == GameMode.Battle ? "Battle training" : "Trap training");
            }
            else if (!adventure) HudValue(_scoreText, _gm.Score.ToString("N0") + " <size=40%><color=#A9B5CC>SCORE</color></size>");
            bool boss = _bossHud.activeSelf;
            _hudStatusHeading.gameObject.SetActive(!boss); _hudStatusDetail.gameObject.SetActive(!boss);
            _hudTrack.gameObject.SetActive(!boss && !tutorial);
            _hudStatusValue.gameObject.SetActive(!adventure);
            for (int i = 0; i < 3; i++) _hudStars[i].gameObject.SetActive(adventure);
            _hudFooter.gameObject.SetActive(!tutorial);
            _biomeLabel.gameObject.SetActive(adventure && !tutorial);
            if (tutorial)
            {
                HudValue(_hudContext, "TUTORIAL · " + _gm.PlayerForm.ToString().ToUpperInvariant());
                return;
            }
            if (adventure)
            {
                int moves = _gm.AdventureMoves;
                int rating = moves <= _gm.ThreeStarLimit ? 3 : moves <= _gm.TwoStarLimit ? 2 : 1;
                HudValue(_hudStatusTitle, "STAR PACE");
                for (int i = 0; i < 3; i++) _hudStars[i].color = i < rating ? UIFactory.Gold : UIFactory.Hex("#344157");
                int left = (rating == 3 ? _gm.ThreeStarLimit : _gm.TwoStarLimit) - moves + 1;
                HudValue(_hudStatusHeading, rating == 1 ? "Finish to earn one star" : left + (left == 1 ? " move" : " moves") + " until " + (rating - 1) + "-star pace");
                HudValue(_hudStatusDetail, rating == 1 ? "Complete the level to earn a star" :
                    "Finish within " + (rating == 3 ? _gm.ThreeStarLimit : _gm.TwoStarLimit) + " moves for " + rating + " stars");
                HudValue(_biomeLabel, _gm.BoardObjective);
                _hudProgress.rectTransform.anchorMax = new Vector2(rating / 3f, 1);
            }
            else if (battle)
            {
                bool transformed = _gm.TransformActive;
                string multiplier = "×" + _gm.ScoreMultiplier;
                if (_hudStatusValue.text != multiplier) _battleMultiplierAlignmentDirty = true;
                HudValue(_hudStatusTitle, "MULTIPLIER"); HudValue(_hudStatusValue, multiplier);
                if (_battleMultiplierAlignmentDirty)
                {
                    _hudStatusTitle.ForceMeshUpdate(); _hudStatusValue.ForceMeshUpdate();
                    var titleBounds = _hudStatusTitle.textBounds;
                    var valueBounds = _hudStatusValue.textBounds;
                    float top = Mathf.Max(_hudStatusTitle.rectTransform.anchoredPosition.y + titleBounds.max.y,
                        _hudStatusValue.rectTransform.anchoredPosition.y + valueBounds.max.y);
                    float bottom = Mathf.Min(_hudStatusTitle.rectTransform.anchoredPosition.y + titleBounds.min.y,
                        _hudStatusValue.rectTransform.anchoredPosition.y + valueBounds.min.y);
                    float timerCenter = _battleForm.anchoredPosition.y - _battleForm.rect.height * .5f;
                    var offset = new Vector2(0, timerCenter - (top + bottom) * .5f);
                    _hudStatusTitle.rectTransform.anchoredPosition += offset;
                    _hudStatusValue.rectTransform.anchoredPosition += offset;
                    _battleMultiplierAlignmentDirty = false;
                }
                string formGlyph = GameConfig.Glyph(transformed ? _gm.PlayerForm : PieceType.Knight);
                if (_battleFormGlyph.text != formGlyph)
                {
                    _battleFormGlyph.text = formGlyph;
                    _battleFormGlyph.ForceMeshUpdate();
                    // Center the visible chess silhouette, including each font
                    // glyph's side bearings and vertical offset.
                    Vector2 correction = _battleFormGlyph.rectTransform.rect.center - (Vector2)_battleFormGlyph.textBounds.center;
                    _battleFormGlyph.rectTransform.anchoredPosition = new Vector2(6, -6) + correction;
                }
                _battleFormRadial.gameObject.SetActive(transformed);
                _battleFormRadial.fillAmount = transformed ? Mathf.Clamp01(_gm.TransformationSeconds / GameConfig.TransformSeconds) : 0;
                for (int i = 0; i < _battleCapturePips.Length; i++)
                {
                    _battleCapturePips[i].gameObject.SetActive(!transformed);
                    _battleCapturePips[i].color = i < _gm.TransformationCaptures ? UIFactory.Gold : UIFactory.Hex("#344157");
                }
            }
        }

        private void BuildModernTutorial()
        {
            _tutorial = _ui.CreateCanvas("Tutorial", 110, new Vector2(HudRefW, HudRefH)).gameObject;
            _tutorialCard = HudSurface(_tutorial.transform, "LessonCard", UIFactory.Bg);
            _tutorialCard.anchorMin = _tutorialCard.anchorMax = _tutorialCard.pivot = new Vector2(.5f, 1);
            _tutCaption = HudText(_tutorialCard, "Lesson", "", 14, UIFactory.Text, 0, 0, 500, 44);
            _tutCaption.textWrappingMode = TextWrappingModes.Normal; _tutCaption.alignment = TextAlignmentOptions.TopLeft;
            _tutCaption.fontSizeMin = 12;
            _tutorialActions = HudRect(_tutorial.transform, "LessonActions", 0, 0, 500, 44);
            _tutorialActions.anchorMin = _tutorialActions.anchorMax = _tutorialActions.pivot = new Vector2(.5f, 0);
            var skip = MenuButton(_tutorialActions, "Skip tutorial", () => _gm.TutorialSkip(), 140, 44, 15, textOnly: true);
            skip.name = "SkipTutorial";
            var cont = MenuButton(_tutorialActions, "Continue", () => _gm.TutorialContinue(), 156, 44, 16, primary: true);
            cont.name = "ContinueTutorial"; _tutContinueBtn = cont.gameObject;
            LayoutModernTutorial(); _tutorial.SetActive(false);
        }
        private void LayoutModernTutorial()
        {
            if (_tutorialCard == null) return;
            float center = (Screen.safeArea.center.x - Screen.width * .5f) / HudScale();
            _tutorialCard.sizeDelta = new Vector2(_hudContentWidth, 44);
            _tutorialCard.anchoredPosition = new Vector2(center, -TopOffsetCanvas() - 62);
            _tutCaption.rectTransform.sizeDelta = new Vector2(_hudContentWidth, 44);
            _tutorialActions.sizeDelta = new Vector2(_hudContentWidth, 44);
            _tutorialActions.anchoredPosition = new Vector2(center, BottomBarCanvas() - 48);
            HudPlace((RectTransform)_tutorialActions.Find("SkipTutorial"), 0, 0, 140, 44);
            HudPlace((RectTransform)_tutContinueBtn.transform, _hudContentWidth - 156, 0, 156, 44);
        }
    }
}
