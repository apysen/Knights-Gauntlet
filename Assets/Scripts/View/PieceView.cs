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

        // Cache of the last-applied appearance inputs. Sync() calls Apply() every frame
        // for every pooled piece, but the look only depends on these four values, so we
        // skip the (TMP mesh-rebuilding) rework unless one of them changed.
        private GameTheme _aTheme;
        private PieceType _aType;
        private bool _aIsPlayer;
        private string _aSkinId;
        private bool _aValid;

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Configure this piece's appearance from the theme.
        //   isPlayer + skinId: pass the equipped skin for the player's own piece
        //   so its tint (and optional sprite override) follow the active skin.
        public void Apply(GameTheme theme, PieceType type, bool isPlayer = false, string skinId = null)
        {
            // Nothing that affects the look changed since the last Apply, so skip the
            // sprite/glyph rework (and, for traps, the per-frame ForceMeshUpdate).
            if (_aValid && _aTheme == theme && _aType == type && _aIsPlayer == isPlayer && _aSkinId == skinId)
                return;
            _aTheme = theme; _aType = type; _aIsPlayer = isPlayer; _aSkinId = skinId; _aValid = true;

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

            // Translucent backing square behind the piece. Enemies get a uniform faint
            // cell (NOT their own colour, so the board stays readable); traps are red.
            // The PLAYER gets NO square. Its "shine" is the radial glow alone.
            if (backing != null)
            {
                if (isPlayer)
                {
                    backing.enabled = false;
                }
                else
                {
                    backing.enabled = true;
                    if (theme.pieceBackingSprite != null) backing.sprite = theme.pieceBackingSprite;
                    Color bc = type == PieceType.Trap
                        ? new Color(0.784f, 0.196f, 0.196f, 0.2f)   // muted red cell (web is .35, but our X reads heavier)
                        : new Color(0.314f, 0.314f, 0.706f, 0.35f);  // web rgba(80,80,180,.35)
                    backing.color = bc;
                }
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
                    glyphLabel.color = tint;
                    var rt = glyphLabel.rectTransform;
                    rt.localPosition = Vector3.zero;

                    if (type == PieceType.Trap)
                    {
                        // Trap marker: the "X" from the Thaleah pixel font (matches the
                        // UI), a bit smaller so a field of traps isn't overwhelming. The
                        // pixel X reads much heavier than the web's thin ✕, so drop its
                        // opacity so a field of traps stays translucent like the web.
                        glyphLabel.color = new Color(tint.r, tint.g, tint.b, 1f);
                        glyphLabel.text = "X";
                        glyphLabel.font = theme.uiFont != null ? theme.uiFont : theme.glyphFont;
                        float s = 0.6f;
                        rt.localScale = new Vector3(s, s, 1f);
                        // Center the glyph's ink in the cell regardless of the font's
                        // ascent/descent (Thaleah caps aren't centered by TMP alone).
                        glyphLabel.ForceMeshUpdate();
                        var c = glyphLabel.textBounds.center;
                        rt.localPosition = new Vector3(-c.x * s, -c.y * s, 0f);
                    }
                    else
                    {
                        // Chess pieces: the pixel chess font, ~65% so they sit small in
                        // the cell (matches the web look) with generous margins.
                        glyphLabel.text = GameConfig.Glyph(type);
                        if (theme.glyphFont != null) glyphLabel.font = theme.glyphFont;
                        rt.localScale = new Vector3(0.65f, 0.65f, 1f);
                    }
                }
            }
        }
    }
}
