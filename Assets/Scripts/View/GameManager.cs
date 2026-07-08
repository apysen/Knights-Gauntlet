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

        private enum State { Menu, Playing, Paused, Dead, Reviving, Tutorial, Adventure }
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

        // ── Interactive tutorial ─────────────────────────────────────────────
        private GameMode _tutMode; private Difficulty _tutDiff; private bool _tutReplay;
        private int _tutStep;
        private string _lastRunCategory; private int _lastRunScore; private bool _lastRunDaily; private string _lastRunDay;
        private LevelData _advLevel; private int _advMoves, _advGoalCol, _advGoalRow;
        private State _prePause;
        private readonly System.Collections.Generic.List<TutStep> _tut = new System.Collections.Generic.List<TutStep>();
        private class TutStep { public string caption; public bool infoOnly; public System.Action setup; public int targetCol, targetRow; }
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
                Debug.Log("[Knight's Gauntlet] No GameTheme assigned, using a runtime default " +
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
            _sim.OnTrapLevelUp += HandleTrapLevelUp;

            _ach = new AchievementTracker(_sim, OnAchievementUnlocked);
            _ads.Initialize();

            var lgo = new GameObject("Leaderboard"); lgo.transform.SetParent(transform, false);
            _lb = lgo.AddComponent<LeaderboardClient>();
            var sb = SupabaseConfig.Load();
            if (sb != null) { _lb.supabaseUrl = sb.url; _lb.supabaseKey = sb.key; }
            else Debug.Log("[Knight's Gauntlet] No SupabaseConfig in Resources, online leaderboard is off. " +
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
                _ui.ShowToast("No daily attempts left. Resets tomorrow.");
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

        public void PauseGame() { if (_state != State.Playing && _state != State.Adventure) return; _prePause = _state; _state = State.Paused; _ui.ShowPause(_prePause == State.Adventure); }
        public void ExitAdventureToLevelSelect()
        {
            _sim.TutorialEnd();
            _view.ClearCoachMark();
            _ui.SetAdventureHud(false, 0, "", -1);
            _state = State.Menu;
            _ui.ShowLevelSelect();
        }
        public void ResumeGame()
        {
            if (_state != State.Paused) return;
            _state = _prePause;
            _ui.ShowHud();
            if (_prePause == State.Adventure && _advLevel != null) _ui.SetAdventureHud(true, _advMoves, _advLevel.Name, AdvEnemiesLeft());
        }
        public void RestartGame()
        {
            if (_state == State.Adventure || (_state == State.Paused && _prePause == State.Adventure)) { RetryAdventure(); return; }
            FinalizeRun();
            if (_daily) StartDaily(); else StartGame(_sim.Mode, _sim.Difficulty);
        }
        public void ToMenu() { FinalizeRun(); _ads.HideBanner(); _sim.TutorialEnd(); _view.ClearCoachMark(); _ui.SetAdventureHud(false, 0, ""); _state = State.Menu; _ui.ShowMenu(); }

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
            string name = SaveData.GetName();
            bool hasName = !string.IsNullOrEmpty(name);
            if (hasName)
                _lb.SubmitScore(category, name, _sim.Score, SaveData.GetSkin());
            if (_sim.RunShillings > 0) SaveData.AddShillings(_sim.RunShillings);
            // Remember the day of the player's most recent daily run, so its
            // placement reward can be shown on a later launch (even days later).
            if (_daily && hasName) { PlayerPrefs.SetString("kg_daily_last_played", today); PlayerPrefs.Save(); }
            // Remember this run so an anonymous player can claim it (enter a name)
            // from the game-over screen and save it retroactively.
            _lastRunCategory = category; _lastRunScore = _sim.Score; _lastRunDaily = _daily; _lastRunDay = today;
        }

        // Submit the just-finished run under a name the player entered on the
        // game-over screen (they played anonymously, then chose to save).
        public void SubmitLastRun()
        {
            string name = SaveData.GetName();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(_lastRunCategory)) return;
            _lb.SubmitScore(_lastRunCategory, name, _lastRunScore, SaveData.GetSkin());
            if (_lastRunDaily) { PlayerPrefs.SetString("kg_daily_last_played", _lastRunDay); PlayerPrefs.Save(); }
            _lastRunCategory = null; // consumed — don't submit twice
        }

        public void ShowPauseScreen() => _ui.ShowPause();
        public void ShowLeaderboard() => _lbScreen.Open(_sim.Mode, _sim.Difficulty);
        public void ReportBug()
        {
            if (!string.IsNullOrEmpty(bugReportUrl)) Application.OpenURL(bugReportUrl);
            else Debug.Log("[Knight's Gauntlet] Set a Bug Report URL on the GameManager to enable this.");
        }

        // ── Loop ─────────────────────────────────────────────────────────────
        // ── Interactive tutorials ────────────────────────────────────────────
        public void StartTrapTutorial(Difficulty diff, bool replay) => StartTutorial(GameMode.Trap, diff, replay);
        public void StartBattleTutorial(Difficulty diff, bool replay) => StartTutorial(GameMode.Battle, diff, replay);
        private void StartTutorial(GameMode mode, Difficulty diff, bool replay)
        {
            _tutMode = mode; _tutDiff = diff; _tutReplay = replay;
            _sim.TutorialSetup(mode);
            _view.SetPlayerForm(PieceType.Knight, CurrentSkin());
            _view.ClearCoachMark();
            if (mode == GameMode.Trap) BuildTrapTutorial(); else BuildBattleTutorial();
            _state = State.Tutorial;
            _ui.ShowHud();
            _tutStep = -1;
            TutorialAdvance();
        }

        private void BuildTrapTutorial()
        {
            _tut.Clear();
            _tut.Add(new TutStep
            {
                caption = "Tap the glowing square to leap in an L-shape, like a chess knight.",
                setup = () => { _sim.TutorialClear(); _sim.TutorialSetPlayer(2, 6); },
                targetCol = 3, targetRow = 4
            });
            _tut.Add(new TutStep
            {
                caption = "Traps fall in rows. Leap through the gap to keep climbing.",
                setup = () =>
                {
                    _sim.TutorialClear(); _sim.TutorialSetPlayer(2, 6);
                    _sim.TutorialAddTrap(0, 4); _sim.TutorialAddTrap(1, 4);
                    _sim.TutorialAddTrap(2, 4); _sim.TutorialAddTrap(4, 4);
                },
                targetCol = 3, targetRow = 4
            });
            _tut.Add(new TutStep
            {
                caption = "No safe leap forward? Sometimes you jump backward to open a new path.",
                setup = () =>
                {
                    _sim.TutorialClear(); _sim.TutorialSetPlayer(2, 5);
                    _sim.TutorialAddTrap(1, 3); _sim.TutorialAddTrap(3, 3);
                    _sim.TutorialAddTrap(0, 4); _sim.TutorialAddTrap(4, 4);
                },
                targetCol = 0, targetRow = 6
            });
            _tut.Add(new TutStep
            {
                caption = "That's it! Thread the traps and climb as high as you can. Good luck!",
                infoOnly = true
            });
        }

        private void BuildBattleTutorial()
        {
            _tut.Clear();
            _tut.Add(new TutStep
            {
                caption = "Tap the enemy piece to leap onto it and capture it for points.",
                setup = () =>
                {
                    _sim.TutorialClear(); _sim.TutorialSetPlayer(2, 6);
                    _sim.TutorialAddPiece(PieceType.Rook, 3, 4);
                },
                targetCol = 3, targetRow = 4
            });
            _tut.Add(new TutStep
            {
                caption = "Capture again on your very next move to build a combo. The pips below you fill toward a transform, and each capture is worth more.",
                setup = () =>
                {
                    _sim.TutorialClear(); _sim.TutorialSetPlayer(3, 4);
                    _sim.TutorialAddPiece(PieceType.Bishop, 2, 2);
                },
                targetCol = 2, targetRow = 2
            });
            _tut.Add(new TutStep
            {
                caption = "Not every move is a capture. Leap to the empty glowing square. Landing on nothing counts as a miss.",
                setup = () =>
                {
                    _sim.TutorialClear(); _sim.TutorialSetPlayer(2, 2);
                    // no piece on the target: this move is a deliberate miss
                },
                targetCol = 4, targetRow = 3
            });
            _tut.Add(new TutStep
            {
                caption = "Your pips survived, that was a free miss. But miss too often and your combo and pips reset to zero. So capture this piece for three in a row and transform!",
                setup = () =>
                {
                    _sim.TutorialClear(); _sim.TutorialSetPlayer(4, 3);
                    _sim.TutorialAddPiece(PieceType.Queen, 3, 1);
                },
                targetCol = 3, targetRow = 1
            });
            _tut.Add(new TutStep
            {
                caption = "You transformed! You stay this piece for 5 seconds, and every capture refills that timer, so keep hunting. Use your new range to strike the enemy.",
                setup = () =>
                {
                    _sim.TutorialClear(); _sim.TutorialSetPlayer(3, 1);
                    _sim.TutorialAddPiece(PieceType.Rook, 3, 5);
                },
                targetCol = 3, targetRow = 5
            });
            _tut.Add(new TutStep
            {
                caption = "You're ready! Capture, chain combos, and transform, while avoiding misses. Keep an eye out for red outlines that show where enemies are about to strike. Good luck!",
                infoOnly = true
            });
        }

        private void TutorialAdvance()
        {
            _tutStep++;
            if (_tutStep >= _tut.Count) { FinishTutorial(); return; }
            var s = _tut[_tutStep];
            s.setup?.Invoke();
            if (s.infoOnly)
            {
                _sim.TutorialUnlock();
                _view.ClearCoachMark();
                _ui.ShowTutorial(s.caption, true);
            }
            else
            {
                _sim.TutorialLock(s.targetCol, s.targetRow);
                _view.SetCoachMark(s.targetCol, s.targetRow);
                _ui.ShowTutorial(s.caption, false);
            }
        }

        private void HandleTutorialInput()
        {
            if (OverUI()) return;
            if (GetTapDown(out Vector2 screenPos))
            {
                Vector3 world = cam.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                var (col, simY) = _board.WorldToCell(world);
                var result = _sim.TryTap(col, simY);
                if (!result.Moved) return;
                var s = _tut[_tutStep];
                if (!s.infoOnly && _sim.PlayerCol == s.targetCol && Mathf.RoundToInt(_sim.PlayerY) == s.targetRow)
                    TutorialAdvance();
            }
        }

        public void TutorialContinue() { if (_state == State.Tutorial) TutorialAdvance(); }
        public void TutorialSkip() { if (_state == State.Tutorial) FinishTutorial(); }

        private void FinishTutorial()
        {
            PlayerPrefs.SetInt(_tutMode == GameMode.Trap ? "kg_seen_trap" : "kg_seen_battle", 1);
            PlayerPrefs.Save();
            _sim.TutorialEnd();
            _view.ClearCoachMark();
            _ui.HideTutorial();
            if (_tutReplay) { _state = State.Menu; _ui.ShowMenu(); }
            else StartGame(_tutMode, _tutDiff);
        }

        private void HandleTrapLevelUp(int level)
        {
            // Brief gold screen flash (reuses the battle wash pulse) + a message.
            _board.WashColor = UIFactory.Hex("#F0C040");
            _washPulse = 1f;
            _ui.ShowToast("Level " + level + "! Speeding up");
        }

        // ── Adventure mode (stationary puzzles, or scrolling survival) ────────
        private bool _advScrolling, _advTall, _advManualScroll;

        public void StartAdventureLevel(LevelData lvl)
        {
            _advLevel = lvl;
            _advMoves = 0;
            _advScrolling = lvl.ScrollSpeed > 0f;
            _advTall = lvl.MapRows > GameConfig.Rows;
            _advManualScroll = false;

            if (_advScrolling)
            {
                // Scrolling survival: deterministic trap field at a constant, per-level speed.
                _sim.Start(GameMode.Trap, Difficulty.Hard, seed: lvl.Seed, mediumSpeed: false, pickups: false);
                _sim.Adventure = true;
                _sim.AdventureSetSpeed(lvl.ScrollSpeed);
                _view.ClearCoachMark();
            }
            else
            {
                _advGoalCol = lvl.GoalCol; _advGoalRow = lvl.GoalRow;
                _sim.TutorialSetup(GameMode.Trap); // frozen static board, real knight moves
                _sim.TutorialClear();
                if (_advTall) _sim.MapRows = lvl.MapRows;
                if (lvl.Traps != null)
                    foreach (var t in lvl.Traps) _sim.TutorialAddTrap(t.Col, t.Row);
                if (lvl.Enemies != null)
                    foreach (var e in lvl.Enemies) _sim.AdventureAddEnemy(e.Type, e.Col, e.Row, e.OffCol, e.OffRow);
                _sim.TutorialSetPlayer(lvl.StartCol, lvl.StartRow);
                if (_advTall)
                {
                    // Center the follow camera on the start, then derive screen positions.
                    _sim.AdventureSetScroll(Mathf.Clamp(GameConfig.Rows * 0.5f - lvl.StartRow, GameConfig.Rows - lvl.MapRows, 0f));
                    _sim.AdventureFollowDerive();
                    _view.SetCoachMark(_advGoalCol, _advGoalRow + _sim.ScrollCells);
                }
                else if (lvl.Objective == Objective.Reach || lvl.Objective == Objective.Combined)
                    _view.SetCoachMark(_advGoalCol, _advGoalRow); // pulsing goal marker
                else
                    _view.ClearCoachMark();
            }

            _view.SetPlayerForm(PieceType.Knight, CurrentSkin());
            _ads.HideBanner();
            _state = State.Adventure;
            _ui.ShowHud();
            _ui.SetAdventureHud(true, 0, lvl.Name, AdvEnemiesLeft());
            if (_advTall) _ui.ShowTallScroll(true, (float)GameConfig.Rows / lvl.MapRows);
        }


        private int AdvEnemiesLeft()
            => (_advScrolling || _advLevel.Objective == Objective.Reach) ? -1 : _sim.AdvRemaining;

        private void HandleAdventureInput()
        {
            if (OverUI()) return;
            if (GetTapDown(out Vector2 screenPos))
            {
                Vector3 world = cam.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                var (col, simY) = _board.WorldToCell(world);
                var result = _sim.TryTap(col, simY);
                if (!result.Moved) return;
                _advMoves++;

                if (_advScrolling) { _ui.SetAdventureHud(true, _advMoves, _advLevel.Name, -1); return; }
                if (_advTall) { _advManualScroll = false; _ui.SetAdventureHud(true, _advMoves, _advLevel.Name, -1); return; } // a move resumes following

                bool hasEnemies = _advLevel.Objective != Objective.Reach;
                if (hasEnemies && _sim.AdventureAfterMove()) { AdventureFail(); return; } // an enemy caught you

                _ui.SetAdventureHud(true, _advMoves, _advLevel.Name, AdvEnemiesLeft());

                bool atGoal = _sim.PlayerCol == _advGoalCol && Mathf.RoundToInt(_sim.PlayerY) == _advGoalRow;
                bool cleared = _sim.AdvAllCaptured();
                bool won =
                    _advLevel.Objective == Objective.Reach ? atGoal :
                    _advLevel.Objective == Objective.CaptureAll ? cleared :
                    (cleared && atGoal); // Combined
                if (won) AdventureWin();
            }
        }

        private void AdventureWin()
        {
            int stars = _advMoves <= _advLevel.ThreeStar ? 3 : (_advMoves <= _advLevel.TwoStar ? 2 : 1);
            int prevStars = SaveData.GetStars(_advLevel.Id);
            SaveData.SetStars(_advLevel.Id, stars);
            if (stars == 3 && prevStars < 3) SaveData.AddShillings(500); // first 3-star clear of this level
            _sim.TutorialEnd(); _sim.Adventure = false;
            _view.ClearCoachMark();
            _ui.SetAdventureHud(false, 0, "", -1);
            _state = State.Menu;
            _ui.ShowAdventureResult(_advLevel, _advMoves, stars);
        }

        private void AdventureFail()
        {
            _sim.TutorialEnd(); _sim.Adventure = false;
            _view.ClearCoachMark();
            _ui.SetAdventureHud(false, 0, "", -1);
            _state = State.Menu;
            _ui.ShowAdventureFail(_advLevel);
        }

        public void RetryAdventure() { if (_advLevel != null) StartAdventureLevel(_advLevel); }

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

            if (_state == State.Tutorial) HandleTutorialInput();
            if (_state == State.Adventure)
            {
                if (_advScrolling)
                {
                    _sim.Tick(Time.deltaTime); // auto-scroller
                    HandleAdventureInput();
                    float prog = Mathf.Clamp01(_sim.ScrollCells / _advLevel.FinishCells);
                    _ui.SetAdventureHud(true, _advMoves, _advLevel.Name + "   " + Mathf.RoundToInt(prog * 100) + "%", -1);
                    if (!_sim.Alive) AdventureFail();
                    else if (_sim.ScrollCells >= _advLevel.FinishCells) AdventureWin();
                }
                else if (_advTall)
                {
                    float span = Mathf.Max(1f, _advLevel.MapRows - GameConfig.Rows);
                    if (_ui.ConsumeTallScrollInput()) _advManualScroll = true; // user grabbed the bar
                    if (_advManualScroll)
                    {
                        _sim.AdventureSetScroll(-_ui.TallScroll * span); // stays where you leave it
                    }
                    else
                    {
                        int pr = Mathf.RoundToInt(_sim.PlayerY - _sim.ScrollCells);
                        float target = Mathf.Clamp(GameConfig.Rows * 0.5f - pr, GameConfig.Rows - _advLevel.MapRows, 0f);
                        _sim.AdventureSetScroll(Mathf.Lerp(_sim.ScrollCells, target, 1f - Mathf.Exp(-9f * Time.deltaTime)));
                        _ui.SetTallScroll(-_sim.ScrollCells / span); // reflect follow position in the bar
                    }
                    _sim.AdventureFollowDerive();
                    _view.SetCoachMark(_advGoalCol, _advGoalRow + _sim.ScrollCells);
                    HandleAdventureInput();
                    if (_sim.PlayerCol == _advGoalCol && Mathf.RoundToInt(_sim.PlayerY - _sim.ScrollCells) == _advGoalRow)
                        AdventureWin();
                }
                else HandleAdventureInput();
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
            bool showHints = _sim.Difficulty != Difficulty.Grandmaster && (_state == State.Playing || _state == State.Adventure);
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
                _ui.ShowToast(def.Name + " unlocked! " + def.SkinName + " skin earned");
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
