using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Uses the production PieceView, including skin materials and imported model orientation.
    // The texture is refreshed only when the appearance or camera angle changes.
    public sealed class MenuKnightPreview : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IEndDragHandler, IInitializePotentialDragHandler
    {
        public const float Fill = .84f;
        private GameManager _gm;
        private GameTheme _theme;
        private RawImage _image;
        private GameObject _stage;
        private PieceView _piece;
        private Camera _camera;
        private RenderTexture _texture;
        private string _skin;
        private Quaternion _angle;
        private float _yaw;
        private float _boardView;
        private Quaternion _inspectionRotation = Quaternion.identity;
        private bool _dragging;
        private int _pointerId;
        private Vector3 _previousTrackball;

        public void SetBoardView(float progress) => _boardView = Mathf.Clamp01(progress);

        public void Initialize(GameManager gm, GameTheme theme, RawImage image)
        {
            _gm = gm; _theme = theme; _image = image;
            _image.raycastTarget = true;
            _stage = new GameObject("MenuKnightStage");
            _stage.transform.position = new Vector3(1000, 0, 0);
            var model = new GameObject("EquippedKnight", typeof(SpriteRenderer), typeof(PieceView));
            model.transform.SetParent(_stage.transform, false);
            _piece = model.GetComponent<PieceView>();
            _piece.enabled = false; // Preview pieces do not contribute shadows to the gameplay board.
            _piece.spriteRenderer = model.GetComponent<SpriteRenderer>();
            var cameraObject = new GameObject("MenuKnightCamera", typeof(Camera));
            cameraObject.transform.SetParent(_stage.transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.enabled = false;
            _texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
                { name = "Equipped knight portrait", antiAliasing = 1 };
            _texture.Create();
            _image.texture = _texture;
        }

        private void LateUpdate()
        {
            if (_gm == null || _camera == null || _gm.MenuCamera == null) return;
            if (_gm.MenuTransitionActive) _dragging = false;
            if (!_dragging && _inspectionRotation != Quaternion.identity)
            {
                _inspectionRotation = Quaternion.Slerp(_inspectionRotation, Quaternion.identity,
                    1 - Mathf.Exp(-12 * Time.unscaledDeltaTime));
                if (Quaternion.Angle(_inspectionRotation, Quaternion.identity) < .15f)
                    _inspectionRotation = Quaternion.identity;
            }
            string skin = SaveData.GetSkin();
            // Models stand along -Z on the production board. A camera looking along
            // +Y presents them fully upright; blend to the real board camera on PLAY.
            var angle = Quaternion.Slerp(Quaternion.Euler(-90, 0, 0),
                _gm.MenuCamera.transform.rotation, _boardView);
            // A virtual trackball supplies pitch/yaw through the center and roll
            // around the rim. Orbiting the isolated camera keeps the model centered.
            angle *= Quaternion.Inverse(Quaternion.Slerp(_inspectionRotation, Quaternion.identity, _boardView));
            if (_skin == skin && _angle == angle && Mathf.Approximately(_yaw, _gm.playerPieceRotation)) return;
            _skin = skin; _angle = angle; _yaw = _gm.playerPieceRotation;
            _piece.Apply(_theme, PieceType.Knight, true, skin);
            _piece.SetModelRotation(_yaw);
            foreach (var child in _stage.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            _camera.CopyFrom(_gm.MenuCamera);
            _camera.enabled = false;
            _camera.cullingMask = 1 << 31;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.targetTexture = _texture;
            _camera.orthographic = true;
            _camera.aspect = 1;
            _camera.nearClipPlane = .01f; _camera.farClipPlane = 50;
            _camera.transform.SetPositionAndRotation(_stage.transform.position - angle * Vector3.forward * 15, angle);
            Rect bounds = ProjectedRect(_piece, _camera);
            Vector3 offset = _camera.transform.right * (bounds.center.x - 256) +
                _camera.transform.up * (bounds.center.y - 256);
            offset *= _camera.orthographicSize * 2 / 512;
            _camera.transform.position += offset;
            _camera.orthographicSize *= Mathf.Max(bounds.width, bounds.height) / (512 * Fill);
            // Production shaders may clip to board coordinates. This isolated preview has no board.
            foreach (var renderer in _piece.GetComponentsInChildren<Renderer>())
            {
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetFloat("_PieceClip", 0);
                renderer.SetPropertyBlock(properties);
            }
            _camera.Render();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_dragging || _gm == null || _gm.MenuTransitionActive ||
                eventData.button != PointerEventData.InputButton.Left) return;
            if (!TryTrackball(eventData, out var point, out var inside) || !inside) return;
            _dragging = true;
            _pointerId = eventData.pointerId;
            _previousTrackball = point;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData.pointerId != _pointerId || _gm.MenuTransitionActive) return;
            if (!TryTrackball(eventData, out var point, out _)) return;
            _inspectionRotation = (Quaternion.FromToRotation(_previousTrackball, point) * _inspectionRotation).normalized;
            _previousTrackball = point;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) _dragging = false;
        }

        public void OnEndDrag(PointerEventData eventData) => OnPointerUp(eventData);

        private bool TryTrackball(PointerEventData eventData, out Vector3 point, out bool inside)
        {
            point = Vector3.forward;
            inside = false;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
                eventData.pressEventCamera, out var local)) return false;
            float radius = Mathf.Min(rect.rect.width, rect.rect.height) * .5f;
            if (radius <= 0) return false;
            Vector2 xy = (local - rect.rect.center) / radius;
            inside = xy.sqrMagnitude <= 1;
            // Continue tracking outside the portrait when a captured finger leaves it.
            if (!inside) xy.Normalize();
            point = new Vector3(xy.x, xy.y, Mathf.Sqrt(Mathf.Max(0, 1 - xy.sqrMagnitude)));
            return true;
        }

        private void OnDisable()
        {
            _dragging = false;
            _inspectionRotation = Quaternion.identity;
        }

        private void OnApplicationFocus(bool focused) { if (!focused) _dragging = false; }
        private void OnApplicationPause(bool paused) { if (paused) _dragging = false; }

        public static Rect ProjectedRect(PieceView piece, Camera camera)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;
            foreach (var renderer in piece.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                // Production renderers have deliberately enlarged culling bounds for the
                // rolling board shader. Measure the real mesh for framing and handoff.
                var filter = renderer.GetComponent<MeshFilter>();
                bool local = filter != null && filter.sharedMesh != null;
                var b = local ? filter.sharedMesh.bounds : renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    if (local) point = renderer.transform.TransformPoint(point);
                    Vector2 screen = camera.WorldToScreenPoint(point);
                    min = Vector2.Min(min, screen); max = Vector2.Max(max, screen); any = true;
                }
            }
            if (!any) return new Rect((Vector2)camera.WorldToScreenPoint(piece.transform.position) - Vector2.one * 32, Vector2.one * 64);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void OnDestroy()
        {
            if (_stage != null) Destroy(_stage);
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
        }
    }
}
