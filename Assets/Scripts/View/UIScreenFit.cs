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
        private bool _dirty = true;
        private void OnEnable() { _dirty = true; }
        private void LateUpdate()
        {
            if (_rect == null) { _rect = (RectTransform)transform; _canvas = GetComponentInParent<Canvas>(); }
            if (_canvas == null) return;
            var size = new Vector2(LayoutUtility.GetPreferredWidth(_rect), LayoutUtility.GetPreferredHeight(_rect));
            var safe = Screen.safeArea;
            if (!_dirty && size == _lastSize && safe == _lastSafe && _width == Screen.width && _height == Screen.height) return;
            _dirty = false; _lastSize = size; _lastSafe = safe; _width = Screen.width; _height = Screen.height;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
            float unit = Mathf.Max(.001f, _canvas.scaleFactor);
            float usableWidth = Mathf.Max(1, safe.width / unit - 48);
            float usableHeight = Mathf.Max(1, safe.height / unit - 128);
            float scale = Mathf.Min(1, usableWidth / Mathf.Max(1, _rect.rect.width), usableHeight / Mathf.Max(1, _rect.rect.height));
            _rect.localScale = Vector3.one * scale;
            _rect.anchoredPosition = new Vector2((safe.center.x - Screen.width * .5f) / unit,
                (safe.center.y - Screen.height * .5f) / unit + 32);
        }
    }
}
