using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    // PlayerPrefs wrapper (replaces the web's localStorage). Holds the locked
    // player name, equipped/owned skins, the shillings wallet, and daily-challenge
    // attempt tracking.
    public static class SaveData
    {
        private const string NameKey = "kg_player_name";
        private const string SkinKey = "kg_skin";
        private const string ShillingsKey = "kg_shillings";
        private const string OwnedKey = "kg_owned_skins";        // CSV of shop skin ids
        private const string DailyDateKey = "kg_daily_date";     // yyyyMMdd of last daily play
        private const string DailyUsedKey = "kg_daily_used";     // attempts used today

        public const int StartingShillings = 0;
        public const int DailyAttemptsPerDay = 10;

        // ── Name (permanent once set) ────────────────────────────────────────
        public static string GetName() => PlayerPrefs.GetString(NameKey, "");
        public static void SetName(string n) { PlayerPrefs.SetString(NameKey, n); PlayerPrefs.Save(); }
        public static bool IsNameLocked() => !string.IsNullOrEmpty(GetName());

        // ── Colorblind mode (shape markers on enemy move-intent cells) ───────
        public static bool IsColorblind() => PlayerPrefs.GetInt("kg_colorblind", 0) == 1;
        public static void SetColorblind(bool on) { PlayerPrefs.SetInt("kg_colorblind", on ? 1 : 0); PlayerPrefs.Save(); }

        // ── Adventure mode (best stars per level, 0..3) ──────────────────────
        public static int GetStars(string levelId) => PlayerPrefs.GetInt("kg_adv_" + levelId, 0);
        public static void SetStars(string levelId, int stars)
        {
            if (stars > GetStars(levelId)) { PlayerPrefs.SetInt("kg_adv_" + levelId, stars); PlayerPrefs.Save(); }
        }

        // ── Equipped skin ────────────────────────────────────────────────────
        public static string GetSkin() => PlayerPrefs.GetString(SkinKey, "gold");
        public static void SetSkin(string id) { PlayerPrefs.SetString(SkinKey, id); PlayerPrefs.Save(); }

        // ── Shillings wallet ─────────────────────────────────────────────────
        public static int GetShillings() => PlayerPrefs.GetInt(ShillingsKey, StartingShillings);
        public static void AddShillings(int n)
        {
            if (n == 0) return;
            PlayerPrefs.SetInt(ShillingsKey, Math.Max(0, GetShillings() + n));
            PlayerPrefs.Save();
        }
        public static bool TrySpendShillings(int n)
        {
            if (n < 0) return false;
            int bal = GetShillings();
            if (bal < n) return false;
            PlayerPrefs.SetInt(ShillingsKey, bal - n);
            PlayerPrefs.Save();
            return true;
        }

        // ── Owned (purchased) shop skins ─────────────────────────────────────
        public static HashSet<string> GetOwnedSkins()
        {
            var set = new HashSet<string>();
            var csv = PlayerPrefs.GetString(OwnedKey, "");
            if (!string.IsNullOrEmpty(csv))
                foreach (var id in csv.Split(',')) if (!string.IsNullOrEmpty(id)) set.Add(id);
            return set;
        }
        public static bool OwnsShopSkin(string id) => GetOwnedSkins().Contains(id);
        public static void AddOwnedSkin(string id)
        {
            var set = GetOwnedSkins();
            if (set.Add(id))
            {
                PlayerPrefs.SetString(OwnedKey, string.Join(",", set));
                PlayerPrefs.Save();
            }
        }

        // ── Daily challenge attempts (reset each calendar day) ───────────────
        private static string TodayStamp() => DateTime.Now.ToString("yyyyMMdd");

        public static int DailyAttemptsLeft()
        {
            if (PlayerPrefs.GetString(DailyDateKey, "") != TodayStamp()) return DailyAttemptsPerDay;
            return Math.Max(0, DailyAttemptsPerDay - PlayerPrefs.GetInt(DailyUsedKey, 0));
        }
        public static void UseDailyAttempt()
        {
            string today = TodayStamp();
            if (PlayerPrefs.GetString(DailyDateKey, "") != today)
            {
                PlayerPrefs.SetString(DailyDateKey, today);
                PlayerPrefs.SetInt(DailyUsedKey, 0);
            }
            PlayerPrefs.SetInt(DailyUsedKey, PlayerPrefs.GetInt(DailyUsedKey, 0) + 1);
            PlayerPrefs.Save();
        }

        // Seed shared by every player for today's daily board.
        public static int DailySeed() => int.Parse(TodayStamp());
    }
}
