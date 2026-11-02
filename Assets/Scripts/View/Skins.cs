using System.Collections.Generic;
using UnityEngine;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    public enum SkinSource { Default, Achievement, Shop, Mastery }

    public class SkinInfo
    {
        public string Id;
        public string Name;
        public Color Color;
        public int Price;          // shop skins only
        public SkinSource Source;
        public int Fx;             // shader finish (SkinFx), 0 = plain colour tint
        public Color Glow;         // player glow colour; alpha 0 = use Color
    }

    // Shader finishes, mirrored by the KG_SKIN_* ids in PieceSkinFX.hlsl. They retexture
    // whichever piece the player is, so they carry through every Battle transformation.
    public static class SkinFx
    {
        public const int None = 0, StainedGlass = 1, Phantom = 2, Gilded = 3, Queensbane = 4, Nightfall = 5, Starforged = 6;
    }

    // Single source of truth for skin ids → colours, used by GetSkinTint, the
    // achievements screen, and the marketplace. Shop skins are placeholder
    // colour swatches for now; an artist will replace them with real art later
    // (each just needs its sprite dropped into the matching GameTheme skin slot).
    public static class Skins
    {
        public static readonly SkinInfo Default =
            new SkinInfo { Id = "gold", Name = "Classic", Color = Hex("#EFB000"), Source = SkinSource.Default };

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
            // Premium finishes: full retextures rather than colour tints.
            new SkinInfo { Id = "stainedglass", Name = "Stained Glass", Color = Hex("#3F6BFF"), Glow = Hex("#FFC66B"), Price = 10000, Source = SkinSource.Shop, Fx = SkinFx.StainedGlass },
            new SkinInfo { Id = "phantom",      Name = "Phantom",       Color = Hex("#9FF3FF"), Glow = Hex("#7FF5FF"), Price = 10000, Source = SkinSource.Shop, Fx = SkinFx.Phantom },
            new SkinInfo { Id = "gilded",       Name = "Golden Gilded", Color = Hex("#FFD36B"), Glow = Hex("#FFC94A"), Price = 10000, Source = SkinSource.Shop, Fx = SkinFx.Gilded },
            new SkinInfo { Id = "queensbane",   Name = "Queensbane",    Color = Hex("#D9DEE6"), Glow = Hex("#FF3A2A"), Price = 10000, Source = SkinSource.Shop, Fx = SkinFx.Queensbane },
            new SkinInfo { Id = "starforged",   Name = "Starforged",    Color = Hex("#7B5CFF"), Glow = Hex("#8A7CFF"), Price = 10000, Source = SkinSource.Shop, Fx = SkinFx.Starforged },
        };

        // Secret "Mastery" reward: a black knight with a red glowing eye. Unlocks only
        // when every Adventure level has a 3-star clear (see HasMasteredAllLevels), and is
        // shown as a mystery (???) in Customization until then. Fully black armor with
        // glowing red eyes, drawn by the Nightfall shader finish on every form.
        public static readonly SkinInfo Secret =
            new SkinInfo { Id = "nightfall", Name = "Nightfall", Color = Hex("#141418"), Glow = Hex("#FF2A2A"),
                Source = SkinSource.Mastery, Fx = SkinFx.Nightfall };

        public static IEnumerable<SkinInfo> All()
        {
            yield return Default;
            foreach (var s in Achievement) yield return s;
            foreach (var s in Shop) yield return s;
            yield return Secret;
        }

        // True once the player has a 3-star clear on every Adventure level.
        public static bool HasMasteredAllLevels()
        {
            var all = AdventureLevels.All;
            if (all == null || all.Length == 0) return false;
            foreach (var lvl in all) if (SaveData.GetStars(lvl.Id) < 3) return false;
            return true;
        }

        // The player's radial-glow colour: the skin tint, unless the skin names its own
        // glow (the black Nightfall knight glows red, Stained Glass glows warm, ...).
        public static Color GlowOf(string id)
        {
            var s = Find(id);
            return s.Glow.a > 0f ? s.Glow : s.Color;
        }

        // Shader finish for this skin, 0 for plain colour skins.
        public static int FxOf(string id) => string.IsNullOrEmpty(id) ? SkinFx.None : Find(id).Fx;

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
            if (id == Secret.Id) return HasMasteredAllLevels();
            foreach (var s in Shop) if (s.Id == id) return SaveData.OwnsShopSkin(id);
            foreach (var a in Achievements.All) if (a.SkinId == id) return Achievements.IsUnlocked(a.Id);
            return false;
        }

        private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
