using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KnightsGauntlet.View
{
    // Separate environment raster: UI, pieces, pickups and combat feedback stay
    // on the original camera. No materials, meshes or simulation positions change.
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class EnvironmentPixelFilter : MonoBehaviour
    {
        [Range(160,640)] public int TargetWidth = 320;
        [Range(0,1)] public float OutlineStrength = .20f;
        [Range(0,1)] public float HighlightStrength = .08f;
        [Range(.02f,.5f)] public float EdgeThreshold = .12f;
        public bool FilterEnabled = true;
        [Min(0)] public int EnvironmentRendererIndex = 1;
        public Vector2Int RasterSize => _raster == null ? Vector2Int.zero : new Vector2Int(_raster.width,_raster.height);
        public RTHandle DepthTarget { get; private set; }
        public bool OwnsCamera(Camera camera) => camera == _environment;

        // Reserved presentation-only layers, not gameplay physics layers.
        private const int ForegroundLayer = 27, CompositeLayer = 28;
        private Camera _source, _environment;
        private GameView _foreground;
        private GameManager _manager;
        private GameObject _plate;
        private Material _material;
        private RenderTexture _raster;
        private RenderTexture _depthRaster;
        private int _originalMask;
        private bool _active;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly Dictionary<GameObject,int> _originalLayers = new Dictionary<GameObject,int>();

        public void Initialize(Camera source, GameView foreground, GameManager manager)
        {
            _source=source; _foreground=foreground; _manager=manager;
            _originalMask=source.cullingMask;
            Synchronize();
        }

        private void LateUpdate() => Synchronize();

        // Also called by deterministic image captures after changing target size.
        public void Synchronize()
        {
            if (_source == null || _foreground == null) return;
            bool wanted=FilterEnabled && (_manager == null || !_manager.Legacy2DActive);
            if (!wanted) { Restore(); return; }
            if (!EnsureResources()) return;
            _foreground.GetComponentsInChildren(true,_renderers);
            foreach (var renderer in _renderers)
            {
                var go=renderer.gameObject;
                if (!_originalLayers.ContainsKey(go)) _originalLayers.Add(go,go.layer);
                go.layer=ForegroundLayer;
            }
            // Pixel blocks are whole display pixels. Resize reallocates only if
            // the integer-scaled raster dimensions actually change.
            int width=Mathf.Max(1,_source.pixelWidth), height=Mathf.Max(1,_source.pixelHeight);
            int scale=Mathf.Max(1,Mathf.RoundToInt((float)width/TargetWidth));
            int w=Mathf.CeilToInt((float)width/scale), h=Mathf.CeilToInt((float)height/scale);
            if (_raster == null || _raster.width!=w || _raster.height!=h)
            {
                ReleaseRaster();
                _raster=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32)
                { name="Pixel environment",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,
                  antiAliasing=1,useMipMap=false,autoGenerateMips=false };
                _raster.Create(); _material.mainTexture=_raster;
                _depthRaster=new RenderTexture(w,h,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear)
                { name="Pixel environment depth",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,
                  antiAliasing=1,useMipMap=false };
                _depthRaster.Create(); _material.SetTexture("_DepthTex",_depthRaster);
                DepthTarget=RTHandles.Alloc(_depthRaster);
            }
            _environment.CopyFrom(_source);
            _environment.transform.SetPositionAndRotation(_source.transform.position,_source.transform.rotation);
            _environment.targetTexture=_raster;
            _environment.aspect=_source.aspect;
            _environment.cullingMask=_originalMask & ~((1<<ForegroundLayer)|(1<<CompositeLayer)|(1<<5)|(1<<31));
            _environment.depth=_source.depth-1;
            _environment.allowMSAA=false; _environment.allowHDR=false;
            _environment.enabled=true;
            // Only the offscreen scene uses the depth-capable forward renderer;
            // the foreground camera retains the project's original 2D renderer.
            var cameraData=_environment.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(EnvironmentRendererIndex);
            cameraData.requiresDepthTexture=true;
            _source.cullingMask=(_originalMask & ((1<<ForegroundLayer)|(1<<5))) | (1<<CompositeLayer);
            _plate.transform.SetPositionAndRotation(_source.transform.position+_source.transform.forward*2,_source.transform.rotation);
            _plate.SetActive(true);
            _material.SetVector("_RasterSize",new Vector4(w,h,1f/w,1f/h));
            _material.SetVector("_OutputSize",new Vector4(width,height,scale,0));
            _material.SetFloat("_Outline",OutlineStrength);
            _material.SetFloat("_Highlight",HighlightStrength);
            _material.SetFloat("_Threshold",EdgeThreshold);
            _active=true;
        }

        private bool EnsureResources()
        {
            if (_material != null) return true;
            var shader=Resources.Load<Shader>("EnvironmentPixelComposite");
            if (shader == null || !shader.isSupported) return false;
            _material=new Material(shader) { name="Environment pixel composite" };
            var cameraObject=new GameObject("Environment Pixel Camera");
            cameraObject.transform.SetParent(transform,false);
            _environment=cameraObject.AddComponent<Camera>();
            _environment.enabled=false;
            _plate=GameObject.CreatePrimitive(PrimitiveType.Quad);
            _plate.name="Pixel Environment Composite";
            _plate.transform.SetParent(transform,false); _plate.layer=CompositeLayer;
            Destroy(_plate.GetComponent<Collider>());
            var renderer=_plate.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            return true;
        }

        private void Restore()
        {
            if (!_active) return;
            if (_source != null) _source.cullingMask=_originalMask;
            foreach (var pair in _originalLayers) if (pair.Key != null) pair.Key.layer=pair.Value;
            _originalLayers.Clear();
            if (_environment != null) _environment.enabled=false;
            if (_plate != null) _plate.SetActive(false);
            _active=false;
        }

        private void ReleaseRaster()
        {
            if (_raster == null) return;
            if (_environment != null) _environment.targetTexture=null;
            _raster.Release(); Dispose(_raster); _raster=null;
            DepthTarget?.Release(); DepthTarget=null;
            if (_depthRaster != null) { _depthRaster.Release(); Dispose(_depthRaster); _depthRaster=null; }
        }
        private void OnDisable()
        {
            Restore(); ReleaseRaster();
        }
        private void OnDestroy()
        {
            Restore(); ReleaseRaster();
            if (_material != null) Dispose(_material);
            if (_plate != null) Dispose(_plate);
            if (_environment != null) Dispose(_environment.gameObject);
        }
        private static void Dispose(Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
