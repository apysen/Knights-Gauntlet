using UnityEngine;

// Optional art-proof component. The three body drawings stay independent from
// the GPU slash, which is drawn below the knight on the same native pixel grid.
[DisallowMultipleComponent]
public sealed class PixelKnightSlash : MonoBehaviour
{
    [Range(16,256)] public int PixelSize = 64;
    public Material ShaderTemplate;
    public SpriteRenderer EffectRenderer;
    public Animator BodyAnimator;
    public float WindupDuration = .07f;
    public float EffectDuration = .22f;
    public float CaptureDuration = .25f;

    private Material _material;
    private RenderTexture _target;
    private Sprite _canvas;
    private MaterialPropertyBlock _properties;
    private float _captureElapsed = -1;
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int GridId = Shader.PropertyToID("_GridSize");
    private static readonly int TextureId = Shader.PropertyToID("_MainTex");

    private void LateUpdate()
    {
        if (BodyAnimator == null) BodyAnimator = GetComponent<Animator>();
        if (BodyAnimator == null || !BodyAnimator.enabled)
        {
            RenderAtCaptureTime(-1);
            return;
        }
        var state = BodyAnimator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Capture")) _captureElapsed = state.normalizedTime * CaptureDuration;
        else if (_captureElapsed >= 0) _captureElapsed += Time.deltaTime;
        if (_captureElapsed > WindupDuration + EffectDuration) _captureElapsed = -1;
        RenderAtCaptureTime(_captureElapsed);
    }

    public void PlayCapture()
    {
        if (BodyAnimator == null) BodyAnimator = GetComponent<Animator>();
        if (BodyAnimator != null) BodyAnimator.Play("Capture",0,0);
        RenderAtCaptureTime(-1);
    }

    public void StopCapture()
    {
        _captureElapsed = -1;
        if (BodyAnimator != null && BodyAnimator.isActiveAndEnabled) BodyAnimator.Play("Idle",0,0);
        RenderAtCaptureTime(-1);
    }

    // Shared by playback and deterministic editor captures; no camera movement,
    // gameplay pause, input delay, or shared-material mutation is involved.
    public void RenderAtCaptureTime(float seconds)
    {
        if (EffectRenderer == null) return;
        float p = (seconds-WindupDuration) / Mathf.Max(.001f,EffectDuration);
        bool visible = seconds >= WindupDuration && p > 0 && p < 1;
        EffectRenderer.enabled = visible;
        if (!visible || !EnsureResources()) return;
        _material.SetFloat(ProgressId,p);
        _material.SetFloat(GridId,PixelSize);
        var active = RenderTexture.active;
        bool srgb = GL.sRGBWrite;
        try
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(Texture2D.blackTexture,_target,_material,0);
        }
        finally { RenderTexture.active = active; GL.sRGBWrite = srgb; }
        _properties.SetTexture(TextureId,_target);
        EffectRenderer.SetPropertyBlock(_properties);
    }

    private bool EnsureResources()
    {
        if (ShaderTemplate == null) { EffectRenderer.enabled=false; return false; }
        if (_target != null && _target.width == PixelSize) return true;
        ReleaseResources();
        _material = new Material(ShaderTemplate) { name = "Pixel knight slash (instance)" };
        _target = new RenderTexture(PixelSize,PixelSize,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB)
        {
            name = "Pixel knight slash " + PixelSize,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            antiAliasing = 1
        };
        _target.Create();
        var white = Texture2D.whiteTexture;
        _canvas = Sprite.Create(white,new Rect(0,0,white.width,white.height),
            new Vector2(.5f,.22f),white.width,0,SpriteMeshType.FullRect);
        _canvas.name = "Pixel slash canvas";
        EffectRenderer.sprite = _canvas;
        EffectRenderer.enabled = true;
        _properties = new MaterialPropertyBlock();
        return true;
    }

    private void OnDisable() => ReleaseResources();
    private void OnDestroy() => ReleaseResources();

    private void ReleaseResources()
    {
        if (EffectRenderer != null)
        {
            EffectRenderer.enabled=false;
            EffectRenderer.SetPropertyBlock(null);
            EffectRenderer.sprite=null;
        }
        if (_target != null) { _target.Release(); Dispose(_target); _target=null; }
        if (_material != null) { Dispose(_material); _material=null; }
        if (_canvas != null) { Dispose(_canvas); _canvas=null; }
    }

    private static void Dispose(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
