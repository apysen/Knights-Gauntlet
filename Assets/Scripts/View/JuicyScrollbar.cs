using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // Life for a vertical Scrollbar (modern style). Put it on the Scrollbar's object and
    // call Setup once the handle exists.
    //   - At rest the bar is a slim, quiet pill. While the list moves (or the bar is
    //     held) it swells, brightens and glows, then settles back after a short pause.
    //   - Grabbing the handle punches it, with a tap of vibration.
    //   - Hitting the top or bottom flashes that end of the track. Paired with an
    //     Elastic ScrollRect the handle also squashes against the end on its own.
    //   - Grip notches in the middle of the handle show while it is active.
    [RequireComponent(typeof(Scrollbar))]
    public sealed class JuicyScrollbar : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Scrollbar _bar;
        private RectTransform _track, _handle, _fill, _glow, _flash, _grip;
        private Image _trackImg, _fillImg, _glowImg, _flashImg;
        private Image[] _notches;
        private Color _color, _trackColor;
        private float _thin, _thick, _last = -1f, _speed, _active, _idle, _punch, _flashT = 1f;
        private bool _down, _atEdge = true;

        // color: the handle's colour when active. thin/thick: widths at rest and in use.
        public void Setup(Color color, Color trackColor, float thin, float thick)
        {
            _bar = GetComponent<Scrollbar>();
            _track = (RectTransform)transform;
            _handle = _bar.handleRect;
            if (_handle == null) { enabled = false; return; }
            _color = color; _trackColor = trackColor; _thin = thin; _thick = thick;

            _trackImg = GetComponent<Image>();
            if (_trackImg != null) { _trackImg.sprite = UISprites.RoundedFill(); _trackImg.type = Image.Type.Sliced; }

            // The handle's own image stays as the (invisible) hit target and Scrollbar
            // transition graphic; what you see is a glow and a pill drawn inside it.
            var handleImg = _handle.GetComponent<Image>();
            if (handleImg != null) handleImg.color = new Color(1f, 1f, 1f, 0f);
            _bar.transition = Selectable.Transition.None;

            _glow = Child(_handle, "Glow", out _glowImg, UISprites.SoftGlow());
            _glow.offsetMin = new Vector2(-14f, -14f); _glow.offsetMax = new Vector2(14f, 14f);
            _fill = Child(_handle, "Fill", out _fillImg, UISprites.RoundedFill());
            _fillImg.type = Image.Type.Sliced;

            _grip = new GameObject("Grip", typeof(RectTransform)).GetComponent<RectTransform>();
            _grip.SetParent(_fill, false);
            _grip.anchorMin = _grip.anchorMax = _grip.pivot = new Vector2(.5f, .5f);
            _grip.sizeDelta = new Vector2(10f, 10f);
            _notches = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var n = new GameObject("Notch", typeof(RectTransform), typeof(Image));
                n.transform.SetParent(_grip, false);
                var nrt = (RectTransform)n.transform;
                nrt.anchorMin = new Vector2(.2f, .5f); nrt.anchorMax = new Vector2(.8f, .5f);
                nrt.sizeDelta = new Vector2(0f, 1.5f); nrt.anchoredPosition = new Vector2(0f, (i - 1) * 4f);
                _notches[i] = n.GetComponent<Image>(); _notches[i].raycastTarget = false;
            }

            // The end-of-track flash lives on the track so it stays put while the handle moves.
            _flash = Child(_track, "EdgeFlash", out _flashImg, UISprites.SoftGlow());
            _flash.anchorMin = _flash.anchorMax = new Vector2(.5f, 1f);
            _flash.offsetMin = _flash.offsetMax = Vector2.zero;
            _flash.sizeDelta = new Vector2(40f, 40f);
            _flashImg.color = Color.clear;
        }

        private static RectTransform Child(Transform parent, string name, out Image img, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; UIFactory.Stretch(rt);
            img = go.GetComponent<Image>(); img.sprite = sprite; img.raycastTarget = false;
            return rt;
        }

        public void OnPointerDown(PointerEventData e)
        {
            _down = true; _punch = 1f;
            Haptics.Play(Haptics.Select, Haptics.SelectAmp);
        }
        public void OnPointerUp(PointerEventData e) { _down = false; }

        private void OnDisable() { _down = false; _active = 0f; _last = -1f; _atEdge = true; }

        private void LateUpdate()
        {
            if (_fill == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            bool reduced = SaveData.IsReducedEffects();

            // How fast the bar is moving (value units per second, smoothed).
            float v = _bar.value;
            if (_last < 0f) _last = v;
            float inst = Mathf.Abs(v - _last) / Mathf.Max(dt, .0001f);
            _speed = Mathf.Lerp(_speed, inst, 1f - Mathf.Exp(-14f * dt));
            _last = v;

            bool moving = _down || _speed > .05f;
            _idle = moving ? 0f : _idle + dt;
            // Swell in fast; hold briefly after the list stops, then ease back.
            float target = moving || _idle < .5f ? 1f : 0f;
            _active = Mathf.MoveTowards(_active, target, dt * (target > _active ? 8f : 2.5f));
            float a = _active * _active * (3f - 2f * _active);

            // Edge hit: the value reaches an end while moving.
            bool edge = v <= .001f || v >= .999f;
            if (edge && !_atEdge && _speed > .15f && !reduced)
            {
                _flashT = 0f;
                // TopToBottom puts value 0 at the top; BottomToTop puts value 1 there.
                bool top = (_bar.direction == Scrollbar.Direction.TopToBottom) == (v < .5f);
                _flash.anchorMin = _flash.anchorMax = new Vector2(.5f, top ? 1f : 0f);
                _punch = Mathf.Max(_punch, .6f);
            }
            _atEdge = edge;

            float width = Mathf.Lerp(_thin, _thick, reduced ? 0f : a);
            _track.sizeDelta = new Vector2(width, _track.sizeDelta.y);
            // Corner radius of half the width keeps both ends perfectly round.
            float ppu = 16f / Mathf.Max(1f, width * .5f);
            if (_trackImg != null)
            {
                _trackImg.pixelsPerUnitMultiplier = ppu;
                _trackImg.color = new Color(_trackColor.r, _trackColor.g, _trackColor.b, _trackColor.a * Mathf.Lerp(.6f, 1f, a));
            }

            _punch = Mathf.Max(0f, _punch - dt / .3f);
            float p = reduced ? 0f : Mathf.Sin(_punch * Mathf.PI);
            _fill.localScale = new Vector3(1f + .35f * p, 1f - .06f * p, 1f);
            _fillImg.pixelsPerUnitMultiplier = ppu;
            var dim = Color.Lerp(_color, new Color(.55f, .6f, .7f), .55f);
            _fillImg.color = Color.Lerp(new Color(dim.r, dim.g, dim.b, .55f), _color, a);
            _glowImg.color = new Color(_color.r, _color.g, _color.b, reduced ? 0f : .45f * a + .3f * p);

            bool grip = _handle.rect.height > 34f && width > 7f;
            _grip.gameObject.SetActive(grip);
            if (grip)
            {
                var nc = new Color(0f, 0f, 0f, .45f * a);
                foreach (var n in _notches) n.color = nc;
            }

            if (_flashT < 1f)
            {
                _flashT = Mathf.Min(1f, _flashT + dt / .45f);
                float f = 1f - _flashT;
                _flash.sizeDelta = Vector2.one * Mathf.Lerp(70f, 30f, f);
                _flashImg.color = new Color(_color.r, _color.g, _color.b, .9f * f * f);
            }
        }
    }
}
