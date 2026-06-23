using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Builds and runs the screen flow: Menu -> HUD (+ Pause overlay) -> GameOver.
    // Logic lives in GameManager; this just constructs the UI and relays button
    // presses back to it.
    public class UIManager : MonoBehaviour
    {
        private GameManager _gm;
        private UIFactory _ui;

        private GameObject _menu, _hud, _pause, _gameOver;
        private TMP_Text _scoreText, _gameOverScore, _savedName, _menuWarn;
        private TMP_InputField _nameInput;

        private GameMode _selMode = GameMode.Trap;
        private Difficulty _selDiff = Difficulty.Medium;
        private Button _trapBtn, _battleBtn;
        private readonly Dictionary<Difficulty, Button> _diffBtns = new Dictionary<Difficulty, Button>();

        public void Init(GameManager gm, GameTheme theme)
        {
            _gm = gm;
            _ui = new UIFactory(theme != null ? theme.uiFont : null);
            BuildMenu();
            BuildHud();
            BuildPause();
            BuildGameOver();
            ShowMenu();
        }

        private void Update()
        {
            if (_hud != null && _hud.activeSelf && _scoreText != null)
                _scoreText.text = _gm.Score.ToString();
        }

        // ── Screen visibility ────────────────────────────────────────────────
        private void HideAll()
        {
            _menu.SetActive(false); _hud.SetActive(false);
            _pause.SetActive(false); _gameOver.SetActive(false);
        }
        public void ShowMenu() { HideAll(); _nameInput.text = SaveData.GetName(); _menuWarn.text = ""; _menu.SetActive(true); }
        public void ShowHud() { HideAll(); _hud.SetActive(true); }
        public void ShowPause() { _hud.SetActive(true); _pause.SetActive(true); _menu.SetActive(false); _gameOver.SetActive(false); }
        public void ShowGameOver(int score)
        {
            HideAll();
            _gameOverScore.text = "Score: " + score;
            _savedName.text = SaveData.GetName();
            _gameOver.SetActive(true);
        }

        // ── Menu ─────────────────────────────────────────────────────────────
        private void BuildMenu()
        {
            var canvas = _ui.CreateCanvas("Menu", 100);
            _menu = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, UIFactory.Bg);
            var col = _ui.Column(screen.transform, 14f);

            _ui.Label(col, "KNIGHT'S GAUNTLET", 34, UIFactory.Gold, FontStyles.Bold);
            _ui.Label(col, "Move in L-shapes. Survive the endless battlefield.", 14, UIFactory.Muted);

            var modeRow = _ui.Row(col);
            _trapBtn = _ui.Button(modeRow, "Trap", () => SetMode(GameMode.Trap), UIFactory.Panel, UIFactory.Muted, 120, 56, 16);
            _battleBtn = _ui.Button(modeRow, "Battle", () => SetMode(GameMode.Battle), UIFactory.Panel, UIFactory.Muted, 120, 56, 16);

            _ui.Label(col, "DIFFICULTY", 12, UIFactory.Muted);
            var diffRow = _ui.Row(col, 8f);
            _diffBtns[Difficulty.Easy] = _ui.Button(diffRow, "Easy", () => SetDiff(Difficulty.Easy), UIFactory.Panel, UIFactory.Muted, 86, 46, 13);
            _diffBtns[Difficulty.Medium] = _ui.Button(diffRow, "Med", () => SetDiff(Difficulty.Medium), UIFactory.Panel, UIFactory.Muted, 86, 46, 13);
            _diffBtns[Difficulty.Hard] = _ui.Button(diffRow, "Hard", () => SetDiff(Difficulty.Hard), UIFactory.Panel, UIFactory.Muted, 86, 46, 13);
            _diffBtns[Difficulty.Grandmaster] = _ui.Button(diffRow, "GM", () => SetDiff(Difficulty.Grandmaster), UIFactory.Panel, UIFactory.Muted, 86, 46, 13);

            _nameInput = _ui.Input(col, "ENTER NAME", 7);
            _menuWarn = _ui.Label(col, "", 12, UIFactory.Amber);

            _ui.Button(col, "PLAY", OnPlay, UIFactory.Gold, UIFactory.Bg, 220, 54, 20);
            _ui.Button(col, "Leaderboard", OnLeaderboard, UIFactory.Panel, UIFactory.Gold, 220, 44, 14);

            SetMode(_selMode);
            SetDiff(_selDiff);
        }

        private void SetMode(GameMode m)
        {
            _selMode = m;
            UIFactory.StyleToggle(_trapBtn, m == GameMode.Trap);
            UIFactory.StyleToggle(_battleBtn, m == GameMode.Battle);
        }

        private void SetDiff(Difficulty d)
        {
            _selDiff = d;
            foreach (var kv in _diffBtns) UIFactory.StyleToggle(kv.Value, kv.Key == d);
        }

        private void OnPlay()
        {
            var raw = (_nameInput.text ?? "").Trim();
            if (raw.Length > 7) raw = raw.Substring(0, 7);
            if (string.IsNullOrEmpty(raw)) { _menuWarn.color = UIFactory.Amber; _menuWarn.text = "Enter a name to play."; return; }
            if (!NameFilter.IsAllowed(raw)) { _menuWarn.color = UIFactory.Error; _menuWarn.text = "Please choose a different name."; return; }
            _menuWarn.text = "";
            SaveData.SetName(raw);
            _gm.StartGame(_selMode, _selDiff);
        }

        private void OnLeaderboard()
        {
            _menuWarn.color = UIFactory.Muted;
            _menuWarn.text = "Leaderboard arrives in the next phase.";
        }

        // ── HUD ──────────────────────────────────────────────────────────────
        private void BuildHud()
        {
            var canvas = _ui.CreateCanvas("HUD", 90);
            _hud = canvas.gameObject;

            // Score, top-left.
            _scoreText = _ui.Label(canvas.transform, "0", 26, UIFactory.Gold, FontStyles.Bold, 200);
            var sRt = _scoreText.rectTransform;
            sRt.anchorMin = sRt.anchorMax = new Vector2(0, 1);
            sRt.pivot = new Vector2(0, 1);
            sRt.anchoredPosition = new Vector2(24, -20);
            _scoreText.alignment = TextAlignmentOptions.TopLeft;

            // Pause, top-right.
            var pause = _ui.Button(canvas.transform, "II", () => _gm.PauseGame(), UIFactory.Panel, UIFactory.Gold, 56, 48, 18);
            var pRt = pause.GetComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = new Vector2(1, 1);
            pRt.pivot = new Vector2(1, 1);
            pRt.anchoredPosition = new Vector2(-20, -16);
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
            _ui.Button(col, "Restart", () => _gm.RestartGame(), UIFactory.Panel, UIFactory.Gold);
            _ui.Button(col, "Main Menu", () => _gm.ToMenu(), UIFactory.Panel, UIFactory.Gold);
        }

        // ── Game over ────────────────────────────────────────────────────────
        private void BuildGameOver()
        {
            var canvas = _ui.CreateCanvas("GameOver", 110);
            _gameOver = canvas.gameObject;
            var screen = _ui.Screen(canvas.transform, new Color(0.08f, 0.08f, 0.16f, 0.95f));
            var col = _ui.Column(screen.transform, 14f);
            _ui.Label(col, "GAME OVER", 28, UIFactory.Gold, FontStyles.Bold);
            _gameOverScore = _ui.Label(col, "Score: 0", 24, UIFactory.Gold, FontStyles.Bold);
            _savedName = _ui.Label(col, "", 14, UIFactory.Muted);
            _ui.Button(col, "Restart", () => _gm.RestartGame(), UIFactory.Gold, UIFactory.Bg);
            _ui.Button(col, "Home", () => _gm.ToMenu(), UIFactory.Panel, UIFactory.Gold);
        }
    }
}
