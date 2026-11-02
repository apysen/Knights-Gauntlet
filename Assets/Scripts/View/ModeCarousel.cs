using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // A swipeable, snapping row of cards (the run-options mode picker). The centred card
    // is full size and lit; the others sit to the side, smaller, dimmer and tilted, tucked
    // behind it. Drag to scroll (it follows the finger, then springs to the nearest card,
    // or the next one on a flick), tap a side card to bring it in, or use Next/Previous.
    // OnSelected fires as soon as a new card crosses the centre.
    public sealed class ModeCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public float Spacing = 214f;      // canvas units between card centres
        public Action<int> OnSelected;
        public Func<int, float> ExtraScale; // per-card multiplier (selection punch), applied in Layout

        private readonly List<RectTransform> _cards = new List<RectTransform>();
        private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();
        private float _pos, _vel, _dragVel;
        private int _index, _settled = -1;
        private bool _dragging;

        public int Index => _index;
        public float Position => _pos;
        public IReadOnlyList<RectTransform> Cards => _cards;

        public void Add(RectTransform card)
        {
            card.SetParent(transform, false);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            var group = card.GetComponent<CanvasGroup>();
            if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
            int i = _cards.Count;
            // Tapping a side card brings it to the centre.
            var tap = card.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => { if (!_dragging && i != _index) Go(i); });
            _cards.Add(card); _groups.Add(group);
            Layout();
        }

        public void Next() => Go(Mathf.Min(_index + 1, _cards.Count - 1));
        public void Previous() => Go(Mathf.Max(_index - 1, 0));

        // Select without animation or callback (restoring the saved choice).
        public void SetIndex(int i, bool instant)
        {
            _index = Mathf.Clamp(i, 0, Mathf.Max(0, _cards.Count - 1));
            if (instant) { _pos = _index; _vel = 0f; _settled = _index; Layout(); }
        }

        private void Go(int i) { _index = Mathf.Clamp(i, 0, _cards.Count - 1); }

        public void OnBeginDrag(PointerEventData e) { _dragging = true; _dragVel = 0f; _vel = 0f; }

        public void OnDrag(PointerEventData e)
        {
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            float delta = -e.delta.x / scale / Spacing;
            // Rubber-band past the ends.
            if (_pos + delta < 0f || _pos + delta > _cards.Count - 1) delta *= .35f;
            _pos += delta;
            float dt = Mathf.Max(Time.unscaledDeltaTime, .008f);
            _dragVel = Mathf.Lerp(_dragVel, delta / dt, .5f);
        }

        public void OnEndDrag(PointerEventData e)
        {
            // A flick goes one card further; otherwise snap to the nearest.
            int target = Mathf.RoundToInt(_pos);
            if (Mathf.Abs(_dragVel) > 1.2f && target == _index) target += (int)Mathf.Sign(_dragVel);
            Go(target);
            _vel = _dragVel;
            // Leave the drag flag up for a frame so the release doesn't count as a tap.
            Invoke(nameof(EndDrag), 0f);
        }
        private void EndDrag() => _dragging = false;

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (!_dragging)
            {
                // Critically-damped-ish spring toward the selected card, with a hint of overshoot.
                _vel += (_index - _pos) * 120f * dt;
                _vel *= Mathf.Exp(-14f * dt);
                _pos += _vel * dt;
                if (Mathf.Abs(_index - _pos) < .002f && Mathf.Abs(_vel) < .01f) { _pos = _index; _vel = 0f; }
            }
            // Select as soon as a card crosses the centre (even mid-swipe), not when the spring
            // finally comes to rest, so the mode and its heading switch the moment it arrives.
            int nearest = Mathf.Clamp(Mathf.RoundToInt(_pos), 0, Mathf.Max(0, _cards.Count - 1));
            if (_cards.Count > 0 && nearest != _settled) { _settled = nearest; OnSelected?.Invoke(nearest); }
            Layout();
        }

        private void Layout()
        {
            bool reduced = SaveData.IsReducedEffects();
            // Draw the nearest cards last so the centred one sits on top.
            var order = new List<int>();
            for (int i = 0; i < _cards.Count; i++) order.Add(i);
            order.Sort((a, b) => Mathf.Abs(b - _pos).CompareTo(Mathf.Abs(a - _pos)));
            for (int k = 0; k < order.Count; k++) _cards[order[k]].SetSiblingIndex(k);
            for (int i = 0; i < _cards.Count; i++)
            {
                float d = i - _pos, ad = Mathf.Min(1.5f, Mathf.Abs(d));
                var c = _cards[i];
                c.anchoredPosition = new Vector2(d * Spacing * (1f - .12f * ad), -14f * ad);
                c.localScale = Vector3.one * (1f - .2f * ad) * (ExtraScale != null ? ExtraScale(i) : 1f);
                c.localRotation = reduced ? Quaternion.identity : Quaternion.Euler(0f, 0f, -6f * Mathf.Clamp(d, -1f, 1f));
                _groups[i].alpha = 1f - .5f * Mathf.Min(1f, ad);
            }
        }
    }
}
