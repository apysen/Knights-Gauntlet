using UnityEngine;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Goes on the piece prefab. Reads the theme and configures itself for a given
    // PieceType. This is the one place that decides "sprite or glyph?", so the
    // rest of the rendering never cares which one the art currently uses.
    //
    // Prefab layout (suggested):
    //   PieceView (this, + SpriteRenderer)   <- the piece sprite
    //   └─ Backing  (SpriteRenderer)         <- optional translucent square
    //   └─ Glyph    (TextMeshPro)            <- optional, for the no-sprite fallback
    [RequireComponent(typeof(SpriteRenderer))]
    public class PieceView : MonoBehaviour
    {
        public SpriteRenderer spriteRenderer;
        public SpriteRenderer backing;   // optional
        public TextMeshPro glyphLabel;   // optional (world-space TMP)

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Configure this piece's appearance from the theme.
        //   isPlayer + skinId: pass the equipped skin for the player's own piece
        //   so its tint (and optional sprite override) follow the active skin.
        public void Apply(GameTheme theme, PieceType type, bool isPlayer = false, string skinId = null)
        {
            var v = theme.GetPiece(type);
            Sprite sprite = v.sprite;
            Color tint = v.tint;

            if (isPlayer)
            {
                // The player keeps its skin colour for whatever form it's wearing.
                tint = theme.GetSkinTint(skinId ?? "gold");
                if (theme.TryGetSkin(skinId, out var skin) && skin.overrideSprite != null)
                    sprite = skin.overrideSprite;
            }

            // Translucent backing square behind the piece. The player's cell takes
            // the skin colour; enemies get a uniform faint cell (NOT their own
            // colour, so the board stays readable); traps are red.
            if (backing != null)
            {
                backing.enabled = true;
                if (theme.pieceBackingSprite != null) backing.sprite = theme.pieceBackingSprite;
                Color bc;
                if (isPlayer) { bc = tint; bc.a = 0.20f; }
                else if (type == PieceType.Trap) { bc = new Color(0.78f, 0.20f, 0.20f, 0.35f); }
                else { bc = new Color(0.31f, 0.31f, 0.55f, 0.30f); }
                backing.color = bc;
            }

            if (sprite != null)
            {
                // Art path: render the sprite, hide the glyph.
                spriteRenderer.enabled = true;
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = tint;
                if (glyphLabel != null) glyphLabel.enabled = false;
            }
            else
            {
                // Fallback path: render the chess glyph (the current look).
                spriteRenderer.enabled = false;
                if (glyphLabel != null)
                {
                    glyphLabel.enabled = true;
                    glyphLabel.text = GameConfig.Glyph(type);
                    glyphLabel.color = tint;
                    if (theme.glyphFont != null) glyphLabel.font = theme.glyphFont;
                }
            }
        }
    }
}
