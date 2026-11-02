Shader "Knight's Gauntlet/Velvet"
{
    // The stage curtains' velvet. Like the chess pieces it uses a fixed key light
    // (the 2D renderer's lights only reach sprites). Velvet's look comes from its
    // sheen: the pile catches light at grazing angles, so the fold edges glow and
    // the faces turned toward you stay deep and dark.
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (.45, .03, .08, 1)
        _SheenColor ("Sheen", Color) = (1, .42, .48, 1)
        _Sheen ("Sheen strength", Range(0, 2)) = .9
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SheenColor;
                half _Sheen;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 Frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                n = front ? n : -n;
                half3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                // Face the normal toward the viewer so both sides of the cloth shade alike.
                if (dot(n, view) < 0) n = -n;
                half3 light = normalize(half3(-0.4, 0.7, -0.8));
                half wrap = saturate((dot(n, light) + .45h) / 1.45h);
                half facing = saturate(dot(n, view));
                half sheen = pow(1 - facing, 2.2h);
                half3 color = _BaseColor.rgb * (.22h + .78h * wrap);
                color += _SheenColor.rgb * _BaseColor.rgb * 2.2h * sheen * _Sheen * (.4h + .6h * wrap);
                // A faint warm highlight down the crest of each fold.
                half crest = pow(saturate(dot(n, normalize(light + view))), 24);
                color += half3(1, .55, .55) * crest * .06h;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
