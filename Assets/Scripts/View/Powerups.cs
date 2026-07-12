using UnityEngine;

namespace KnightsGauntlet.View
{
    public enum PowerupId { Chronokinesis, DivineIntervention, DivinePath }

    // A consumable in-run powerup. Bought in the shop's Powerups tab, spent from the
    // in-game activation buttons. Effects are applied in GameManager; this is just the
    // catalog (name, cost, icon, effect duration) so the shop and HUD read the same data.
    public class PowerupInfo
    {
        public PowerupId Id;
        public string Key;        // save-inventory key + Resources icon subpath
        public string Name;
        public string Blurb;      // one-line description shown in the shop
        public int Cost;          // shillings per copy
        public float Duration;    // seconds the effect lasts once activated

        public int Owned => SaveData.GetPowerup(Key);
        public Sprite Icon => Powerups.IconOf(Key);
    }

    public static class Powerups
    {
        public static readonly PowerupInfo Chronokinesis = new PowerupInfo
        {
            Id = PowerupId.Chronokinesis, Key = "chronokinesis",
            Name = "Chronokinesis", Blurb = "Bend the threads of time, slowing the world as you reclaim your lost moments.",
            Cost = 1000, Duration = 15f,
        };

        public static readonly PowerupInfo DivineIntervention = new PowerupInfo
        {
            Id = PowerupId.DivineIntervention, Key = "divine",
            Name = "Divine Intervention", Blurb = "A divine whisper guides your hand toward the perfect moves.",
            Cost = 2000, Duration = 20f,
        };

        // Adventure-only: highlights the next tile of the optimal route, re-solved each move
        // (it lasts the whole level, unlike the timed powerups).
        public static readonly PowerupInfo DivinePath = new PowerupInfo
        {
            Id = PowerupId.DivinePath, Key = "divine_path",
            Name = "Pathfinder", Blurb = "Adventure: An old explorer's map unveils the surest path forward.",
            Cost = 5000, Duration = 0f,
        };

        public static readonly PowerupInfo[] All = { Chronokinesis, DivineIntervention, DivinePath };

        public static PowerupInfo Get(PowerupId id)
            => id == PowerupId.Chronokinesis ? Chronokinesis
             : id == PowerupId.DivineIntervention ? DivineIntervention
             : DivinePath;

        // Icons live at Assets/Resources/Powerups/<file>.png. Cached per key.
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _icons =
            new System.Collections.Generic.Dictionary<string, Sprite>();
        public static Sprite IconOf(string key)
        {
            if (_icons.TryGetValue(key, out var s)) return s;
            string file = key == "chronokinesis" ? "Powerups/Chronokinesis"
                        : key == "divine_path" ? "Powerups/Pathfinder"
                        : "Powerups/Holy Cross";
            s = Resources.Load<Sprite>(file);
            if (s == null) Debug.LogWarning("[Powerups] Missing icon at Assets/Resources/" + file + ".png");
            _icons[key] = s;
            return s;
        }
    }
}
