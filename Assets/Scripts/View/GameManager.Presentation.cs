using UnityEngine;

namespace KnightsGauntlet.View
{
    public partial class GameManager
    {
        private GameTheme _modernTheme;
        public Camera MenuCamera => cam;
        public bool MenuTransitionActive { get; set; }
        public Rect MenuPlayerRect => _view.MenuPlayerRect(cam);
        public void SetMenuPlayerVisible(bool visible) => _view.SetMenuPlayerVisible(visible);

        private void ApplyPresentationTheme()
        {
            UIFactory.LegacyStyle = _legacy2D;
            theme.comicStyle = !_legacy2D;
            theme.uiFont = _legacy2D ? UIFactory.PixelFont() : _modernTheme.uiFont ?? UIFactory.ComicFont();
            theme.hudFont = _legacy2D ? UIFactory.PixelFont() : _modernTheme.hudFont ?? UIFactory.ComicFont();
            theme.popupFont = _legacy2D ? UIFactory.PixelFont() : _modernTheme.popupFont ?? UIFactory.ComicFont();
            theme.glyphFont = _legacy2D ? UIFactory.LegacyChessFont() : UIFactory.ChessFont();
            theme.cellColorA = _legacy2D ? new Color(.176f,.176f,.306f) : _modernTheme.cellColorA;
            theme.cellColorB = _legacy2D ? new Color(.118f,.118f,.208f) : _modernTheme.cellColorB;
            theme.hintColor = _legacy2D ? new Color(.941f,.753f,.251f,.25f) : _modernTheme.hintColor;
            theme.threatFill = _legacy2D ? new Color(.353f,.078f,.078f,.55f) : _modernTheme.threatFill;
            theme.threatStroke = _legacy2D ? new Color(.588f,.157f,.157f,.55f) : _modernTheme.threatStroke;
            theme.pieces = _legacy2D ? System.Array.Empty<GameTheme.PieceVisual>() : _modernTheme.pieces;
            theme.uiPanelSprite = _legacy2D ? null : _modernTheme.uiPanelSprite;
            theme.uiBorderSprite = _legacy2D ? null : _modernTheme.uiBorderSprite;
            theme.uiButtonSprite = _legacy2D ? null : _modernTheme.uiButtonSprite;
        }
    }
}
