using UnityEngine;

namespace KnightsGauntlet.View
{
    public partial class GameManager
    {
        public Camera MenuCamera => cam;
        public bool MenuTransitionActive { get; set; }
        public Rect MenuPlayerRect => _view.MenuPlayerRect(cam);
        public void SetMenuPlayerVisible(bool visible) => _view.SetMenuPlayerVisible(visible);

        private void ApplyPresentationTheme()
        {
            // Fill in fonts the theme asset leaves empty (explicit == null: Unity fake-null).
            if (theme.uiFont == null) theme.uiFont = UIFactory.ComicFont();
            if (theme.hudFont == null) theme.hudFont = UIFactory.ComicFont();
            if (theme.popupFont == null) theme.popupFont = UIFactory.ComicFont();
            theme.glyphFont = UIFactory.ChessFont();
        }
    }
}
