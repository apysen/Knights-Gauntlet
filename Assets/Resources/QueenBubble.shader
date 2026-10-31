Shader "Knight's Gauntlet/Queen Bubble"
{
    // Soap-film shield around the boss queen. Vertices follow the same rolling
    // surface as the chess pieces (BoardRoll.hlsl), so the bubble curls under the
    // conveyor with her instead of sliding off the top of the board.
    Properties
    {
        _Tint ("Film tint", Color) = (0.55, 0.82, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Pop ("Pop progress", Range(0, 1)) = 0
        _Board ("Sits on the rolling board", Float) = 1 // 0 for free-standing glass (UI icons)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Resources/BoardRoll.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Opacity;
                float _Pop;
                float _Board;
            CBUFFER_END
            float4 _KGBoardClip;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1; // before rolling, for the board clip
                float3 rolledWS : TEXCOORD2;   // after rolling, for lighting
                float3 normalOS : TEXCOORD3;   // stable pattern for the pop break-up
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;
                // A slow wobble keeps it reading as a liquid film, not glass.
                float t = _Time.y;
                p += input.normalOS * (sin(t * 2.3 + p.z * 9.0) * .012 + sin(t * 1.7 + p.x * 11.0 + p.y * 7.0) * .010) * _Board;
                p *= 1 + _Pop * .22; // swell as it bursts
                output.positionWS = TransformObjectToWorld(p);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.rolledWS = output.positionWS;
                if (_Board > .5)
                {
                    output.normalWS = KGRollNormal(output.normalWS, output.positionWS.y);
                    output.rolledWS = KGRollPosition(output.positionWS);
                }
                output.positionCS = TransformWorldToHClip(output.rolledWS);
                output.normalOS = input.normalOS;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Same top/bottom board clip as the pieces (not the side clip, so a
                // bubble on an edge column stays round).
                if (_Board > .5 && _KGBoardClip.w > .5)
                {
                    float boardY = input.positionWS.y - input.positionWS.z * _KGBoardClip.z;
                    clip(_KGBoardClip.y + boardY);
                    clip(_KGBoardRoll.w > .5 ? _KGBoardRoll.z - input.positionWS.y : _KGBoardClip.y - boardY);
                }
                // Pop: the film breaks into patches that vanish at random.
                if (_Pop > 0)
                {
                    float cell = frac(sin(dot(floor(input.normalOS * 6), float3(12.9898, 78.233, 45.164))) * 43758.5453);
                    clip(cell - _Pop * 1.15);
                }
                half3 n = normalize(input.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(input.rolledWS));
                half fresnel = pow(1 - saturate(abs(dot(n, v))), 2.5);
                // Thin-film iridescence, strongest at the rim like a real soap bubble.
                half3 film = .5 + .5 * cos(6.2832 * (fresnel * 1.2 + n.y * .15 + _Time.y * .05 + half3(0, .33, .67)));
                half3 color = lerp(_Tint.rgb, film, .45 * fresnel);
                // Key light matches the chess piece shader, plus a soft window glint.
                half3 key = normalize(half3(-.4, .7, -.8));
                half spec = pow(saturate(dot(n, normalize(key + v))), 90);
                half glint = pow(saturate(dot(n, normalize(half3(.5, -.2, -.9) + v))), 30) * .35;
                color += spec + glint;
                half alpha = (.06 + fresnel * .75 + spec + glint) * _Opacity * (1 - _Pop);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
