using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // Content-sized dialogs stay centered inside the usable area, including
    // after dynamic text or button visibility changes. Home has its own layout.
    public sealed class UIScreenFit : MonoBehaviour
    {
        private RectTransform _rect;
        private Canvas _canvas;
        private Vector2 _lastSize;
        private Rect _lastSafe;
        private int _width, _height;
        private float _unit;
        private bool _dirty = true;
        private void OnEnable() { _dirty = true; }
        private void LateUpdate()
        {
            if (_rect == null) { _rect = (RectTransform)transform; _canvas = GetComponentInParent<Canvas>(); }
            if (_canvas == null) return;
            var size = new Vector2(LayoutUtility.GetPreferredWidth(_rect), LayoutUtility.GetPreferredHeight(_rect));
            var safe = Screen.safeArea;
            // The CanvasScaler can catch up a frame after a resize (e.g. folding a foldable),
            // so refit when the scale factor changes too, not just the screen size.
            float unit = Mathf.Max(.001f, _canvas.scaleFactor);
            if (!_dirty && size == _lastSize && safe == _lastSafe && _width == Screen.width && _height == Screen.height
                && Mathf.Approximately(unit, _unit)) return;
            _dirty = false; _lastSize = size; _lastSafe = safe; _width = Screen.width; _height = Screen.height; _unit = unit;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
            float usableWidth = Mathf.Max(1, safe.width / unit - 48);
            float usableHeight = Mathf.Max(1, safe.height / unit - 128);
            float scale = Mathf.Min(1, usableWidth / Mathf.Max(1, _rect.rect.width), usableHeight / Mathf.Max(1, _rect.rect.height));
            _rect.localScale = Vector3.one * scale;
            _rect.anchoredPosition = new Vector2((safe.center.x - Screen.width * .5f) / unit,
                (safe.center.y - Screen.height * .5f) / unit + 32);
        }
    }
}
