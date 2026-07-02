using System;
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

        private enum State { Menu, Playing, Paused, Dead, Reviving }
        private State _state = State.Menu;

        private bool _daily;
        private int _reviveCount;
        private float _reviveCountdown;
        private bool _runEnded;

        private GameSimulation _sim;
        private BoardView _board;
        private GameView _view;
        private UIManager _ui;
        private LeaderboardClient _lb;
        private LeaderboardScreen _lbScreen;
        private AchievementTracker _ach;
        private IAdService _ads = new NoOpAdService();
        private bool _dailyRewardChecked;
        private bool _wasTransformed;   // for firing the wash pulse only on a NEW transform
        private float _washPulse;       // 1..0 envelope of the current transform pulse
        private static readonly int[] DailyDayRewards = { 1000, 750, 500, 250, 100 };

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
            _ads.Initialize();

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

            CheckDailyReward();
        }

        // ── Public API (called by UIManager) ─────────────────────────────────
        public void StartGame(GameMode mode, Difficulty difficulty)
        {
            _daily = false; _reviveCount = 0; _runEnded = false;
            _ads.HideBanner();
            _sim.Start(mode, difficulty);
            _ach.Reset();
            _view.SetPlayerForm(_sim.PlayerForm, CurrentSkin());
            _state = State.Playing;
            _ui.ShowHud();
        }

        // Daily challenge: Grandmaster rules at medium scroll speed, identical
        // seeded board for everyone, 10 attempts/day, no pickups, no revives.
        public void StartDaily()
        {
            if (SaveData.DailyAttemptsLeft() <= 0)
            {
                _ui.ShowToast("No daily attempts left — resets tomorrow.");
                _ui.ShowMenu();
                return;
            }
            SaveData.UseDailyAttempt();
            _daily = true; _reviveCount = 0; _runEnded = false;
            _ads.HideBanner();
            _sim.Start(GameMode.Trap, Difficulty.Grandmaster, SaveData.DailySeed(), mediumSpeed: true, pickups: false);
            _ach.Reset();
            _view.SetPlayerForm(_sim.PlayerForm, CurrentSkin());
            _state = State.Playing;
            _ui.ShowHud();
        }

        public void PauseGame() { if (_state != State.Playing) return; _state = State.Paused; _ui.ShowPause(); }
        public void ResumeGame() { if (_state != State.Paused) return; _state = State.Playing; _ui.ShowHud(); }
        public void RestartGame()
        {
            FinalizeRun();
            if (_daily) StartDaily(); else StartGame(_sim.Mode, _sim.Difficulty);
        }
        public void ToMenu() { FinalizeRun(); _ads.HideBanner(); _state = State.Menu; _ui.ShowMenu(); }

        // ── Extra life ───────────────────────────────────────────────────────
        public int ReviveCost() => 5 * (1 << _reviveCount); // 5, 10, 20, 40, …
        public bool TryReviveWithShillings()
        {
            if (_state != State.Dead || _daily) return false;
            if (!SaveData.TrySpendShillings(ReviveCost())) return false;
            DoRevive();
            return true;
        }

        // Rewarded-ad extra life. Grants a free revive only if the ad completes;
        // with the no-op ad service this falls back to a toast.
        public void WatchAdForLife()
        {
            if (_state != State.Dead || _daily) return;
            _ads.ShowRewarded(
                onReward: () => { if (_state == State.Dead) DoRevive(); },
                onUnavailable: () => _ui.ShowToast("No ad available right now."));
        }

        private void DoRevive()
        {
            _reviveCount++;
            _sim.Revive();
            _state = State.Reviving;     // freeze the board for a 3-2-1 countdown
            _reviveCountdown = 3f;
            _ads.HideBanner();
            _ui.ShowHud();
            _ui.ShowCountdown(3);
        }

        // Temporary dev tool: wipe all saved data (name, wallet, skins, achievements,
        // daily attempts, seen-howto flags) and return to a fresh menu.
        public void DevReset()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            _ui.ShowToast("Dev: save data cleared.");
            _state = State.Menu;
            _ui.ShowMenu();
        }

        // ── Wallet / skins (marketplace) ─────────────────────────────────────
        public int Shillings => SaveData.GetShillings();
        public bool TransformActive => _sim != null && _sim.Mode == GameMode.Battle && _sim.TransformActive;
        public PieceType PlayerForm => _sim != null ? _sim.PlayerForm : PieceType.Knight;
        public void CheckNameAvailable(string name, Action<bool> cb) => _lb.CheckNameAvailable(name, cb);

        // On a new day, see whether the player placed top-5 in yesterday's daily
        // challenge; if so, grant the reward and show a congrats popup on the menu.
        // NOTE: client-side award (local wallet) — fine for beta; the authoritative
        // payout should come from a server cron once the wallet moves server-side.
        public void CheckDailyReward()
        {
            if (_dailyRewardChecked) return;
            _dailyRewardChecked = true; // once per session

            // The day of the player's most recent daily run. Checking this (rather
            // than always "yesterday") means a top-5 finish is still announced even
            // if they don't open the app again for several days.
            string day = PlayerPrefs.GetString("kg_daily_last_played", "");
            if (string.IsNullOrEmpty(day)) return;                                   // never played a daily
            if (day == DateTime.Now.ToString("yyyyMMdd")) return;                    // today's board isn't settled yet
            if (PlayerPrefs.GetString("kg_daily_reward_done", "") == day) return;    // already claimed

            string name = SaveData.GetName();
            if (string.IsNullOrEmpty(name)) return;

            _lb.FetchOnline("daily-" + day, rows =>
            {
                // Empty can mean "no board" or a network failure — don't mark done,
                // so it retries on the next launch.
                if (rows == null || rows.Count == 0) return;

                PlayerPrefs.SetString("kg_daily_reward_done", day);
                PlayerPrefs.Save();

                int rank = -1;
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i].name == name) { rank = i; break; }

                if (rank >= 0 && rank < DailyDayRewards.Length)
                {
                    int reward = DailyDayRewards[rank];
                    SaveData.AddShillings(reward);
                    if (_state == State.Menu) _ui.ShowDailyReward(rank + 1, reward, FriendlyDate(day));
                }
            });
        }

        private static string FriendlyDate(string ymd)
        {
            if (string.IsNullOrEmpty(ymd) || ymd.Length != 8) return "";
            string[] mo = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            if (!int.TryParse(ymd.Substring(4, 2), out int m) || m < 1 || m > 12) return "";
            if (!int.TryParse(ymd.Substring(6, 2), out int d)) return "";
            return mo[m - 1] + " " + d;
        }
        public void EquipSkin(string id)
        {
            SaveData.SetSkin(id);
            _lb.SyncEquippedSkin(SaveData.GetName(), id);
            if (_sim != null) _view.SetPlayerForm(_sim.PlayerForm, id);
        }
        public bool BuyShopSkin(SkinInfo s)
        {
            if (s == null) return false;
            if (SaveData.OwnsShopSkin(s.Id)) { EquipSkin(s.Id); return true; }
            if (!SaveData.TrySpendShillings(s.Price)) return false;
            SaveData.AddOwnedSkin(s.Id);
            EquipSkin(s.Id);
            return true;
        }

        private void FinalizeRun()
        {
            if (_runEnded || _state != State.Dead) return;
            _runEnded = true;
            string today = DateTime.Now.ToString("yyyyMMdd");
            string category = _daily
                ? "daily-" + today
                : _sim.Mode.ToString().ToLower() + "-" + _sim.Difficulty.ToString().ToLower();
            _lb.SubmitScore(category, SaveData.GetName(), _sim.Score, SaveData.GetSkin());
            if (_sim.RunShillings > 0) SaveData.AddShillings(_sim.RunShillings);
            // Remember the day of the player's most recent daily run, so its
            // placement reward can be shown on a later launch (even days later).
            if (_daily) { PlayerPrefs.SetString("kg_daily_last_played", today); PlayerPrefs.Save(); }
        }

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

            if (_state == State.Reviving)
            {
                _reviveCountdown -= Time.deltaTime;
                int n = Mathf.CeilToInt(_reviveCountdown);
                if (n <= 0) { _ui.HideCountdown(); _state = State.Playing; }
                else _ui.SetCountdown(n);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_state == State.Playing) PauseGame();
                else if (_state == State.Paused) ResumeGame();
            }
            if (_state == State.Dead && Input.GetKeyDown(KeyCode.R)) RestartGame();

            // Transform wash: a brief one-shot pulse of the piece's colour when a
            // NEW transformation begins. Nightrider re-morphs happen while already
            // transformed, so they don't re-fire (no flashing through colours).
            bool xf = _sim.Mode == GameMode.Battle && _sim.TransformActive;
            if (xf && !_wasTransformed)
            {
                _board.WashColor = UIFactory.Hex(GameConfig.FormColorHex(_sim.PlayerForm));
                _washPulse = 1f;
            }
            _wasTransformed = xf;
            if (_washPulse > 0f)
            {
                _washPulse = Mathf.MoveTowards(_washPulse, 0f, Time.deltaTime / 0.7f);
                _board.WashStrength = 0.30f * Mathf.Sin(Mathf.PI * (1f - _washPulse)); // rise then fall
            }
            else _board.WashStrength = 0f;
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

            // Score is submitted in FinalizeRun (so a revive doesn't double-post).
            int reviveCost = _daily ? -1 : ReviveCost();
            _ui.ShowGameOver(_sim.Score, isTrap, analysis, _sim.RunShillings, reviveCost, SaveData.GetShillings());
            _ads.ShowBanner();
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
