using UnityEngine;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // The one component you add to the scene. Add it to an empty GameObject,
    // optionally assign a GameTheme, and press Play — it builds the camera,
    // board, and renderers at runtime and starts a game.
    //
    // Menus, "Was I trapped?", popups, threat-tile previews and the transform
    // timer bar come in later phases; this phase is "trap mode plays in editor".
    public class GameManager : MonoBehaviour
    {
        [Header("Setup")]
        public GameTheme theme;                 // optional; a default is made if null
        public Camera cam;                      // optional; defaults to Camera.main
        public TMP_Text scoreLabel;             // optional HUD label

        [Header("Game")]
        public GameMode startMode = GameMode.Trap;
        public Difficulty startDifficulty = Difficulty.Medium;
        public bool autoStart = true;

        private enum State { Idle, Playing, Dead }
        private State _state = State.Idle;

        private GameSimulation _sim;
        private BoardView _board;
        private GameView _view;

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
            _sim.OnShielded += () => Debug.Log("[Knight's Gauntlet] Shielded!");

            _board.FitCamera(cam);

            if (autoStart) StartGame(startMode, startDifficulty);
        }

        public void StartGame(GameMode mode, Difficulty difficulty)
        {
            _sim.Start(mode, difficulty);
            _view.SetPlayerForm(_sim.PlayerForm, CurrentSkin());
            _state = State.Playing;
        }

        private void Update()
        {
            _board.FitCamera(cam);

            if (_state == State.Playing)
            {
                _sim.Tick(Time.deltaTime);
                HandleInput();
                if (!_sim.Alive) EnterDead();
            }
            else
            {
                HandleInput(); // tap / R to restart while dead
            }

            bool showHints = _sim.Difficulty != Difficulty.Grandmaster;
            _board.SyncBackground(_sim.ScrollCells);
            _view.Sync(_sim, CurrentSkin(), showHints);

            if (scoreLabel != null) scoreLabel.text = _sim.Score.ToString();
        }

        private void HandleInput()
        {
            if (_state == State.Dead)
            {
                if (Input.GetKeyDown(KeyCode.R) || GetTapDown(out _))
                    StartGame(_sim.Mode, _sim.Difficulty);
                return;
            }
            if (_state != State.Playing) return;

            if (GetTapDown(out Vector2 screenPos))
            {
                Vector3 world = cam.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                var (col, simY) = _board.WorldToCell(world);
                var result = _sim.TryTap(col, simY);
                // result.Captures will drive popups in a later phase.
            }
        }

        private void EnterDead()
        {
            _state = State.Dead;
            if (_sim.Mode == GameMode.Trap)
            {
                var a = _sim.BuildDeathAnalysis();
                Debug.Log($"[Knight's Gauntlet] Game over — score {_sim.Score}. " +
                          $"Was I trapped? {a.Reason} (escaped={a.Escaped}).");
            }
            else
            {
                Debug.Log($"[Knight's Gauntlet] Game over — score {_sim.Score}. Tap or press R to restart.");
            }
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

        private static string CurrentSkin() => PlayerPrefs.GetString("kg_skin", "gold");
    }
}
