using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // The single source of truth for how the game LOOKS. Every renderer reads
    // from a GameTheme; nothing hard-codes a color, sprite, glyph, or font.
    //
    // To reskin the whole game you create a new GameTheme asset
    // (Create > Knight's Gauntlet > Game Theme), drop in your art, and assign it.
    // Any slot you leave empty falls back to the procedural glyph/color look from
    // the Core layer, so the game is fully playable before a single sprite exists.
    [CreateAssetMenu(fileName = "GameTheme", menuName = "Knight's Gauntlet/Game Theme")]
    public class GameTheme : ScriptableObject
    {
        [Serializable]
        public struct PieceVisual
        {
            public PieceType type;
            public Sprite sprite;   // leave null to fall back to the glyph
            public Color tint;      // applied to the sprite OR the glyph
        }

        [Serializable]
        public struct SkinVisual
        {
            public string id;            // 'gold', 'emerald', ...
            public string displayName;
            public Color tint;           // recolors the player's current form
            public Sprite overrideSprite; // null = just recolor the default form art
        }

        [Header("Board tiles")]
        [Tooltip("Square sprite for a board cell. Null = a solid filled square.")]
        public Sprite cellSprite;
        public Color cellColorA = new Color(0.176f, 0.176f, 0.306f); // #2d2d4e
        public Color cellColorB = new Color(0.118f, 0.118f, 0.208f); // #1e1e35
        public Color gridLineColor = new Color(1f, 1f, 1f, 0.05f);

        [Header("Pieces")]
        [Tooltip("One entry per PieceType. Sprite null = chess-glyph fallback.")]
        public PieceVisual[] pieces;
        [Tooltip("Translucent square drawn behind a piece (the rgba backing in the original). Null = none.")]
        public Sprite pieceBackingSprite;
        [Range(0f, 1f)] public float pieceBackingAlpha = 0.35f;

        [Header("Move hints")]
        public Sprite hintSprite;    // null = solid square
        public Sprite hintDotSprite; // null = generated circle
        public Color hintColor = new Color(0.941f, 0.753f, 0.251f, 0.25f); // #f0c040 @ .25

        [Header("Threat tiles (battle)")]
        public Sprite threatSprite;
        public Color threatFill = new Color(0.353f, 0.078f, 0.078f, 0.55f);
        public Color threatStroke = new Color(0.588f, 0.157f, 0.157f, 0.55f);

        [Header("Pickups (shillings)")]
        [Tooltip("Coin/shilling sprite. null = generated gold circle.")]
        public Sprite pickupSprite;
        public Color pickupColor = new Color(0.95f, 0.78f, 0.25f, 1f);

        [Header("UI (menus / buttons): null = generated rounded sprite")]
        [Tooltip("9-sliced rounded fill used for buttons, inputs, pills, bars.")]
        public Sprite uiPanelSprite;
        [Tooltip("9-sliced rounded ring used for the selected / outline state.")]
        public Sprite uiBorderSprite;
        [Tooltip("Filled circle used for dots and markers.")]
        public Sprite uiCircleSprite;
        [Tooltip("9-sliced bar used as the background for labelled buttons. Null = DEVNIK pixel bar from Resources, then the rounded fill.")]
        public Sprite uiButtonSprite;

        [Header("Skins")]
        public SkinVisual[] skins;

        [Header("Fonts (text-as-art)")]
        [Tooltip("Custom typeface for the score / HUD. Null = TMP default.")]
        public TMP_FontAsset hudFont;
        [Tooltip("Font for the +score / x-combo popups.")]
        public TMP_FontAsset popupFont;
        [Tooltip("Font for menus and overlay screens.")]
        public TMP_FontAsset uiFont;
        [Tooltip("Font used to render the chess-glyph fallback when a piece has no sprite.")]
        public TMP_FontAsset glyphFont;

        // ── Resolvers ────────────────────────────────────────────────────────
        // These never throw and always return something usable: if a slot is
        // unset, the value is synthesized from the Core defaults.

        public PieceVisual GetPiece(PieceType type)
        {
            if (pieces != null)
                for (int i = 0; i < pieces.Length; i++)
                    if (pieces[i].type == type) return pieces[i];

            var v = new PieceVisual { type = type, sprite = null };
            ColorUtility.TryParseHtmlString(GameConfig.PieceColorHex(type), out v.tint);
            return v;
        }

        public bool TryGetSkin(string id, out SkinVisual skin)
        {
            if (skins != null)
                for (int i = 0; i < skins.Length; i++)
                    if (skins[i].id == id) { skin = skins[i]; return true; }
            skin = default;
            return false;
        }

        public Color GetSkinTint(string id)
        {
            if (TryGetSkin(id, out var s)) return s.tint;
            return BuiltInSkinTint(id);
        }

        // Built-in skin colours, so the player/leaderboard always tint correctly
        // even if the theme asset's `skins` array was never populated in the editor.
        public static Color BuiltInSkinTint(string id) => Skins.ColorOf(id);

        // ── Editor convenience ──────────────────────────────────────────────
        // Right-click the asset > "Populate Defaults From Core" to fill the piece
        // and skin lists from the enum + the ported defaults, so you only have to
        // drop in art rather than hand-enter every row.
        [ContextMenu("Populate Defaults From Core")]
        private void PopulateDefaults()
        {
            var types = (PieceType[])Enum.GetValues(typeof(PieceType));
            var pl = new List<PieceVisual>();
            foreach (var t in types)
            {
                ColorUtility.TryParseHtmlString(GameConfig.PieceColorHex(t), out var c);
                pl.Add(new PieceVisual { type = t, sprite = null, tint = c });
            }
            pieces = pl.ToArray();

            // Ported from the ACHIEVEMENTS skin definitions (+ the default gold).
            var defs = new (string id, string name, string hex)[]
            {
                ("gold",    "Classic Gold", "#F0C040"),
                ("emerald", "Emerald",      "#3CDC78"),
                ("crimson", "Crimson",      "#FF5A5A"),
                ("violet",  "Violet",       "#B070FF"),
                ("frost",   "Frost",        "#8FE0FF"),
                ("magma",   "Magma",        "#FF8A3C"),
            };
            var sl = new List<SkinVisual>();
            foreach (var d in defs)
            {
                ColorUtility.TryParseHtmlString(d.hex, out var c);
                sl.Add(new SkinVisual { id = d.id, displayName = d.name, tint = c, overrideSprite = null });
            }
            skins = sl.ToArray();
        }
    }
}
