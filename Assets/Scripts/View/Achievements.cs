using UnityEngine;

namespace KnightsGauntlet.View
{
    // Achievement definitions and unlock persistence (replaces the JS kg_ach
    // localStorage list). Each achievement unlocks a skin id that the GameTheme
    // resolves to a colour/sprite.
    public static class Achievements
    {
        public struct Def
        {
            public string Id, Name, Desc, SkinId, SkinName, Hex;
            public bool Battle;
        }

        public static readonly Def[] All =
        {
            new Def { Id = "trap_advance",   Name = "Trailblazer",  Desc = "Trap: advance 25 squares without backtracking.", SkinId = "emerald", SkinName = "Emerald", Hex = "#3CDC78", Battle = false },
            new Def { Id = "trap_score",     Name = "Centurion",    Desc = "Trap: reach a score of 150.",                    SkinId = "crimson", SkinName = "Crimson", Hex = "#FF5A5A", Battle = false },
            new Def { Id = "battle_streak",  Name = "Relentless",   Desc = "Battle: capture 5 in a row without a miss.",      SkinId = "violet",  SkinName = "Violet",  Hex = "#B070FF", Battle = true  },
            new Def { Id = "battle_marathon",Name = "Marathon",     Desc = "Battle: hold one transformation for a minute.",   SkinId = "frost",   SkinName = "Frost",   Hex = "#8FE0FF", Battle = true  },
            new Def { Id = "battle_polymorph",Name = "Shapeshifter",Desc = "Battle: transform into all four forms, twice each.",SkinId = "magma", SkinName = "Magma",   Hex = "#FF8A3C", Battle = true  },
        };

        private const string Key = "kg_ach";

        public static bool IsUnlocked(string id)
        {
            var s = PlayerPrefs.GetString(Key, "");
            return ("," + s + ",").Contains("," + id + ",");
        }

        // Returns true if this call newly unlocked it.
        public static bool Unlock(string id)
        {
            if (IsUnlocked(id)) return false;
            var s = PlayerPrefs.GetString(Key, "");
            s = string.IsNullOrEmpty(s) ? id : s + "," + id;
            PlayerPrefs.SetString(Key, s);
            PlayerPrefs.Save();
            return true;
        }

        public static bool TryFind(string id, out Def def)
        {
            foreach (var d in All) if (d.Id == id) { def = d; return true; }
            def = default; return false;
        }
    }
}
