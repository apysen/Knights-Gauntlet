using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    public enum SkinSource { Default, Achievement, Shop }

    public class SkinInfo
    {
        public string Id;
        public string Name;
        public Color Color;
        public int Price;          // shop skins only
        public SkinSource Source;
    }

    // Single source of truth for skin ids → colours, used by GetSkinTint, the
    // achievements screen, and the marketplace. Shop skins are placeholder
    // colour swatches for now; an artist will replace them with real art later
    // (each just needs its sprite dropped into the matching GameTheme skin slot).
    public static class Skins
    {
        public static readonly SkinInfo Default =
            new SkinInfo { Id = "gold", Name = "Classic Gold", Color = Hex("#F0C040"), Source = SkinSource.Default };

        // Earned via achievements (colours mirror Achievements.All).
        public static readonly SkinInfo[] Achievement =
        {
            new SkinInfo { Id = "emerald", Name = "Emerald", Color = Hex("#3CDC78"), Source = SkinSource.Achievement },
            new SkinInfo { Id = "crimson", Name = "Crimson", Color = Hex("#FF5A5A"), Source = SkinSource.Achievement },
            new SkinInfo { Id = "violet",  Name = "Violet",  Color = Hex("#B070FF"), Source = SkinSource.Achievement },
            new SkinInfo { Id = "frost",   Name = "Frost",   Color = Hex("#8FE0FF"), Source = SkinSource.Achievement },
            new SkinInfo { Id = "magma",   Name = "Magma",   Color = Hex("#FF8A3C"), Source = SkinSource.Achievement },
        };

        // Buyable with shillings. Colours chosen to not clash with the above.
        public static readonly SkinInfo[] Shop =
        {
            new SkinInfo { Id = "teal",   Name = "Teal",   Color = Hex("#2ED9C3"), Price = 3000,  Source = SkinSource.Shop },
            new SkinInfo { Id = "rose",   Name = "Rose",   Color = Hex("#FF6FA5"), Price = 3000,  Source = SkinSource.Shop },
            new SkinInfo { Id = "sky",    Name = "Sky",    Color = Hex("#4FA8FF"), Price = 4000,  Source = SkinSource.Shop },
            new SkinInfo { Id = "lime",   Name = "Lime",   Color = Hex("#B6E33B"), Price = 4000,  Source = SkinSource.Shop },
            new SkinInfo { Id = "amber",  Name = "Amber",  Color = Hex("#FFB300"), Price = 5000, Source = SkinSource.Shop },
            new SkinInfo { Id = "coral",  Name = "Coral",  Color = Hex("#FF7A5C"), Price = 5000, Source = SkinSource.Shop },
            new SkinInfo { Id = "indigo", Name = "Indigo", Color = Hex("#6C5CE7"), Price = 6000, Source = SkinSource.Shop },
            new SkinInfo { Id = "silver", Name = "Silver", Color = Hex("#C9D2E0"), Price = 7500, Source = SkinSource.Shop },
        };

        public static IEnumerable<SkinInfo> All()
        {
            yield return Default;
            foreach (var s in Achievement) yield return s;
            foreach (var s in Shop) yield return s;
        }

        public static SkinInfo Find(string id)
        {
            foreach (var s in All()) if (s.Id == id) return s;
            return Default;
        }

        public static Color ColorOf(string id) => Find(id).Color;
        public static string NameOf(string id) => Find(id).Name;

        // Whether the player may equip this skin: gold is free, achievement skins
        // when their achievement is unlocked, shop skins when purchased.
        public static bool IsOwned(string id)
        {
            if (id == "gold") return true;
            foreach (var s in Shop) if (s.Id == id) return SaveData.OwnsShopSkin(id);
            foreach (var a in Achievements.All) if (a.SkinId == id) return Achievements.IsUnlocked(a.Id);
            return false;
        }

        private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
