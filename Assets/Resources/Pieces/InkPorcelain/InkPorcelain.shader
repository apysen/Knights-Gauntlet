Shader "Knight's Gauntlet/Ink Porcelain"
{
    Properties
    {
        [MainColor] _BaseColor ("Porcelain palette", Color) = (0.93,0.88,0.77,1)
        _AccentColor ("Equipped skin", Color) = (1,1,1,1)
        _AccentStrength ("Use equipped skin", Float) = 0
        _SkinRegion ("Painted accent region", Float) = 0
        _Surface ("Ink, porcelain, gold", Float) = 0.5
        _PieceClip ("Clip at board edge", Float) = 0
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
                half4 _BaseColor, _AccentColor;
                float _AccentStrength, _SkinRegion, _Surface, _PieceClip;
            CBUFFER_END
            float4 _KGBoardClip;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(KGRollPosition(output.positionWS));
                output.normalWS = KGRollNormal(TransformObjectToWorldNormal(input.normalOS), output.positionWS.y);
                output.positionOS = input.positionOS.xyz;
                return output;
            }
            float PorcelainNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f*f*(3-2*f);
                float4 n = float4(dot(cell,float3(127.1,311.7,74.7)),
                    dot(cell+float3(1,0,0),float3(127.1,311.7,74.7)),
                    dot(cell+float3(0,1,0),float3(127.1,311.7,74.7)),
                    dot(cell+float3(1,1,0),float3(127.1,311.7,74.7)));
                float4 a = frac(sin(n)*43758.5453);
                float4 b = frac(sin(n+74.7)*43758.5453);
                float4 z = lerp(a,b,f.z);
                return lerp(lerp(z.x,z.y,f.x),lerp(z.z,z.w,f.x),f.y);
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
                half3 light = normalize(half3(-.45,.65,-.8));
                half diffuse = saturate(dot(normal,light));
                half3 body = lerp(_BaseColor.rgb, _AccentColor.rgb, saturate(_AccentStrength*_SkinRegion));
                // Object-space pigment keeps the finish stable through movement and rotation.
                // Broad washes remain visible at board size; fine streaks supply brush detail.
                // FBX stores centimeter-sized mesh coordinates under its conversion transform.
                float3 p = input.positionOS * 100;
                float wash = PorcelainNoise(p*13);
                float brush = PorcelainNoise(p*float3(32,5,32)+wash*.6);
                float streak = smoothstep(.58,.86,brush);
                float pigment = saturate(wash*.7 + brush*.3);
                half3 inkWash = body * lerp(.56,1.18,pigment);
                inkWash = lerp(inkWash,lerp(body,half3(.94,.88,.73),.32),streak*.4);
                body = lerp(body,inkWash,saturate(_SkinRegion));
                // Gentle ivory mottling, without muddying the dark foot or metallic trim.
                float ceramic = (1-_SkinRegion)*step(.2,_Surface)*(1-step(.8,_Surface));
                body *= 1 + ceramic*(wash-.5)*.13;
                // Restrained broad ink shadows and warm highlights retain shape at phone size.
                half shade = smoothstep(-.25,.75,dot(normal,light));
                half3 color = body * lerp(half3(.38,.43,.52),half3(1.02,.98,.90),shade);
                half3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half highlight = pow(saturate(dot(normal,normalize(light+view))),lerp(16,58,_Surface));
                color += lerp(half3(1,.97,.88),_BaseColor.rgb,step(.8,_Surface)) * highlight * _Surface * .65;
                half edge = pow(1-saturate(dot(normal,view)),4);
                color *= 1-edge*.17;
                // Subtle painted grain is procedural and stays attached to the moving mesh.
                half grain = sin(p.x*83+sin(p.y*57))*sin(p.z*117);
                color *= 1 + grain * (.012 + _SkinRegion*.045);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
