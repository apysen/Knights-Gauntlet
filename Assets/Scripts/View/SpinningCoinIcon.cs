using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // A 3D shilling for the UI: the coin rendered by a tiny isolated camera into a
    // texture (overlay canvases cannot draw meshes). Same offscreen-stage approach
    // as MenuKnightPreview, each icon on its own spot.
    //
    // By default it spins steadily (the in-run HUD counter). EnableSwing() makes it
    // the main menu wallet coin: it sways back and forth, and a horizontal swipe
    // flicks it into a spin that winds down and settles back into the sway.
    [RequireComponent(typeof(RawImage))]
    public sealed class SpinningCoinIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float SpinSpeed = 50f;        // steady spin, degrees per second
        private const float SwingAmplitude = 35f;   // sway either side of face-on, degrees
        private const float SwingPeriod = 2.6f;     // seconds per full sway
        private const float SwipeDegrees = 900f;    // degrees turned by a swipe across the whole screen
        private const float MaxFlick = 2000f;       // fastest flick spin, degrees per second
        private const float FlickFriction = 1.6f;   // higher stops a flick sooner
        private const float SettleSpeed = 5f;       // how quickly it eases back into the sway
        private const int TextureSize = 96;
        private const int StageLayer = 31;   // shared with MenuKnightPreview; stages are far apart
        private static int _stages; // each icon gets its own stage so cameras never see a neighbour

        private GameObject _stage;
        private Transform _coin;
        private Camera _camera;
        private RenderTexture _texture;
        private float _angle, _velocity, _swingTime, _dragVelocity, _lastDragTime;
        private bool _swing, _dragging;

        private void Awake()
        {
            _stage = new GameObject("CoinIconStage");
            _stage.transform.position = new Vector3(-1000f - 20f * _stages++, 0f, 0f);
            var coin = CoinMesh.Create("IconShilling", _stage.transform, false);
            coin.gameObject.layer = StageLayer;
            _coin = coin.transform;

            var cameraObject = new GameObject("CoinIconCamera", typeof(Camera));
            cameraObject.transform.SetParent(_stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f); // looks along +Z at the coin
            _camera = cameraObject.GetComponent<Camera>();
            _camera.enabled = false; // rendered by hand below, only while the icon is visible
            _camera.orthographic = true;
            _camera.orthographicSize = CoinMesh.Diameter * .53f;
            _camera.aspect = 1f;
            _camera.nearClipPlane = .1f; _camera.farClipPlane = 20f;
            _camera.cullingMask = 1 << StageLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.allowHDR = false; _camera.allowMSAA = false;

            _texture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
                { name = "Coin icon", antiAliasing = 4 };
            _texture.Create();
            _camera.targetTexture = _texture;
            var image = GetComponent<RawImage>();
            image.texture = _texture;
            image.raycastTarget = false;
        }

        // Main menu: sway instead of spin, and accept swipes. The coin is small, so
        // the touch area reaches well past its edges.
        public void EnableSwing()
        {
            _swing = true;
            var image = GetComponent<RawImage>();
            image.raycastTarget = true;
            image.raycastPadding = new Vector4(-18f, -18f, -18f, -18f);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_swing) return;
            _dragging = true; _velocity = 0f; _dragVelocity = 0f;
            _lastDragTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            // The coin follows the finger, and the recent speed is kept for the flick.
            float degrees = eventData.delta.x / Mathf.Max(1f, Screen.width) * SwipeDegrees;
            _angle += degrees;
            float now = Time.unscaledTime;
            float dt = Mathf.Max(now - _lastDragTime, .008f);
            _lastDragTime = now;
            _dragVelocity = Mathf.Lerp(_dragVelocity, degrees / dt, .5f);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            // A finger that stopped before lifting places the coin rather than flicking it.
            bool held = Time.unscaledTime - _lastDragTime > .12f;
            _velocity = held ? 0f : Mathf.Clamp(_dragVelocity, -MaxFlick, MaxFlick);
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            float dt = Time.unscaledDeltaTime;
            if (!_swing) _angle += SpinSpeed * dt;
            else if (!_dragging)
            {
                _swingTime += dt;
                if (Mathf.Abs(_velocity) > 40f)
                {
                    // Flicked: spin freely and wind down.
                    _angle += _velocity * dt;
                    _velocity *= Mathf.Exp(-FlickFriction * dt);
                }
                else
                {
                    // Ease into the sway from wherever the spin left it, the short way round.
                    _velocity = 0f;
                    float sway = SwingAmplitude * Mathf.Sin(_swingTime * Mathf.PI * 2f / SwingPeriod);
                    _angle += Mathf.DeltaAngle(_angle, sway) * (1f - Mathf.Exp(-SettleSpeed * dt));
                }
            }
            _angle = Mathf.Repeat(_angle + 180f, 360f) - 180f;
            // Face the camera (coin axis along -Z), then turn about the vertical.
            _coin.localRotation = Quaternion.AngleAxis(_angle, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
            _camera.Render();
        }

        private void OnDestroy()
        {
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
            if (_stage != null) Destroy(_stage);
        }
    }
}
