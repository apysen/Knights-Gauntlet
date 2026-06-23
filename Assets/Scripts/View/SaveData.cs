using UnityEngine;

namespace KnightsGauntlet.View
{
    // Thin PlayerPrefs wrapper, replacing the localStorage calls from the web
    // version. Keys mirror the originals (kg_player_name, kg_skin) so they read
    // naturally; the leaderboard / achievements layers will add more later.
    public static class SaveData
    {
        private const string NameKey = "kg_player_name";
        private const string SkinKey = "kg_skin";

        public static string GetName() => PlayerPrefs.GetString(NameKey, "");
        public static void SetName(string n) { PlayerPrefs.SetString(NameKey, n); PlayerPrefs.Save(); }

        public static string GetSkin() => PlayerPrefs.GetString(SkinKey, "gold");
        public static void SetSkin(string id) { PlayerPrefs.SetString(SkinKey, id); PlayerPrefs.Save(); }
    }
}
