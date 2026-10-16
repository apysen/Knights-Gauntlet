Shader "Knight's Gauntlet/Matte Ceramic Prototype"
{
    Properties
    {
        [MainColor] _BaseColor ("Clay color", Color) = (0.5,0.6,0.8,1)
        _AccentColor ("Equipped skin", Color) = (1,1,1,1)
        _AccentStrength ("Use equipped skin", Float) = 0
        _SkinRegion ("Recolor body", Float) = 1
        _PieceClip ("Clip pieces at the board edge", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Resources/BoardRoll.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _AccentColor;
                float _AccentStrength;
                float _SkinRegion;
                float _PieceClip;
            CBUFFER_END
            float4 _KGBoardClip;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(KGRollPosition(output.positionWS));
                output.normalWS = KGRollNormal(output.normalWS, output.positionWS.y);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_PieceClip > .5 && _KGBoardClip.w > .5)
                {
                    clip(_KGBoardClip.x - abs(input.positionWS.x));
                    float boardY = input.positionWS.y - input.positionWS.z * _KGBoardClip.z;
                    clip(_KGBoardClip.y + boardY);
                    clip(_KGBoardRoll.w > .5 ? _KGBoardRoll.z - input.positionWS.y : _KGBoardClip.y - boardY);
                }
                half3 normal = normalize(input.normalWS);
                half3 light = normalize(half3(-.4,.7,-.8));
                half wrap = saturate((dot(normal,light)+.45h)/1.45h);
                half3 body = lerp(_BaseColor.rgb, _AccentColor.rgb,
                    saturate(_AccentStrength * _SkinRegion));
                half3 color = body * (half3(.42,.48,.56) + half3(.50,.46,.39)*wrap);
                half3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half broadHighlight = pow(saturate(dot(normal,normalize(light+view))),12);
                color += half3(1,.94,.82) * broadHighlight * .025h;
                color += half3(.035,.045,.055) * pow(1-saturate(dot(normal,view)),3);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
