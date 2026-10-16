using System.Collections.Generic;

namespace KnightsGauntlet.View
{
    // A movement VFX: the animation played at the knight's feet each time it moves.
    // Frames live under Resources/VFX. `Sheet` sources are a single sliced sprite
    // sheet (Explosion/Void/… from the RPG VFX pack); non-sheet sources are a folder
    // of per-frame textures (Smoke, Lightning).
    public class VfxInfo
    {
        public string Id;
        public string Name;
        public string ResourcesPath;
        public bool Sheet;
        public float Fps;
        public int Price; // direct shilling price; None is always free
        public bool BehindKnight; // draw between the board and the knight (else on top)
        public float Scale = 1f;  // per-effect size multiplier over the base footprint
    }

    // Single source of truth for movement VFX, mirroring Skins.
    public static class Vfx
    {
        public const string NoneId = "none";

        public static readonly VfxInfo[] All =
        {
            new VfxInfo { Id = "explosion", Name = "Explosion", ResourcesPath = "VFX/explosion", Sheet = true,  Fps = 14f, Price = 5000 },
            new VfxInfo { Id = "fireworks", Name = "Fireworks", ResourcesPath = "VFX/fireworks", Sheet = true,  Fps = 14f, BehindKnight = true, Price = 5000 },
            new VfxInfo { Id = "void",      Name = "Void",      ResourcesPath = "VFX/void",      Sheet = true,  Fps = 14f, BehindKnight = true, Scale = 1.5f, Price = 6500 },
            new VfxInfo { Id = "water",     Name = "Water",     ResourcesPath = "VFX/water",     Sheet = true,  Fps = 14f, Price = 4500 },
            new VfxInfo { Id = "wind",      Name = "Wind",      ResourcesPath = "VFX/wind",      Sheet = true,  Fps = 14f, Price = 4500 },
            new VfxInfo { Id = "smoke",     Name = "Smoke",     ResourcesPath = "VFX/smoke",     Sheet = false, Fps = 16f, BehindKnight = true, Price = 3500 },
            new VfxInfo { Id = "lightning", Name = "Lightning", ResourcesPath = "VFX/lightning", Sheet = false, Fps = 14f, BehindKnight = true, Price = 6500 },
        };

        public static VfxInfo Find(string id)
        {
            foreach (var v in All) if (v.Id == id) return v;
            return null;
        }

        // "none" is always available; effects are purchased directly with shillings.
        public static bool IsOwned(string id) => id == NoneId || SaveData.OwnsVfx(id);

        public static string NameOf(string id) => id == NoneId ? "None" : (Find(id)?.Name ?? "None");

        // None + every effect, for the customization grid (mirrors Skins.All()).
        public static IEnumerable<VfxInfo> Grid()
        {
            yield return new VfxInfo { Id = NoneId, Name = "None" };
            foreach (var v in All) yield return v;
        }
    }
}
