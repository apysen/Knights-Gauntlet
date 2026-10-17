using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace KnightsGauntlet.View
{
    // Preserve offscreen scene depth before the render graph releases it, so
    // the sharp foreground respects the rolling crest and scenery occlusion.
    public sealed class EnvironmentPixelDepthFeature : ScriptableRendererFeature
    {
        private Material _material;
        private DepthPass _pass;
        public override void Create()
        {
            CoreUtils.Destroy(_material);
            _material=CoreUtils.CreateEngineMaterial(Resources.Load<Shader>("EnvironmentPixelComposite"));
            _pass=new DepthPass(_material);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var filter=data.cameraData.camera.GetComponentInParent<EnvironmentPixelFilter>();
            if (filter != null && filter.OwnsCamera(data.cameraData.camera) && filter.DepthTarget != null)
                renderer.EnqueuePass(_pass);
        }
        protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);
        private sealed class DepthPass : ScriptableRenderPass
        {
            private readonly Material _copy;
            private sealed class PassData { public TextureHandle source; public Material material; }
            public DepthPass(Material copy)
            {
                _copy=copy;
                renderPassEvent=RenderPassEvent.AfterRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var camera=frameData.Get<UniversalCameraData>().camera;
                var filter=camera.GetComponentInParent<EnvironmentPixelFilter>();
                if (filter == null || filter.DepthTarget == null) return;
                var resources=frameData.Get<UniversalResourceData>();
                var destination=graph.ImportTexture(filter.DepthTarget);
                var source=resources.cameraDepthTexture;
                if (!source.IsValid()) return;
                using (var builder=graph.AddRasterRenderPass<PassData>("Preserve pixel environment occlusion",out var pass))
                {
                    pass.source=source; pass.material=_copy;
                    builder.UseTexture(source,AccessFlags.Read);
                    builder.SetRenderAttachment(destination,0);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data,RasterGraphContext context)=>
                        Blitter.BlitTexture(context.cmd,data.source,new Vector4(1,1,0,0),data.material,1));
                }
            }
        }
    }
}
