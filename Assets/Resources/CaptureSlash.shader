Shader "KnightsGauntlet/CaptureSlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Slash silhouette", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [PerRendererData] _Progress ("Capture lifetime", Range(0, 1)) = 0
        [PerRendererData] _Seed ("Ink seed", Float) = 0
        [PerRendererData] _Variant ("Slash shape", Float) = 0
        [PerRendererData] _Combo ("Combo strength", Range(0, 1)) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SlashFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _Progress, _Seed, _Variant, _Combo;

            float InkNoise(float2 uv)
            {
                float2 cell = floor(uv * float2(74.0, 23.0));
                float n = frac(dot(cell, float2(0.1031, 0.11369)) + _Seed * 0.13787);
                return frac(n * (n + 33.33) * (n + 19.19));
            }

            fixed4 SlashFrag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                float p = saturate(_Progress);
                // Trace the crescent around its bend; straight and swept ribbons
                // travel along their shared lower-left to upper-right diagonal.
                float along = saturate((uv.x + uv.y) * 0.5);
                if (_Variant < 0.5)
                    along = saturate((atan2(uv.y - 0.97, uv.x - 0.03) + 1.5707963) / 1.5707963);

                float draw = saturate(p / 0.37);
                float head = lerp(-0.08, 1.10, 1.0 - (1.0 - draw) * (1.0 - draw));
                float erase = smoothstep(0.43, 1.0, p);
                float tail = lerp(-0.12, 1.12, erase);
                // A small jagged break follows the cutting tip, rather than
                // dissolving the entire sprite into unrelated particles.
                float brokenTail = tail + (InkNoise(uv) - 0.5) * 0.11 * erase;
                float reveal = 1.0 - smoothstep(head - 0.045, head + 0.025, along);
                float trail = smoothstep(brokenTail - 0.045, brokenTail + 0.045, along);
                float mask = reveal * trail * (1.0 - smoothstep(0.88, 1.0, p));

                fixed4 source = SampleSpriteTexture(uv);
                float fill = smoothstep(0.16, 0.8, dot(source.rgb, float3(0.299, 0.587, 0.114)));
                float glint = (1.0 - smoothstep(0.0, 0.08, abs(along - head + 0.025))) * fill;
                float3 ink = source.rgb * input.color.rgb;
                ink = lerp(ink, float3(1.0, 1.0, 0.97) * (1.0 + _Combo * 0.25), glint);
                float alpha = source.a * input.color.a * mask;
                return fixed4(ink * alpha, alpha);
            }
            ENDCG
        }
    }
}
