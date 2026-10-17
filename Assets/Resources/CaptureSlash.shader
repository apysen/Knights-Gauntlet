Shader "KnightsGauntlet/CaptureSlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Slash silhouette", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _FlowTex ("Flow noise (repeat)", 2D) = "gray" {}
        _WakeColor ("Golden wake", Color) = (1, 0.65, 0.16, 1)
        _EdgeColor ("Cutting edge", Color) = (1, 0.99, 0.88, 1)
        _Intensity ("Edge intensity", Range(0, 3)) = 1.2
        _NoiseSpeed ("Flow per slash", Range(0, 4)) = 1.35
        _Erosion ("Tail erosion", Range(0, 1)) = 0.55
        [PerRendererData] _ReducedEffects ("Reduced effects", Range(0, 1)) = 0
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

            float _Progress, _Seed, _Variant, _Combo, _ReducedEffects;
            sampler2D _FlowTex;
            float4 _WakeColor, _EdgeColor;
            float _Intensity, _NoiseSpeed, _Erosion;

            float InkNoise(float2 uv)
            {
                float2 cell = floor(uv * float2(74.0, 23.0));
                float n = frac(dot(cell, float2(0.1031, 0.11369)) + _Seed * 0.13787);
                return frac(n * (n + 33.33) * (n + 19.19));
            }

            half4 SlashFrag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                float p = saturate(_Progress);
                // Trace the crescent around its bend; straight and swept ribbons
                // travel along their shared lower-left to upper-right diagonal.
                float along = saturate((uv.x + uv.y) * 0.5);
                if (_Variant < 0.5)
                    along = saturate((atan2(uv.y - 0.97, uv.x - 0.03) + 1.5707963) / 1.5707963);

                float reduced = saturate(_ReducedEffects);
                // Lifetime-driven flow freezes cleanly with the capture animation.
                float across = (uv.y - uv.x) * 0.7071;
                if (_Variant < 0.5)
                    across = length(uv - float2(0.03, 0.97));
                float2 flowUV = float2(along * 2.0 - p * _NoiseSpeed, across * 9.0);
                float flow = tex2D(_FlowTex, flowUV + frac(_Seed * 0.017)).r;
                float detail = tex2D(_FlowTex, flowUV * float2(1.7, 1.3) + 0.37).r;
                float energy = lerp(0.5, flow * 0.7 + detail * 0.3, 1.0 - reduced);
                float draw = saturate(p / 0.28);
                float head = lerp(-0.08, 1.10, 1.0 - (1.0 - draw) * (1.0 - draw));
                float erase = smoothstep(0.34, 1.0, p);
                float tail = lerp(-0.12, 1.12, erase);
                // A small jagged break follows the cutting tip, rather than
                // dissolving the entire sprite into unrelated particles.
                float brokenTail = tail + ((energy - 0.5) * 0.38 * _Erosion
                    + (InkNoise(uv) - 0.5) * 0.035) * erase * (1.0 - reduced);
                float reveal = 1.0 - smoothstep(head - 0.045, head + 0.025, along);
                float trail = smoothstep(brokenTail - 0.045, brokenTail + 0.045, along);
                float mask = reveal * trail * (1.0 - smoothstep(0.88, 1.0, p));

                fixed4 source = SampleSpriteTexture(uv);
                float fill = smoothstep(0.16, 0.8, dot(source.rgb, float3(0.299, 0.587, 0.114)));
                float tip = 1.0 - smoothstep(0.015, 0.16, max(0.0, head - along));
                float wake = saturate((head - along) / 0.65);
                float streak = smoothstep(0.43, 0.72, energy);
                float3 hotColor = _EdgeColor.rgb * lerp(1.0, _Intensity * (1.0 + _Combo * 0.3), 1.0 - reduced);
                float3 stroke = lerp(_WakeColor.rgb, hotColor, saturate(1.0 - wake * 0.8 + streak * 0.3 + tip));
                // Only recolor the light ink: the original black border stays crisp.
                float3 ink = lerp(source.rgb, stroke, fill) * input.color.rgb;
                float tailBand = 1.0 - smoothstep(0.0, 0.24, along - tail);
                float erosion = smoothstep(0.16, 0.48, energy + (1.0 - erase) * 0.5);
                mask *= lerp(1.0, erosion, tailBand * erase * _Erosion * (1.0 - reduced));
                float alpha = source.a * input.color.a * mask;
                return half4(ink * alpha, alpha);
            }
            ENDCG
        }
    }
}
