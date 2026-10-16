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
        private const string VfxKey = "kg_vfx";                  // equipped movement VFX id ("none" default)
        private const string OwnedVfxKey = "kg_owned_vfx";       // CSV of unlocked VFX ids
        private const string DailyDateKey = "kg_daily_date";     // yyyyMMdd of last daily play
        private const string DailyUsedKey = "kg_daily_used";     // attempts used today

        public const int StartingShillings = 0;
        public const int DailyAttemptsPerDay = 10;

        // ── Name (permanent once set) ────────────────────────────────────────
        public static string GetName() => PlayerPrefs.GetString(NameKey, "");
        public static void SetName(string n) { PlayerPrefs.SetString(NameKey, n); PlayerPrefs.Save(); }
        public static bool IsNameLocked() => !string.IsNullOrEmpty(GetName());

        // ── Colorblind mode (shape markers on enemy move-intent cells) ───────
        // Read once per frame by GameView.Sync, so it's cached in memory: PlayerPrefs is a
        // native call, and this value only ever changes from the settings toggle below.
        private static int _colorblindCache = -1;
        public static bool IsColorblind()
        {
            if (_colorblindCache < 0) _colorblindCache = PlayerPrefs.GetInt("kg_colorblind", 0);
            return _colorblindCache == 1;
        }
        public static void SetColorblind(bool on)
        {
            _colorblindCache = on ? 1 : 0;
            PlayerPrefs.SetInt("kg_colorblind", _colorblindCache); PlayerPrefs.Save();
        }

        // ── Screen shake (camera kick on capture) ────────────────────────────
        // On by default. Read on every capture by GameManager, so it's cached the same
        // way as colorblind; only the settings/pause toggle below ever changes it.
        private static int _screenShakeCache = -1;
        public static bool IsScreenShake()
        {
            if (_screenShakeCache < 0) _screenShakeCache = PlayerPrefs.GetInt("kg_screenshake", 1);
            return _screenShakeCache == 1;
        }
        public static void SetScreenShake(bool on)
        {
            _screenShakeCache = on ? 1 : 0;
            PlayerPrefs.SetInt("kg_screenshake", _screenShakeCache); PlayerPrefs.Save();
        }

        private static int _captureHapticsCache = -1;
        public static bool IsCaptureHaptics()
        {
            if (_captureHapticsCache < 0) _captureHapticsCache = PlayerPrefs.GetInt("kg_capture_haptics", 1);
            return _captureHapticsCache == 1;
        }
        public static void SetCaptureHaptics(bool on)
        {
            _captureHapticsCache = on ? 1 : 0;
            PlayerPrefs.SetInt("kg_capture_haptics", _captureHapticsCache); PlayerPrefs.Save();
        }

        private static int _reducedEffectsCache = -1;
        public static bool IsReducedEffects()
        {
            if (_reducedEffectsCache < 0) _reducedEffectsCache = PlayerPrefs.GetInt("kg_reduced_effects", 0);
            return _reducedEffectsCache == 1;
        }
        public static void SetReducedEffects(bool on)
        {
            _reducedEffectsCache = on ? 1 : 0;
            PlayerPrefs.SetInt("kg_reduced_effects", _reducedEffectsCache);
            PlayerPrefs.Save();
        }

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

        // ── Powerup inventory (consumable; buy in the shop, spend on activation) ──
        // Stored per powerup id as a simple count under "kg_pu_<id>". The HUD reads these
        // every frame, so counts are cached in memory and only written through on a change
        // (the key-string concat alone was allocating once per powerup per frame).
        private static readonly Dictionary<string, int> _powerupCache = new Dictionary<string, int>();
        public static int GetPowerup(string id)
        {
            if (_powerupCache.TryGetValue(id, out int v)) return v;
            v = PlayerPrefs.GetInt("kg_pu_" + id, 0);
            _powerupCache[id] = v;
            return v;
        }
        public static void AddPowerup(string id, int n)
        {
            if (n == 0) return;
            SetPowerup(id, Math.Max(0, GetPowerup(id) + n));
        }
        // Consumes one; returns false (and spends nothing) if the player has none.
        public static bool TrySpendPowerup(string id)
        {
            int have = GetPowerup(id);
            if (have <= 0) return false;
            SetPowerup(id, have - 1);
            return true;
        }
        private static void SetPowerup(string id, int n)
        {
            _powerupCache[id] = n;
            PlayerPrefs.SetInt("kg_pu_" + id, n);
            PlayerPrefs.Save();
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

        // ── Movement VFX (equipped + purchased) ──────────────────────────────
        public static string GetVfx() => PlayerPrefs.GetString(VfxKey, "none");
        public static void SetVfx(string id) { PlayerPrefs.SetString(VfxKey, id); PlayerPrefs.Save(); }
        public static HashSet<string> GetOwnedVfx()
        {
            var set = new HashSet<string>();
            var csv = PlayerPrefs.GetString(OwnedVfxKey, "");
            if (!string.IsNullOrEmpty(csv))
                foreach (var id in csv.Split(',')) if (!string.IsNullOrEmpty(id)) set.Add(id);
            return set;
        }
        public static bool OwnsVfx(string id) => GetOwnedVfx().Contains(id);
        public static void AddOwnedVfx(string id)
        {
            var set = GetOwnedVfx();
            if (set.Add(id))
            {
                PlayerPrefs.SetString(OwnedVfxKey, string.Join(",", set));
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
