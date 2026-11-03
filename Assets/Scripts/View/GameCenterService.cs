using KnightsGauntlet.Core;
using UnityEngine;

namespace KnightsGauntlet.View
{
    // Mirrors best scores to Apple Game Center leaderboards on iOS. Supabase stays the
    // main leaderboard (one board for every platform, shown in our own UI); this is an
    // extra layer so players also see their progress in Apple's Games app.
    //
    // Uses Unity's built-in Social API, which talks to GameKit on iOS with no plugin. On
    // every other platform each call is a no-op.
    //
    // Nothing here ever blocks play. If the player is not signed in to Game Center, or
    // declines, reports are skipped; the next successful sign-in re-sends every stored
    // best score, so runs played offline still land. (In-game achievements stay local only.)
    //
    // The IDs below must match the ones created in App Store Connect
    // (App > Services > Game Center) character for character.
    public static class GameCenterService
    {
        private const string Prefix = "com.apy.knightsgauntlet.";

        // The fixed boards. The daily challenge has its own recurring board, see SubmitDaily.
        private static readonly string[] Boards =
        {
            "trap-hard", "trap-grandmaster", "battle-hard", "battle-grandmaster",
        };

        // "trap-hard" -> "com.apy.knightsgauntlet.lb.trap_hard"
        public static string LeaderboardId(string category) => Prefix + "lb." + category.Replace('-', '_');

        private const string BestKey = "kg_gc_best_";

        // A recurring leaderboard in App Store Connect that resets every 24 hours.
        public const string DailyLeaderboardId = Prefix + "lb.daily";

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void KG_GameCenterSubmitScore(string leaderboardId, long score);
#endif

        public static bool Authenticated { get; private set; }
        private static bool _started;

        public static void Initialize()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_started) return;
            _started = true;
            Social.localUser.Authenticate(ok =>
            {
                Authenticated = ok;
                Debug.Log("[Knight's Gauntlet] Game Center sign-in: " + (ok ? "ok" : "unavailable"));
                if (ok) SyncAll();
            });
#endif
        }

        // Called at the end of every run. The best score per board is remembered locally
        // whether or not Game Center is reachable, so it can be sent later.
        public static void ReportScore(GameMode mode, Difficulty difficulty, int score)
        {
            if (score <= 0) return;
            string category = mode.ToString().ToLower() + "-" + difficulty.ToString().ToLower();
            if (System.Array.IndexOf(Boards, category) < 0) return;

            int best = PlayerPrefs.GetInt(BestKey + category, 0);
            if (score <= best) return;
            PlayerPrefs.SetInt(BestKey + category, score);
            PlayerPrefs.Save();
            SendScore(category, score);
        }

        // Every daily run is posted; Game Center keeps the best per occurrence. Not queued
        // for later: a daily score sent after the board resets would land on the wrong day.
        public static void SubmitDaily(int score)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!Authenticated || score <= 0) return;
            KG_GameCenterSubmitScore(DailyLeaderboardId, score);
#endif
        }

        public static void ShowLeaderboards()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Authenticated) Social.ShowLeaderboardUI();
#endif
        }

        private static void SendScore(string category, int score)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!Authenticated) return;
            string gcId = LeaderboardId(category);
            Social.ReportScore(score, gcId, ok =>
            {
                if (!ok) Debug.LogWarning("[Knight's Gauntlet] Game Center score failed: " + gcId);
            });
#endif
        }

        // Game Center ignores a score lower than the player's best, so re-sending every
        // stored best on sign-in is safe and cheap.
        private static void SyncAll()
        {
            foreach (var category in Boards)
            {
                int best = PlayerPrefs.GetInt(BestKey + category, 0);
                if (best > 0) SendScore(category, best);
            }
        }
    }
}
