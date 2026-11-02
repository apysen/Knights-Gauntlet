using UnityEngine;

namespace KnightsGauntlet.View
{
    public enum PowerupId { Chronokinesis, DivineIntervention, DivinePath, WarHorn, Berserker }

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
            Name = "Divine Intervention", Blurb = "Trap: A divine whisper guides your hand toward the perfect moves.",
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

        // Battle-only: restores the combo multiplier to the highest it reached this run.
        // Instant (no duration); only usable while the current streak is below that peak.
        public static readonly PowerupInfo WarHorn = new PowerupInfo
        {
            Id = PowerupId.WarHorn, Key = "war_horn",
            Name = "War Horn", Blurb = "Battle: One blast of the old war horn and your greatest streak charges back into the fight.",
            Cost = 3000, Duration = 0f,
        };

        // Battle-only, timed: each capture adds five times its usual multiplier gain.
        public const int BerserkerGainScale = 5;
        public static readonly PowerupInfo Berserker = new PowerupInfo
        {
            Id = PowerupId.Berserker, Key = "berserker",
            Name = "Berserker Draught", Blurb = "Battle: A fiery brew that makes every capture build your multiplier five times as fast.",
            Cost = 2500, Duration = 15f,
        };

        public static readonly PowerupInfo[] All = { Chronokinesis, WarHorn, Berserker, DivineIntervention, DivinePath }; // shop order

        public static PowerupInfo Get(PowerupId id)
            => id == PowerupId.Chronokinesis ? Chronokinesis
             : id == PowerupId.DivineIntervention ? DivineIntervention
             : id == PowerupId.WarHorn ? WarHorn
             : id == PowerupId.Berserker ? Berserker
             : DivinePath;

        // The shop and HUD share the same line icon family.
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _icons =
            new System.Collections.Generic.Dictionary<string, Sprite>();
        public static Sprite IconOf(string key)
        {
            // Domain reload is off (Enter Play Mode Options), so this static cache outlives Play
            // sessions while the runtime-made sprites in it are destroyed on exit: rebuild those.
            if (_icons.TryGetValue(key, out var s) && s != null) return s;
            string royal = key == "chronokinesis" ? "time" : key == "divine_path" ? "path" : key == "war_horn" ? "horn" : key == "berserker" ? "potion" : "shield";
            s = RoyalIcons.Get(royal);
            _icons[key] = s;
            return s;
        }
    }
}
