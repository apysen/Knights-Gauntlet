using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    public partial class UIManager
    {
        // ── Trap-mode shilling counter ───────────────────────────────────────
        // Bottom-left of the footer: the spinning 3D coin and this run's shillings.
        // Collected coins fly into it from the board, and the count only goes up
        // (with a punch) when each one lands, so the eye follows the coin home.
        private const float ShillingFlyDuration = .5f;
        private const float ShillingFlyArc = 90f;     // canvas units the flight bows upward
        private const float ShillingPunchDuration = .22f;
        private const float ShillingPunchScale = .35f;

        private const float ShillingFlightSize = 36f;  // flying coin's size in canvas units at scale 1
        private sealed class ShillingFlight { public RectTransform rt; public Vector2 from; public float age, startScale = 1f, faceOn = 1f; public int value; public bool active; }

        private RectTransform _hudShillingsRt, _hudShillingsIconRt;
        private TMP_Text _hudShillingsText;
        private RawImage _hudShillingsIcon;
        private readonly List<ShillingFlight> _shillingFlights = new List<ShillingFlight>();
        private int _shillingsInFlight;
        private float _shillingPunch = 1f;

        private void BuildShillingCounter()
        {
            _hudShillingsRt = HudRect(_hudFooter, "ShillingCounter", 0, 4, 150, 44);
            var icon = new GameObject("Coin", typeof(RectTransform), typeof(RawImage), typeof(SpinningCoinIcon));
            icon.transform.SetParent(_hudShillingsRt, false);
            _hudShillingsIconRt = (RectTransform)icon.transform;
            HudPlace(_hudShillingsIconRt, 0, 4, 36, 36);
            _hudShillingsIcon = icon.GetComponent<RawImage>();
            _hudShillingsText = HudText(_hudShillingsRt, "Count", "0", 20, UIFactory.Gold, 42, 0, 108, 44);
            _hudShillingsRt.gameObject.SetActive(false);
        }

        private void UpdateShillingCounter(bool visible)
        {
            if (_hudShillingsRt == null) return;
            _hudShillingsRt.gameObject.SetActive(visible);
            if (_gm.RunShillings < _shillingsInFlight) ResetShillingFlights(); // run restarted mid-flight
            AnimateShillingFlights(Time.unscaledDeltaTime);
            if (!visible) return;
            HudValue(_hudShillingsText, Mathf.Max(0, _gm.RunShillings - _shillingsInFlight).ToString("N0"));
            _shillingPunch = Mathf.MoveTowards(_shillingPunch, 1f, Time.unscaledDeltaTime / ShillingPunchDuration * ShillingPunchScale);
            _hudShillingsIconRt.localScale = _hudShillingsText.rectTransform.localScale = Vector3.one * _shillingPunch;
        }

        // A collect is three steps so the count waits for the coin: Reserve on the
        // collect frame (holds the value back from the counter), then either Fly when
        // the board coin hands itself over, or Land straight away if there is no coin
        // to follow (2D style, reduced effects).
        public bool ReserveShilling(int value)
        {
            if (_hudShillingsRt == null || !_hudShillingsRt.gameObject.activeInHierarchy) return false;
            _shillingsInFlight += value;
            return true;
        }

        public void LandShilling(int value)
        {
            _shillingsInFlight = Mathf.Max(0, _shillingsInFlight - value);
            _shillingPunch = 1f + ShillingPunchScale;
        }

        // Continues the board coin as a UI coin: starts at its screen position and size.
        public void FlyShilling(Vector3 world, float worldDiameter, float faceOn, int value)
        {
            var cam = Camera.main;
            var canvas = (RectTransform)_hudCanvas.transform;
            if (_hudShillingsRt == null || !_hudShillingsRt.gameObject.activeInHierarchy || cam == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, cam.WorldToScreenPoint(world), null, out var from))
            {
                LandShilling(value);
                return;
            }
            // On-screen diameter of the board coin, in canvas units.
            float pixels = Vector3.Distance(cam.WorldToScreenPoint(world),
                cam.WorldToScreenPoint(world + cam.transform.right * worldDiameter));
            var flight = GetShillingFlight();
            flight.active = true; flight.age = 0f; flight.value = value; flight.from = from;
            flight.startScale = pixels / _hudCanvas.scaleFactor / ShillingFlightSize;
            flight.faceOn = Mathf.Clamp(faceOn, .08f, 1f);
            flight.rt.gameObject.SetActive(true);
            flight.rt.SetAsLastSibling(); // over the footer
            AnimateShilling(flight, 0f);
        }

        private void AnimateShillingFlights(float dt)
        {
            for (int i = 0; i < _shillingFlights.Count; i++)
                if (_shillingFlights[i].active) AnimateShilling(_shillingFlights[i], dt);
        }

        private void AnimateShilling(ShillingFlight f, float dt)
        {
            f.age += dt;
            float t = Mathf.Clamp01(f.age / ShillingFlyDuration);
            if (t >= 1f || !_hudShillingsRt.gameObject.activeInHierarchy)
            {
                f.active = false;
                f.rt.gameObject.SetActive(false);
                LandShilling(f.value); // landed: count it and bump the counter
                return;
            }
            var canvas = (RectTransform)_hudCanvas.transform;
            Vector2 to = canvas.InverseTransformPoint(_hudShillingsIconRt.TransformPoint(_hudShillingsIconRt.rect.center));
            // Ease in: it lifts off gently, then accelerates into the counter.
            float e = t * t * (3f - 2f * t) * .3f + t * t * .7f;
            Vector2 p = Vector2.Lerp(f.from, to, e) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * ShillingFlyArc);
            f.rt.anchoredPosition = p;
            // From the board coin's on-screen size down to the counter icon's.
            float size = Mathf.Lerp(f.startScale, _hudShillingsIconRt.rect.width / ShillingFlightSize, e);
            // Finish the board coin's turn: its width opens out to face-on early in the flight.
            float turn = Mathf.SmoothStep(0f, 1f, t / .3f);
            f.rt.localScale = new Vector3(size * Mathf.Lerp(f.faceOn, 1f, turn), size, 1f);
        }

        private ShillingFlight GetShillingFlight()
        {
            for (int i = 0; i < _shillingFlights.Count; i++) if (!_shillingFlights[i].active) return _shillingFlights[i];
            var go = new GameObject("FlyingShilling", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(_hudCanvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(ShillingFlightSize, ShillingFlightSize);
            var image = go.GetComponent<RawImage>();
            image.texture = _hudShillingsIcon.texture; // the same spinning coin render
            image.raycastTarget = false;
            var f = new ShillingFlight { rt = rt };
            _shillingFlights.Add(f);
            return f;
        }

        // A restart or quit drops anything still in the air.
        private void ResetShillingFlights()
        {
            foreach (var f in _shillingFlights) { f.active = false; f.rt.gameObject.SetActive(false); }
            _shillingsInFlight = 0;
            _shillingPunch = 1f;
        }
    }
}
