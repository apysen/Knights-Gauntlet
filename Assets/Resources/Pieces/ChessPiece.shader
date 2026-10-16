Shader "Knight's Gauntlet/Chess Piece"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _BoardTint ("Board tint", Color) = (1,1,1,1)
        _ClipBoard ("Clip board bounds", Float) = 0
        _PieceClip ("Clip pieces at the board edge", Float) = 0
        _RollBoard ("Follow rolling surface", Float) = 0
        _Environment ("Distant environment", Float) = 0
        _Backdrop ("Tabletop background", Float) = 0
        _Smoothness ("Enamel polish", Range(0,1)) = 0.5
        _AccentColor ("Equipped skin accent", Color) = (1,1,1,1)
        _AccentStrength ("Apply equipped skin", Float) = 0
        _RegionClip ("Clip scenery region", Float) = 0
        _RegionY ("Scenery region bounds", Vector) = (-10000,10000,0,0)
        _BoardHalfSize ("Board half size", Vector) = (2.5,5,0,0)
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
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _BoardTint;
                float _ClipBoard;
                float _PieceClip;
                float _RollBoard;
                float _Environment;
                float _Backdrop;
                float _Smoothness;
                half4 _AccentColor;
                float _AccentStrength;
                float _RegionClip;
                float4 _RegionY;
                float4 _BoardHalfSize;
            CBUFFER_END
            int _RoyalShadowCount;
            float4 _KGBoardClip;
            float4 _KGBoardEnd;
            float4 _RoyalShadows[24];
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; float3 positionWS : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 world = output.positionWS;
                if (_RollBoard > .5 || _PieceClip > .5)
                {
                    world = KGRollPosition(world);
                    output.normalWS = KGRollNormal(output.normalWS, output.positionWS.y);
                }
                output.positionCS = TransformWorldToHClip(world);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_RollBoard > .5 && _ClipBoard < .5 && _KGBoardEnd.y > .5)
                    clip(_KGBoardEnd.x + .18 - input.positionWS.y);
                if (_PieceClip > .5 && _KGBoardClip.w > .5)
                {
                    clip(_KGBoardClip.x - abs(input.positionWS.x));
                    float boardY = input.positionWS.y - input.positionWS.z * _KGBoardClip.z;
                    clip(_KGBoardClip.y + boardY);
                    clip(_KGBoardRoll.w > .5 ? _KGBoardRoll.z - input.positionWS.y : _KGBoardClip.y - boardY);
                }
                if (_Backdrop > 0.5)
                {
                    float2 p = input.positionWS.xy;
                    half glow = exp(-dot(p * float2(.12,.075), p * float2(.12,.075)));
                    half3 background = lerp(half3(.002,.004,.012), half3(.009,.018,.039), glow);
                    float2 edge = max(abs(p - float2(.15,-.18)) - float2(2.65,5.15), 0);
                    half shadow = exp(-dot(edge,edge) * 4);
                    return half4(background * (1 - shadow * .48), 1);
                }
                if (_ClipBoard > 0.5)
                {
                    clip(_BoardHalfSize.x - abs(input.positionWS.x));
                    clip(_BoardHalfSize.y + input.positionWS.y);
                    clip(_KGBoardRoll.w > .5 ? _KGBoardRoll.z - input.positionWS.y : _BoardHalfSize.y - input.positionWS.y);
                    // Tile sidewalls have depth. Clip their camera projection too,
                    // otherwise they peek beyond the lower edge of the frame.
                    float projectedY = input.positionWS.y - input.positionWS.z * _BoardHalfSize.z;
                    clip(_BoardHalfSize.y + projectedY);
                }
                if (_RegionClip > .5)
                {
                    clip(input.positionWS.y - _RegionY.x);
                    clip(_RegionY.y - input.positionWS.y);
                }
                // Mesh pieces share a board with a 2D renderer, whose lights only
                // illuminate sprites. A fixed key light keeps their relief visible.
                half3 normal = normalize(input.normalWS);
                half3 light = normalize(float3(-0.4, 0.7, -0.8));
                half diffuse = saturate(dot(normal, light));
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * _BoardTint;
                // Recolor gold details per player without changing the painted
                // enamel or shared enemy materials. Classic Gold keeps the source art.
                half gold = saturate((color.r-color.b)*5) * saturate((color.g-color.b)*5);
                half brightness = dot(color.rgb,half3(.2126,.7152,.0722));
                half3 accent = _AccentColor.rgb * brightness / .72h;
                color.rgb = lerp(color.rgb,accent,gold*_AccentStrength);
                half fill = saturate(dot(normal, normalize(float3(.6,-.3,-.4))));
                color.rgb *= half3(.20,.25,.34) + half3(.80,.72,.57) * diffuse + .12h * fill;
                half3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half highlight = pow(saturate(dot(normal, normalize(light + view))), lerp(20, 90, _Smoothness));
                color.rgb += half3(1,.80,.43) * highlight * lerp(.045h,.24h,_Smoothness);
                half rim = pow(1 - saturate(dot(normal,view)), 4);
                color.rgb += half3(.06,.10,.20) * rim * _Smoothness;
                if (_ClipBoard > .5)
                {
                    half shade = 0;
                    for (int i = 0; i < _RoyalShadowCount; i++)
                    {
                        float4 s = _RoyalShadows[i];
                        float2 contact = (input.positionWS.xy - s.xy) / max(s.z,.01);
                        float2 cast = (input.positionWS.xy - s.xy - float2(.17,-.22)*s.w) / (max(s.z,.01)*float2(1.15,.95));
                        // Compact polynomial falloff avoids two exponentials per
                        // piece per floor pixel, and has a finite soft footprint.
                        half a = saturate(1 - dot(contact,contact) * .33);
                        half b = saturate(1 - dot(cast,cast) * .28);
                        a *= a; b *= b;
                        shade = max(shade, .24h*a*a + .30h*b*b);
                    }
                    color.rgb *= 1 - shade;
                }
                if (_Environment > .5)
                {
                    half haze = saturate((input.positionWS.y - 5) / 18) * .65h;
                    color.rgb = lerp(color.rgb, half3(.15,.23,.28), haze);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
