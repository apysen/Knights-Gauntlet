using System.Collections.Generic;
using UnityEngine;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    public enum ComicPose { Idle, Attack, Hit }
    public enum PieceFacing { Front, Back }

    // Shared imported sprites and material; never creates a material per piece.
    public static class ComicArt
    {
        public static readonly Color Ink = UIFactory.Hex("#111210");
        public static readonly Color Cyan = UIFactory.Hex("#B18418"); // jump accent
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static Material _material;
        private static Material _paintedMaterial;
        public static Material Material => _material != null ? _material : (_material = Resources.Load<Material>("Comic/ComicPalette"));
        public static Material PaintedMaterial => _paintedMaterial != null ? _paintedMaterial : (_paintedMaterial = Resources.Load<Material>("Comic/PaintedChess"));

        public static Sprite Piece(PieceType type, ComicPose pose = ComicPose.Idle, PieceFacing facing = PieceFacing.Front, bool boss = false)
        {
            if (type == PieceType.Trap) return pose == ComicPose.Idle ? Load("comic-trap-idle") : null;
            if (type == PieceType.Nightrider) type = PieceType.Knight;
            // The approved set has two views, with no separate attack/hit drawings.
            // Keep those events immediate and present them with the existing impact FX.
            string name = boss && type == PieceType.Queen ? "king" : type.ToString().ToLowerInvariant();
            return Load("supplied-" + name + "-" + facing.ToString().ToLowerInvariant());
        }

        public static Sprite Load(string name)
        {
            if (!Sprites.TryGetValue(name, out var sprite))
            {
                sprite = Resources.Load<Sprite>("Comic/" + name);
                Sprites[name] = sprite;
            }
            return sprite;
        }

        public static Color ColorOf(PieceType type) => UIFactory.Hex(type switch
        {
            PieceType.Rook => "#FF6500",
            PieceType.Bishop => "#00858B",
            PieceType.Queen => "#B90052",
            PieceType.Trap => "#CF3524",
            PieceType.Nightrider => "#124AAB",
            PieceType.Pawn => "#00632D",
            _ => "#124AAB"
        });
    }
}
