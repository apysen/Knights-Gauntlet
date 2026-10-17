Shader "KnightsGauntlet/Environment Pixel Composite"
{
    Properties { _MainTex("Environment",2D)="black"{} _DepthTex("Environment depth",2D)="white"{} }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite On ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_DepthTex); SAMPLER(sampler_DepthTex);
            float4 _RasterSize,_OutputSize;
            float _Outline,_Highlight,_Threshold;
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=float4(v.positionOS.xy*2,0,1); o.uv=v.uv; return o;
            }
            float Luma(float3 c) { return dot(c,float3(.2126,.7152,.0722)); }
            float3 Read(float2 uv)
            {
                // Camera render targets on top-origin graphics APIs are flipped
                // relative to the screen-space quad's mesh UVs.
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y=1-uv.y;
                #endif
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb;
            }
            struct Output { half4 color:SV_Target; float depth:SV_Depth; };
            Output Frag(Varyings i)
            {
                // Whole output-pixel blocks, no bilinear upscaling or soft alpha.
                float2 pixel=floor(i.uv*_OutputSize.xy/_OutputSize.z);
                float2 uv=(pixel+.5)*_RasterSize.zw;
                float3 c=Read(uv);
                float l=Luma(c), up=Luma(Read(uv+float2(0,_RasterSize.w)));
                float down=Luma(Read(uv-float2(0,_RasterSize.w)));
                float left=Luma(Read(uv-float2(_RasterSize.z,0)));
                float right=Luma(Read(uv+float2(_RasterSize.z,0)));
                // Restrained color discontinuity accents work with the existing
                // procedural materials; they don't require depth-normal passes.
                float low=min(min(up,down),min(left,right));
                float high=max(max(up,down),max(left,right));
                float dark=smoothstep(_Threshold,_Threshold+.10,high-l);
                float light=smoothstep(_Threshold,_Threshold+.10,l-low);
                c*=1-dark*_Outline;
                c=lerp(c,float3(1,.97,.89),light*_Highlight*(1-dark));
                Output o; o.color=half4(c,1);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y=1-uv.y;
                #endif
                o.depth=SAMPLE_TEXTURE2D(_DepthTex,sampler_DepthTex,uv).r;
                return o;
            }
            ENDHLSL
        }
        Pass
        {
            Name "CopyEnvironmentDepth"
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CopyDepth
            #pragma target 3.0
            #define USE_FULL_PRECISION_BLIT_TEXTURE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float CopyDepth(Varyings i):SV_Target
            { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_PointClamp,i.texcoord,0).r; }
            ENDHLSL
        }
    }
}
