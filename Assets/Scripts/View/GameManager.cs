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
        public string bugReportEmail = "apysen02@gmail.com";

        private enum State { Menu, Playing, Paused, Dead, Reviving, Tutorial, Adventure }
        private State _state = State.Menu;

        private bool _daily;
        private int _reviveCount;
        private bool _adLifeUsed;    // one free ad revive per run
        private float _reviveCountdown;
        private bool _runEnded;

        private GameSimulation _sim;
        private BoardView _board;
        private GameView _view;
        private UIManager _ui;
        private LeaderboardClient _lb;
        private LeaderboardScreen _lbScreen;
        private AchievementTracker _ach;
        // Ads via LevelPlay. Values are from the LevelPlay dashboard (App Key per platform,
        // plus the Banner and Rewarded Ad Unit IDs per platform). If a key is blank, init
        // fails gracefully and no ads show. Swap to `new NoOpAdService()` to disable ads.
        // Ads are LIVE. Register your test devices (GAID/IDFA) in the LevelPlay dashboard so
        // you only ever serve test ads to yourself. See LevelPlayAdService for the privacy
        // (COPPA/GDPR) and iOS ATT setup.
        private IAdService _ads = new LevelPlayAdService(
            androidAppKey:     "271f04335",
            iosAppKey:         "271f07cbd",
            androidBannerId:   "5c04wh7w59j3rvkf",
            iosBannerId:       "a084z73cm679qdqn",
            androidRewardedId: "kgkds0l2ryp0s481",
            iosRewardedId:     "x0cmcsjtmgf87urv");
        // private IAdService _ads = new NoOpAdService();

        // In-app purchases (consumable shilling bundles). Same pattern as ads: the game
        // talks only to IIapService. LIVE via UnityIapService against the real store
        // products (ids in IapCatalog must match App Store Connect / Google Play exactly).
        // Swap to StubIapService to grant instantly with no billing for local testing.
        private readonly IIapService _iap = new UnityIapService();
        // private readonly IIapService _iap = new StubIapService();
        public IIapService Iap => _iap;
        private bool _dailyRewardChecked;
        private bool _wasTransformed;   // for firing the wash pulse only on a NEW transform
        private bool _transformLooping; // whether the transform sting loop is currently playing
        private float _washPulse;       // 1..0 envelope of the current transform pulse

        // Active-powerup timers (seconds left), Battle/Trap only. Chronokinesis halves the
        // sim tick; Divine Intervention feeds the best-move hint to the view.
        private float _chronoRemaining, _divineRemaining;
        private float _chronoRewindRate, _chronoRewindRemaining; // Chronokinesis: gradual board rewind (cells/sec, seconds left)
        public float ChronoRemaining => _chronoRemaining;
        public float DivineRemaining => _divineRemaining;

        // Divine Path (adventure only): the solved optimal route, shown until the level ends.
        private bool _divinePathActive;
        private System.Collections.Generic.List<Int2> _divinePath;
        // Offered on any solvable adventure level (incl. transform + the queen boss). Only
        // the auto-scroller survival levels have no discrete route to solve.
        public bool PathUsable => _state == State.Adventure && _advLevel != null && _advLayout != null && !_advScrolling;
        public bool DivinePathActive => _divinePathActive;

        // ── Interactive tutorial ─────────────────────────────────────────────
        private GameMode _tutMode; private Difficulty _tutDiff; private bool _tutReplay;
        private int _tutStep;
        private string _lastRunCategory; private int _lastRunScore; private bool _lastRunDaily; private string _lastRunDay;
        private LevelData _advLevel;  // the selected level (the boss parent, for a boss)
        private LevelData _advLayout; // the layout actually on the board (a boss phase, or == _advLevel)
        private int _advMoves, _advGoalCol, _advGoalRow;
        private State _prePause;
        private readonly System.Collections.Generic.List<TutStep> _tut = new System.Collections.Generic.List<TutStep>();
        private class TutStep { public string caption; public bool infoOnly; public System.Action setup; public int targetCol, targetRow; }

        public int Score => _sim != null ? _sim.Score : 0;

        private static readonly Color PopupGain = new Color32(0x9B, 0xE8, 0xA0, 0xFF);
        private static readonly Color PopupCombo = new Color32(0xFF, 0xD0, 0x60, 0xFF);
        private static readonly Color PopupShield = new Color32(0xFF, 0x9A, 0x5C, 0xFF);

        private void Start()
        {
            // Target a smooth 60 fps. Without this, mobile falls back to the platform
            // default (frequently an uneven 30), which reads as stutter. vSync is
            // disabled so targetFrameRate is actually honoured.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<GameTheme>();
                Debug.Log("[Knight's Gauntlet] No GameTheme assigned, using a runtime default " +
                          "(glyph graphics). Create one via Create > Knight's Gauntlet > Game Theme to add art.");
            }

            // Use the Thaleah pixel font for all UI/HUD/popup text (any slot the
            // theme leaves empty). The chess-glyph font is deliberately left alone,
            // Thaleah has no chess glyphs.
            var pixelFont = UIFactory.PixelFont();
            if (pixelFont != null)
            {
                if (theme.uiFont == null) theme.uiFont = pixelFont;
                if (theme.popupFont == null) theme.popupFont = pixelFont;
                if (theme.hudFont == null) theme.hudFont = pixelFont;
            }

            // Pixel chess-piece font for every chess glyph (board pieces + UI). This
            // OVERRIDES the theme's glyphFont (GameTheme.asset ships DejaVu, which also
            // has chess glyphs, hence pieces looked unchanged until now). PixelChess
            // carries a DejaVu fallback so the trap "✕" and any non-piece glyphs show.
            var chessFont = UIFactory.ChessFont();
            if (chessFont != null) theme.glyphFont = chessFont;

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

            var sfxGo = new GameObject("Sfx"); sfxGo.transform.SetParent(transform, false);
            sfxGo.AddComponent<SfxManager>();

            _sim = new GameSimulation();
            _sim.OnFormChanged += form =>
            {
                // Visual only. The transform sting loop is driven by the transformed-state
                // edge in Update, so a nightrider's rapid mimic re-morphs (which each fire
                // OnFormChanged) don't retrigger the sound.
                _view.SetPlayerForm(form, CurrentSkin());
            };
            _sim.OnShielded += () =>
            {
                _view.SpawnPopup(_sim.PlayerCol, _sim.PlayerY - 0.3f, "shielded!", PopupShield);
                SfxManager.Play(Sfx.ShieldBreak);
            };
            _sim.OnTransformExpired += () => SfxManager.Play(Sfx.Untransform);
            _sim.OnPickupCollected += () => SfxManager.Play(Sfx.ShillingCollect);
            _sim.OnTrapLevelUp += HandleTrapLevelUp;

            _ach = new AchievementTracker(_sim, OnAchievementUnlocked);
            _ads.Initialize();
            _iap.Initialize();

            var lgo = new GameObject("Leaderboard"); lgo.transform.SetParent(transform, false);
            _lb = lgo.AddComponent<LeaderboardClient>();
            var sb = SupabaseConfig.Load();
            if (sb != null)
            {
                _lb.supabaseUrl = sb.url; _lb.supabaseKey = sb.key;
                _lb.OnNameRejected += OnNameRejected;
                // Logged positively so that, on a device, an absent "no config" warning is not
                // mistaken for a healthy load. Only the host is logged - never the key.
                Debug.Log("[Knight's Gauntlet] Supabase configured: " + sb.url);
            }
            else Debug.Log("[Knight's Gauntlet] No SupabaseConfig in Resources, online leaderboard is off. " +
                           "Create Assets/Resources/SupabaseConfig.asset to enable it.");

            var lsgo = new GameObject("LeaderboardUI"); lsgo.transform.SetParent(transform, false);
            _lbScreen = lsgo.AddComponent<LeaderboardScreen>();
            _lbScreen.Init(theme, _lb);

            var ugo = new GameObject("UI"); ugo.transform.SetParent(transform, false);
            _ui = ugo.AddComponent<UIManager>();
            _ui.Init(this, theme);

            _board.FitCamera(cam, _ui.HudTopInsetFrac(), _ui.HudBottomInsetFrac());

            // Reflect the player's current skin on their existing board rows.
            _lb.SyncEquippedSkin(SaveData.GetName(), SaveData.GetSkin());

            CheckDailyReward();
        }

        // ── Public API (called by UIManager) ─────────────────────────────────
        public void StartGame(GameMode mode, Difficulty difficulty)
        {
            _daily = false; _reviveCount = 0; _adLifeUsed = false; _runEnded = false;
            ResetPowerups();
            _ads.HideBanner();
            SfxManager.StopMenuMusic();
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
            _daily = true; _reviveCount = 0; _adLifeUsed = false; _runEnded = false;
            ResetPowerups();
            _ads.HideBanner();
            SfxManager.StopMenuMusic();
            _sim.Start(GameMode.Trap, Difficulty.Grandmaster, SaveData.DailySeed(), mediumSpeed: true, pickups: false);
            _ach.Reset();
            _view.SetPlayerForm(_sim.PlayerForm, CurrentSkin());
            _state = State.Playing;
            _ui.ShowHud(showBadge: false); // daily hides the "TRAP · …" mode badge
        }

        public void PauseGame() { if (_state != State.Playing && _state != State.Adventure) return; _prePause = _state; _state = State.Paused; _ui.ShowPause(_prePause == State.Adventure); }
        public void ExitAdventureToLevelSelect()
        {
            _sim.TutorialEnd();
            _view.ClearCoachMark();
            _ui.SetAdventureHud(false, 0, "", -1); _ui.SetBossHud(false, 0, 0, false);
            _state = State.Menu;
            _ui.ShowLevelSelect();
        }
        public void ResumeGame()
        {
            if (_state != State.Paused) return;
            _state = _prePause;
            _ui.ShowHud(showBadge: !_daily);
            if (_prePause == State.Adventure && _advLevel != null) _ui.SetAdventureHud(true, _advMoves, _advLevel.Name, AdvEnemiesLeft());
            // ShowHud() hides the tall-map scrollbar; restore it when resuming a tall level.
            if (_prePause == State.Adventure && _advTall && _advLevel != null)
                _ui.ShowTallScroll(true, (float)GameConfig.Rows / _advLayout.MapRows);
        }
        public void RestartGame()
        {
            if (_state == State.Adventure || (_state == State.Paused && _prePause == State.Adventure)) { RetryAdventure(); return; }
            FinalizeRun();
            if (_daily) StartDaily(); else StartGame(_sim.Mode, _sim.Difficulty);
        }
        public void ToMenu() { FinalizeRun(); _ads.HideBanner(); _sim.TutorialEnd(); _view.ClearCoachMark(); _ui.SetAdventureHud(false, 0, ""); _ui.SetBossHud(false, 0, 0, false); _state = State.Menu; _ui.ShowMenu(); }

        // ── Extra life ───────────────────────────────────────────────────────
        public int ReviveCost() => 250 * (1 << _reviveCount); // 250, 500, 1000, 2000, …
        public bool TryReviveWithShillings()
        {
            if (_state != State.Dead || _daily) return false;
            if (!SaveData.TrySpendShillings(ReviveCost())) return false;
            DoRevive();
            return true;
        }

        // Whether the game-over screen should offer the free ad life: one per run,
        // never in the daily (no revives there), and only when an ad is actually loaded.
        public bool CanOfferAdLife => !_daily && !_adLifeUsed && _ads.IsRewardedReady;

        // Rewarded-ad extra life. Grants a free revive only if the ad completes;
        // with the no-op ad service this falls back to a toast.
        public void WatchAdForLife()
        {
            if (_state != State.Dead || _daily || _adLifeUsed) return;
            _ads.ShowRewarded(
                onReward: () =>
                {
                    _adLifeUsed = true;      // burn the free life only on a completed ad
                    if (_state == State.Dead) DoRevive();
                },
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

        // ── Wallet / skins (marketplace) ─────────────────────────────────────
        public int Shillings => SaveData.GetShillings();
        public bool TransformActive => _sim != null && _sim.Mode == GameMode.Battle && _sim.TransformActive;
        public PieceType PlayerForm => _sim != null ? _sim.PlayerForm : PieceType.Knight;
        public void CheckNameAvailable(string name, Action<bool> cb) => _lb.CheckNameAvailable(name, cb);

        // The server refused this name: it collides with an existing one that differs only in
        // case, so it can never post a score. Names are normally permanent, but keeping this
        // one would leave the player silently unable to reach the leaderboard forever - so we
        // release it and let them choose again. Their local scores are untouched.
        private void OnNameRejected(string name)
        {
            if (SaveData.GetName() != name) return; // stale reply from a name they already dropped

            Debug.LogWarning("[Knight's Gauntlet] Name '" + name + "' is unusable (case conflict); releasing it.");
            SaveData.SetName("");
            _ui?.ShowNameRejected(name);
        }

        // On a new day, see whether the player placed top-5 in yesterday's daily
        // challenge; if so, grant the reward and show a congrats popup on the menu.
        // NOTE: client-side award (local wallet). Fine for beta, but the authoritative
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
                // Empty can mean "no board" or a network failure, so don't mark done,
                // so it retries on the next launch.
                if (rows == null || rows.Count == 0) return;

                PlayerPrefs.SetString("kg_daily_reward_done", day);
                PlayerPrefs.Save();

                int rank = -1;
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i].name == name) { rank = i; break; }

                int place = rank + 1;
                if (rank >= 0 && DailyPrizes.Pays(place))
                {
                    var prize = DailyPrizes.For(place);
                    SaveData.AddShillings(prize.Shillings);
                    if (prize.Chrono > 0) SaveData.AddPowerup(Powerups.Chronokinesis.Key, prize.Chrono);
                    if (prize.Divine > 0) SaveData.AddPowerup(Powerups.DivineIntervention.Key, prize.Divine);
                    if (_state == State.Menu) _ui.ShowDailyReward(place, FriendlyDate(day));
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
        public void EquipVfx(string id) => SaveData.SetVfx(id);

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
            _lastRunCategory = null; // consumed, don't submit twice
        }

        public void ShowPauseScreen() => _ui.ShowPause();
        public void ShowLeaderboard() => _lbScreen.Open(_sim.Mode, _sim.Difficulty);

        // Banner visibility, driven by the UI layer so the banner appears on the menu
        // and other non-gameplay screens (leaderboard, level select, settings, shop,
        // customize, achievements, tutorials, adventure result/fail, game
        // over) and is hidden during actual play. UIManager.HideAll turns it off; each
        // non-gameplay Show* turns it back on. NoOp ad service makes this a no-op.
        public void SetBanner(bool show) { if (show) _ads.ShowBanner(); else _ads.HideBanner(); }
        public void LaunchAdTestSuite() => _ads.LaunchTestSuite();

        // ── Powerups (Battle + Trap only) ────────────────────────────────────
        // Spend one from inventory and start its effect. Returns false (spending nothing)
        // if not in play, already active, or the player owns none.
        public bool PowerupsUsable => _state == State.Playing && !_daily && _sim != null && (_sim.Mode == GameMode.Battle || _sim.Mode == GameMode.Trap);
        private bool PowerupsAllowed => PowerupsUsable;
        public float PowerupRemaining(PowerupId id) => id == PowerupId.Chronokinesis ? _chronoRemaining : _divineRemaining;

        public bool ActivatePowerup(PowerupId id)
        {
            if (!PowerupsAllowed) return false;
            if (id == PowerupId.Chronokinesis)
            {
                if (_chronoRemaining > 0f) return false;
                if (!SaveData.TrySpendPowerup(Powerups.Chronokinesis.Key)) return false;
                _chronoRemaining = Powerups.Chronokinesis.Duration;
                // Also rewind the board toward a safe near-top row so there's room to retreat,
                // spread smoothly over the first ~2 seconds (see the Playing tick in Update).
                const float chronoTargetRow = 3f, rewindSeconds = 2f;
                float rewind = _sim.PlayerY - chronoTargetRow;
                if (rewind > 0f) { _chronoRewindRate = rewind / rewindSeconds; _chronoRewindRemaining = rewindSeconds; }
                return true;
            }
            if (_divineRemaining > 0f) return false;
            if (!SaveData.TrySpendPowerup(Powerups.DivineIntervention.Key)) return false;
            _divineRemaining = Powerups.DivineIntervention.Duration;
            return true;
        }

        // True while the effect is running (HUD shows a countdown / disabled button).
        public bool PowerupActive(PowerupId id)
            => id == PowerupId.Chronokinesis ? _chronoRemaining > 0f : _divineRemaining > 0f;

        private void ResetPowerups() { _chronoRemaining = 0f; _divineRemaining = 0f; _chronoRewindRate = 0f; _chronoRewindRemaining = 0f; }

        // ── Divine Path (adventure): reveal the level's optimal route ────────
        // Solves from the CURRENT position; refunds (doesn't charge) if there's no route
        // it can compute (boss / transform levels, or a genuinely stuck state).
        public bool ActivateDivinePath()
        {
            if (_state != State.Adventure || _divinePathActive) return false;
            if (Powerups.DivinePath.Owned <= 0) return false;
            var path = ComputeDivinePath();
            if (path == null) { _ui.ShowToast("No route available for this level."); return false; }
            if (!SaveData.TrySpendPowerup(Powerups.DivinePath.Key)) return false;
            _divinePathActive = true; _divinePath = path;
            return true;
        }

        // The live oscillation phase (all bobbing enemies share it). NOT _advMoves % 2, since a
        // boss's phases reset their enemies to phase 0 while _advMoves keeps accumulating,
        // so inferring it from the move count is off by one and can path you into an enemy.
        private int CurrentAdvPhase()
        {
            var adv = _sim.AdvEnemies;
            for (int i = 0; i < adv.Count; i++)
                if (adv[i].Alive && !adv[i].Hunter && adv[i].Moves) return adv[i].Phase;
            return 0; // no oscillators -> phase is irrelevant
        }

        // BFS the optimal route for the current adventure state (see AdventureSolver).
        private System.Collections.Generic.List<Int2> ComputeDivinePath()
        {
            if (_advLevel == null || _advLayout == null || _advScrolling) return null;
            int startCol = _sim.PlayerCol;
            int startRow = Mathf.RoundToInt(_sim.PlayerY - _sim.ScrollCells);
            int maxRow = _sim.MapRows > 0 ? _sim.MapRows - 1 : GameConfig.Rows - 1;
            int phase = CurrentAdvPhase();
            if (_advLevel.IsBoss())
            {
                // Solve just the current phase; the final phase must also reach the flag.
                bool requireGoal = _bossPhase == _advLevel.Phases.Length - 1;
                return AdventureSolver.SolveBoss(startCol, startRow, phase, _sim.AdvEnemies, _sim.AdvTrapAt,
                                                 maxRow, requireGoal, _advGoalCol, _advGoalRow, _sim.Cracked);
            }
            bool hasGoal = _advLayout.Objective == Objective.Reach || _advLayout.Objective == Objective.Combined;
            // _sim.Cracked holds only the tiles still intact (collapsed ones have already
            // become traps and left the list), so the solver plans from the real board.
            return AdventureSolver.Solve(startCol, startRow, phase, _sim.AdvEnemies, _sim.AdvTrapAt,
                                         maxRow, hasGoal, _advGoalCol, _advGoalRow, _advLayout.Transform,
                                         _sim.PlayerForm, _sim.AdvPips, _sim.Cracked);
        }

        // Boss paths model the queen's chase off the exact move sequence, so we solve once
        // (not per move) and just drop cells the player has already stepped onto.
        private void TrimDivinePath()
        {
            if (_divinePath == null || _divinePath.Count == 0) return;
            int pc = _sim.PlayerCol, pr = Mathf.RoundToInt(_sim.PlayerY - _sim.ScrollCells);
            if (_divinePath[0].Col == pc && _divinePath[0].Row == pr) _divinePath.RemoveAt(0);
        }
        // Sends the player's typed report to the Supabase `bug_reports` table (silent,
        // no mail app). App version, device, and the player's name ride along to help
        // debugging. onOk/onFail let the UI toast the result. If Supabase isn't set up,
        // we fall back to a mailto so the report still has somewhere to go.
        public void SendBugReport(string body, Action onOk = null, Action onFail = null)
        {
            if (_lb != null && _lb.CanSubmitBug)
            {
                _lb.SubmitBugReport(body, SaveData.GetName(), onOk, onFail);
                return;
            }

            // No Supabase configured: fall back to the mail app if an address is set.
            if (!string.IsNullOrWhiteSpace(bugReportEmail))
            {
                string footer = "\n\n---\nApp version: " + Application.version +
                                "\nDevice: " + SystemInfo.deviceModel +
                                "\nOS: " + SystemInfo.operatingSystem;
                string subject = System.Uri.EscapeDataString("Knight's Gauntlet Bug Report");
                string bodyEnc = System.Uri.EscapeDataString((body ?? "") + footer);
                Application.OpenURL("mailto:" + bugReportEmail + "?subject=" + subject + "&body=" + bodyEnc);
                onOk?.Invoke();
                return;
            }
            onFail?.Invoke();
        }

        // ── Loop ─────────────────────────────────────────────────────────────
        // ── Interactive tutorials ────────────────────────────────────────────
        public void StartTrapTutorial(Difficulty diff, bool replay) => StartTutorial(GameMode.Trap, diff, replay);
        public void StartBattleTutorial(Difficulty diff, bool replay) => StartTutorial(GameMode.Battle, diff, replay);
        private void StartTutorial(GameMode mode, Difficulty diff, bool replay)
        {
            SfxManager.StopMenuMusic();
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
        private int _lastAdvPct = -1, _lastAdvHudMoves = -1; // gate the scrolling HUD rebuild
        private int _bossPhase, _queenHealth; // queen-boss encounters (see LevelData.Phases)
        private bool _bossQueenDead;          // final queen down: now reach the flag to finish

        public void StartAdventureLevel(LevelData lvl)
        {
            SfxManager.StopMenuMusic();
            _advLevel = lvl;
            _advMoves = 0;
            _bossQueenDead = false;
            if (lvl.IsBoss())
            {
                _bossPhase = 0;
                _queenHealth = lvl.Phases.Length; // one HP per phase
                LoadAdventureLayout(lvl.Phases[0]);
            }
            else
            {
                _bossPhase = 0; _queenHealth = 0;
                LoadAdventureLayout(lvl);
            }
        }

        // Sets up the board from one layout (a stand-alone level, or one boss phase). Does
        // NOT reset _advMoves (so a boss's phases share one move total) or the boss state.
        private void LoadAdventureLayout(LevelData lvl)
        {
            _advLayout = lvl;
            _divinePathActive = false; _divinePath = null;
            _advScrolling = lvl.ScrollSpeed > 0f;
            _advTall = lvl.MapRows > GameConfig.Rows;
            _advManualScroll = false;
            _lastAdvPct = -1; _lastAdvHudMoves = -1;

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
                _sim.AdvTransform = lvl.Transform; // transformation world: captures change form
                if (_advTall) _sim.MapRows = lvl.MapRows;
                if (lvl.Traps != null)
                    foreach (var t in lvl.Traps) _sim.TutorialAddTrap(t.Col, t.Row);
                if (lvl.Cracked != null)
                    foreach (var cc in lvl.Cracked) _sim.AdventureAddCracked(cc.Col, cc.Row);
                if (lvl.Enemies != null)
                    foreach (var e in lvl.Enemies) _sim.AdventureAddEnemy(e.Type, e.Col, e.Row, e.OffCol, e.OffRow, e.Hunter);
                _sim.TutorialSetPlayer(lvl.StartCol, lvl.StartRow);
                bool hasGoal = lvl.Objective == Objective.Reach || lvl.Objective == Objective.Combined;
                if (_advTall)
                {
                    // Center the follow camera on the start, then derive screen positions.
                    _sim.AdventureSetScroll(Mathf.Clamp(GameConfig.Rows * 0.5f - lvl.StartRow, GameConfig.Rows - lvl.MapRows, 0f));
                    _sim.AdventureFollowDerive();
                    if (hasGoal) _view.SetGoalFlag(_advGoalCol, _advGoalRow + _sim.ScrollCells);
                    else _view.ClearCoachMark();
                }
                else if (hasGoal)
                    _view.SetGoalFlag(_advGoalCol, _advGoalRow); // animated goal flag
                else
                    _view.ClearCoachMark();
            }

            _view.SetPlayerForm(PieceType.Knight, CurrentSkin());
            _ads.HideBanner();
            _state = State.Adventure;
            _ui.ShowHud();
            _ui.SetAdventureHud(true, _advMoves, _advLevel.Name, AdvEnemiesLeft());
            _ui.SetBossHud(_advLevel.IsBoss(), _queenHealth, _advLevel.IsBoss() ? _advLevel.Phases.Length : 0, _sim.QueenShielded);
            if (_advTall) _ui.ShowTallScroll(true, (float)GameConfig.Rows / lvl.MapRows);
            else _ui.ShowTallScroll(false, 0f);
        }


        private int AdvEnemiesLeft()
            => (_advScrolling || _bossQueenDead || _advLevel.Objective == Objective.Reach) ? -1 : _sim.AdvRemaining;

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

                if (_advScrolling) { SfxManager.Play(Sfx.PieceMove); _ui.SetAdventureHud(true, _advMoves, _advLevel.Name, -1); return; }
                if (_advTall) _advManualScroll = false; // a move resumes following

                bool hasEnemies = _advLevel.Objective != Objective.Reach;
                int enemiesBefore = _sim.AdvRemaining;
                if (hasEnemies && _sim.AdventureAfterMove()) { AdventureFail(); return; } // an enemy caught you
                // A move that removed an enemy is a capture; otherwise a plain move.
                if (hasEnemies && _sim.AdvRemaining < enemiesBefore) SfxManager.Play(Sfx.PieceCapture);
                else SfxManager.Play(Sfx.PieceMove);

                _ui.SetAdventureHud(true, _advMoves, _advLevel.Name, AdvEnemiesLeft());

                // Divine Path: normal levels re-solve from the new position (cheap) so the
                // route shrinks as you follow it; boss paths are solved once (the queen chase
                // is tied to the exact sequence) and just trimmed cell by cell.
                if (_divinePathActive)
                {
                    if (_advLevel.IsBoss()) TrimDivinePath();
                    else _divinePath = ComputeDivinePath();
                }

                // Queen boss: capture the queen (only once her minions/shield are gone) to
                // drop a phase. After the FINAL capture she's dead but the level isn't won
                // yet. You then have to reach the flag tile to finish.
                if (_advLevel.IsBoss())
                {
                    _ui.SetBossHud(true, _queenHealth, _advLevel.Phases.Length, _sim.QueenShielded);
                    if (_bossQueenDead)
                    {
                        int prow = Mathf.RoundToInt(_sim.PlayerY - _sim.ScrollCells);
                        if (_sim.PlayerCol == _advGoalCol && prow == _advGoalRow) AdventureWin();
                    }
                    else if (_sim.QueenDownThisMove) AdvanceBossPhase();
                    return;
                }

                // Base-row (unscrolled) coordinate: equals PlayerY on normal maps and
                // strips the follow-camera offset on tall maps.
                int playerRow = Mathf.RoundToInt(_sim.PlayerY - _sim.ScrollCells);
                bool atGoal = _sim.PlayerCol == _advGoalCol && playerRow == _advGoalRow;
                bool cleared = _sim.AdvAllCaptured();
                bool won =
                    _advLevel.Objective == Objective.Reach ? atGoal :
                    _advLevel.Objective == Objective.CaptureAll ? cleared :
                    (cleared && atGoal); // Combined
                if (won) AdventureWin();
            }
        }

        // Captured the queen: drop her a phase. When her last HP goes she's dead, but the
        // level isn't won yet. The player must still reach the flag (_bossQueenDead). Until
        // then, transport the player to the next (harder) phase, keeping the move total.
        private void AdvanceBossPhase()
        {
            _queenHealth = Mathf.Max(0, _queenHealth - 1);
            SfxManager.Play(Sfx.PieceCapture);
            _ui.SetBossHud(true, _queenHealth, _advLevel.Phases.Length, _sim.QueenShielded);
            if (_queenHealth <= 0) { _bossQueenDead = true; return; } // now go reach the flag
            _bossPhase++;
            LoadAdventureLayout(_advLevel.Phases[_bossPhase]);
        }

        private void AdventureWin()
        {
            int stars = _advMoves <= _advLevel.ThreeStar ? 3 : (_advMoves <= _advLevel.TwoStar ? 2 : 1);
            int prevStars = SaveData.GetStars(_advLevel.Id);
            SaveData.SetStars(_advLevel.Id, stars);
            int reward = (stars == 3 && prevStars < 3) ? 500 : 0; // first 3-star clear of this level
            int balanceBefore = SaveData.GetShillings();
            if (reward > 0) SaveData.AddShillings(reward);
            SfxManager.Play(Sfx.AdventureNextLevel); // reached this level's goal
            _sim.TutorialEnd(); _sim.Adventure = false;
            _view.ClearCoachMark();
            _ui.SetAdventureHud(false, 0, "", -1); _ui.SetBossHud(false, 0, 0, false);
            _state = State.Menu;
            _ui.ShowAdventureResult(_advLevel, _advMoves, stars, reward, balanceBefore);
        }

        private void AdventureFail()
        {
            _sim.TutorialEnd(); _sim.Adventure = false;
            _view.ClearCoachMark();
            _ui.SetAdventureHud(false, 0, "", -1); _ui.SetBossHud(false, 0, 0, false);
            _state = State.Menu;
            _ui.ShowAdventureFail(_advLevel);
        }

        public void RetryAdventure() { if (_advLevel != null) StartAdventureLevel(_advLevel); }

        // Forward app background/foreground to the ad network so banners and rewarded
        // ads pause and resume correctly.
        private void OnApplicationPause(bool paused) { if (_ads != null) _ads.OnAppPause(paused); }

        private void Update()
        {
            _board.FitCamera(cam, _ui.HudTopInsetFrac(), _ui.HudBottomInsetFrac());

            if (_state == State.Playing)
            {
                // Chronokinesis halves the simulation's clock (enemy moves, scroll, timers)
                // while active; achievements and the powerup timers use real time.
                float simDt = _chronoRemaining > 0f ? Time.deltaTime * 0.5f : Time.deltaTime;
                _sim.Tick(simDt);
                _ach.Tick(Time.deltaTime);
                if (_chronoRemaining > 0f) _chronoRemaining = Mathf.Max(0f, _chronoRemaining - Time.deltaTime);
                if (_divineRemaining > 0f) _divineRemaining = Mathf.Max(0f, _divineRemaining - Time.deltaTime);
                // Chronokinesis rewind: smooth backward scroll over the first ~2 seconds.
                if (_chronoRewindRemaining > 0f)
                {
                    _sim.ScrollBack(_chronoRewindRate * Time.deltaTime);
                    _chronoRewindRemaining = Mathf.Max(0f, _chronoRewindRemaining - Time.deltaTime);
                }
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
                    float prog = Mathf.Clamp01(_sim.ScrollCells / _advLayout.FinishCells);
                    int pct = Mathf.RoundToInt(prog * 100);
                    if (pct != _lastAdvPct || _advMoves != _lastAdvHudMoves)
                    {
                        _lastAdvPct = pct; _lastAdvHudMoves = _advMoves;
                        _ui.SetAdventureHud(true, _advMoves, _advLevel.Name + "   " + pct + "%", -1);
                    }
                    if (!_sim.Alive) AdventureFail();
                    else if (_sim.ScrollCells >= _advLayout.FinishCells) AdventureWin();
                }
                else if (_advTall)
                {
                    float span = Mathf.Max(1f, _advLayout.MapRows - GameConfig.Rows);
                    if (_ui.ConsumeTallScrollInput()) _advManualScroll = true; // user grabbed the bar
                    if (_advManualScroll)
                    {
                        _sim.AdventureSetScroll(-_ui.TallScroll * span); // stays where you leave it
                    }
                    else
                    {
                        int pr = Mathf.RoundToInt(_sim.PlayerY - _sim.ScrollCells);
                        float target = Mathf.Clamp(GameConfig.Rows * 0.5f - pr, GameConfig.Rows - _advLayout.MapRows, 0f);
                        _sim.AdventureSetScroll(Mathf.Lerp(_sim.ScrollCells, target, 1f - Mathf.Exp(-9f * Time.deltaTime)));
                        _ui.SetTallScroll(-_sim.ScrollCells / span); // reflect follow position in the bar
                    }
                    _sim.AdventureFollowDerive();
                    _view.SetGoalFlag(_advGoalCol, _advGoalRow + _sim.ScrollCells);
                    HandleAdventureInput(); // handles enemy resolution + objective-aware win
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

            // The transform sting loops for as long as the player is transformed and actively
            // playing. In battle the form is timed (TransformActive); in adventure it's a
            // permanent puzzle form held until the level ends (PlayerForm != Knight). Gating on
            // the play state stops it on the timer running out, a shield break, death, pause,
            // or leaving the level, so it never lingers on another screen. Re-morphs and
            // adventure form swaps keep the flag true, so the loop is never restarted.
            bool transformedNow =
                (_state == State.Playing   && _sim.Mode == GameMode.Battle && _sim.TransformActive) ||
                (_state == State.Adventure && _sim.PlayerForm != PieceType.Knight);
            if (transformedNow != _transformLooping)
            {
                if (transformedNow) SfxManager.PlayLoop(Sfx.Transformation);
                else SfxManager.StopLoop();
                _transformLooping = transformedNow;
            }
            if (_washPulse > 0f)
            {
                _washPulse = Mathf.MoveTowards(_washPulse, 0f, Time.deltaTime / 0.7f);
                _board.WashStrength = 0.30f * Mathf.Sin(Mathf.PI * (1f - _washPulse)); // rise then fall
            }
            else _board.WashStrength = 0f;
            _board.SyncBackground(_sim.ScrollCells);
            bool showHints = _sim.Difficulty != Difficulty.Grandmaster && (_state == State.Playing || _state == State.Adventure);
            // Divine Intervention: while active, feed the view the single best move to glow green.
            KnightsGauntlet.Core.MoveTarget? bestMove = null;
            if (_divineRemaining > 0f && _state == State.Playing && _sim.Alive) bestMove = _sim.BestMove();
            var advPath = (_divinePathActive && _state == State.Adventure) ? _divinePath : null;
            // Only ever reveal the NEXT step of the solved route, never the whole thing:
            // the full route hands the player the answer outright. The path is re-solved
            // each move, so following it one step at a time still walks the optimal line.
            _view.Sync(_sim, showHints, Time.deltaTime, bestMove, advPath, true);
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

                _view.PlayMoveVfx();
                _ach.OnMove(result);
                // A capturing move plays the capture sound; any other move (trap or
                // battle) plays the plain move sound.
                bool captured = result.Captures != null && result.Captures.Count > 0;
                if (captured) SfxManager.Play(Sfx.PieceCapture);
                else SfxManager.Play(Sfx.PieceMove);
                if (result.Captures != null)
                {
                    foreach (var cap in result.Captures)
                    {
                        _view.SpawnPopup(cap.Col, cap.Y - 0.42f, "+" + cap.PointsGained, PopupGain);
                        _view.SpawnPopup(cap.Col, cap.Y + 0.14f,
                            "X" + cap.NewMultiplier + " (+" + cap.MultiplierDelta + ")", PopupCombo);
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
            _ui.ShowGameOver(_sim.Score, isTrap, analysis, _sim.RunShillings, reviveCost, SaveData.GetShillings(), CanOfferAdLife);
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
