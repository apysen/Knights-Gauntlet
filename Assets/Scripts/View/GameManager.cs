using UnityEngine;
using UnityEngine.EventSystems;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // The one component you add to the scene. Builds camera/board/renderers/UI at
    // runtime and runs the menu -> play -> pause -> game-over loop, with a shared
    // Supabase leaderboard (custom UI) and achievement tracking.
    public class GameManager : MonoBehaviour
    {
        [Header("Setup")]
        public GameTheme theme;   // optional; a runtime default is made if null
        public Camera cam;        // optional; defaults to Camera.main

        [Header("Links")]
        public string bugReportUrl = "";

        private enum State { Menu, Playing, Paused, Dead }
        private State _state = State.Menu;

        private GameSimulation _sim;
        private BoardView _board;
        private GameView _view;
        private UIManager _ui;
        private LeaderboardClient _lb;
        private LeaderboardScreen _lbScreen;
        private AchievementTracker _ach;

        public int Score => _sim != null ? _sim.Score : 0;

        private static readonly Color PopupGain = new Color32(0x9B, 0xE8, 0xA0, 0xFF);
        private static readonly Color PopupCombo = new Color32(0xFF, 0xD0, 0x60, 0xFF);
        private static readonly Color PopupShield = new Color32(0xFF, 0x9A, 0x5C, 0xFF);

        private void Start()
        {
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<GameTheme>();
                Debug.Log("[Knight's Gauntlet] No GameTheme assigned — using a runtime default " +
                          "(glyph graphics). Create one via Create > Knight's Gauntlet > Game Theme to add art.");
            }

            if (cam == null) cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cgo.AddComponent<Camera>();
            }

            var bgo = new GameObject("Board"); bgo.transform.SetParent(transform, false);
            _board = bgo.AddComponent<BoardView>();
            _board.Init(theme);

            var vgo = new GameObject("View"); vgo.transform.SetParent(transform, false);
            _view = vgo.AddComponent<GameView>();
            _view.Init(theme, _board);

            _sim = new GameSimulation();
            _sim.OnFormChanged += form => _view.SetPlayerForm(form, CurrentSkin());
            _sim.OnShielded += () => _view.SpawnPopup(_sim.PlayerCol, _sim.PlayerY - 0.3f, "shielded!", PopupShield);

            _ach = new AchievementTracker(_sim, OnAchievementUnlocked);

            var lgo = new GameObject("Leaderboard"); lgo.transform.SetParent(transform, false);
            _lb = lgo.AddComponent<LeaderboardClient>();
            var sb = SupabaseConfig.Load();
            if (sb != null) { _lb.supabaseUrl = sb.url; _lb.supabaseKey = sb.key; }
            else Debug.Log("[Knight's Gauntlet] No SupabaseConfig in Resources — online leaderboard is off. " +
                           "Create Assets/Resources/SupabaseConfig.asset to enable it.");

            var lsgo = new GameObject("LeaderboardUI"); lsgo.transform.SetParent(transform, false);
            _lbScreen = lsgo.AddComponent<LeaderboardScreen>();
            _lbScreen.Init(theme, _lb);

            var ugo = new GameObject("UI"); ugo.transform.SetParent(transform, false);
            _ui = ugo.AddComponent<UIManager>();
            _ui.Init(this, theme);

            _board.FitCamera(cam);

            // Reflect the player's current skin on their existing board rows.
            _lb.SyncEquippedSkin(SaveData.GetName(), SaveData.GetSkin());
        }

        // ── Public API (called by UIManager) ─────────────────────────────────
        public void StartGame(GameMode mode, Difficulty difficulty)
        {
            _sim.Start(mode, difficulty);
            _ach.Reset();
            _view.SetPlayerForm(_sim.PlayerForm, CurrentSkin());
            _state = State.Playing;
            _ui.ShowHud();
        }

        public void PauseGame() { if (_state != State.Playing) return; _state = State.Paused; _ui.ShowPause(); }
        public void ResumeGame() { if (_state != State.Paused) return; _state = State.Playing; _ui.ShowHud(); }
        public void RestartGame() => StartGame(_sim.Mode, _sim.Difficulty);
        public void ToMenu() { _state = State.Menu; _ui.ShowMenu(); }

        public void ShowPauseScreen() => _ui.ShowPause();
        public void ShowLeaderboard() => _lbScreen.Open(_sim.Mode, _sim.Difficulty);
        public void ReportBug()
        {
            if (!string.IsNullOrEmpty(bugReportUrl)) Application.OpenURL(bugReportUrl);
            else Debug.Log("[Knight's Gauntlet] Set a Bug Report URL on the GameManager to enable this.");
        }

        // ── Loop ─────────────────────────────────────────────────────────────
        private void Update()
        {
            _board.FitCamera(cam);

            if (_state == State.Playing)
            {
                _sim.Tick(Time.deltaTime);
                _ach.Tick(Time.deltaTime);
                HandleInput();
                if (!_sim.Alive) EnterDead();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_state == State.Playing) PauseGame();
                else if (_state == State.Paused) ResumeGame();
            }
            if (_state == State.Dead && Input.GetKeyDown(KeyCode.R)) RestartGame();

            _board.SyncBackground(_sim.ScrollCells);
            bool showHints = _sim.Difficulty != Difficulty.Grandmaster && _state == State.Playing;
            _view.Sync(_sim, CurrentSkin(), showHints, Time.deltaTime);
            _ui.SetEnemyBar(_sim.Mode == GameMode.Battle && _state == State.Playing, _sim.EnemyMoveProgress);
        }

        private void HandleInput()
        {
            if (_state != State.Playing) return;
            if (OverUI()) return;

            if (GetTapDown(out Vector2 screenPos))
            {
                Vector3 world = cam.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                var (col, simY) = _board.WorldToCell(world);
                var result = _sim.TryTap(col, simY);
                if (!result.Moved) return;

                _ach.OnMove(result);
                if (result.Captures != null)
                {
                    foreach (var cap in result.Captures)
                    {
                        _view.SpawnPopup(cap.Col, cap.Y - 0.42f, "+" + cap.PointsGained, PopupGain);
                        _view.SpawnPopup(cap.Col, cap.Y + 0.14f,
                            "\u00D7" + cap.NewMultiplier + " (+" + cap.MultiplierDelta + ")", PopupCombo);
                    }
                }
            }
        }

        private void EnterDead()
        {
            _state = State.Dead;
            bool isTrap = _sim.Mode == GameMode.Trap;
            DeathAnalysis analysis = default;
            if (isTrap) analysis = _sim.BuildDeathAnalysis();

            string category = _sim.Mode.ToString().ToLower() + "-" + _sim.Difficulty.ToString().ToLower();
            _lb.SubmitScore(category, SaveData.GetName(), _sim.Score, SaveData.GetSkin());

            _ui.ShowGameOver(_sim.Score, isTrap, analysis);
        }

        private void OnAchievementUnlocked(string id)
        {
            if (!Achievements.Unlock(id)) return;
            if (Achievements.TryFind(id, out var def))
                _ui.ShowToast(def.Name + " unlocked — " + def.SkinName + " skin earned");
        }

        private static bool OverUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return es.IsPointerOverGameObject();
        }

        private static bool GetTapDown(out Vector2 pos)
        {
            pos = default;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) { pos = t.position; return true; }
                return false;
            }
            if (Input.GetMouseButtonDown(0)) { pos = Input.mousePosition; return true; }
            return false;
        }

        private static string CurrentSkin() => SaveData.GetSkin();
    }
}
