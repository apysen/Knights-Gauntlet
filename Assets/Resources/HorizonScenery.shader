Shader "Knight's Gauntlet/Horizon Scenery"
{
    Properties
    {
        [MainColor] _BaseColor("Stone",Color)=(.3,.4,.45,1)
        _Wind("Banner cloth",Float)=0
        _Glow("Torch flame",Float)=0
        _Glass("Stained glass",Float)=0
        _Metal("Gilded decoration",Float)=0
        _Interior("Enclosed interior",Float)=0
        _BoardClearance("Board horizon overlap",Float)=-.003
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        ZWrite On Cull Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Wind;
            float _Glow;
            float _Glass;
            float _Metal;
            float _Interior;
            float _BoardClearance;
            CBUFFER_END
            float4 _KGHorizon, _KGScenery, _KGSceneryForward, _KGSceneryRight, _KGSceneryUp;
            struct Attributes { float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.world=TransformObjectToWorld(v.positionOS);
                float freeEdge=saturate(v.uv.y);
                float weight=freeEdge*freeEdge*_Wind*_KGScenery.y;
                float phase=_KGScenery.x*1.8+v.uv.y*4.8+v.uv.x*3+o.world.x*.35;
                float wave=sin(phase)*.12+sin(phase*1.7)*.025;
                // The top seam stays pinned. The free hem billows, ripples and
                // shifts gently sideways; embroidery shares the same UV motion.
                o.world+=(_KGSceneryForward.xyz*wave+
                    _KGSceneryRight.xyz*sin(phase*.8)*.035+
                    _KGSceneryUp.xyz*sin(phase*1.25)*.018)*weight;
                o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normalOS);
                o.normal=normalize(o.normal+(_KGSceneryRight.xyz*cos(phase)*.22+
                    _KGSceneryUp.xyz*sin(phase)*.07)*weight);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // Architectural cutaway: foreground board and its incoming rows
                // remain clear while the modeled doorway passes overhead.
                float screenY=i.positionCS.y/_ScreenParams.y;
                clip(screenY-_KGHorizon.x-_BoardClearance);
                half diffuse=saturate(dot(normalize(i.normal),normalize(float3(-.35,.65,-.8))));
                half3 color=_BaseColor.rgb*(half3(.38,.43,.5)+half3(.6,.54,.43)*diffuse);
                color*=lerp(half3(1,1,1),half3(.72,.65,.57),_KGHorizon.z);
                half3 view=normalize(GetWorldSpaceViewDir(i.world));
                half goldHighlight=pow(saturate(dot(normalize(i.normal),normalize(view+normalize(float3(-.35,.65,-.8))))),24);
                color+=half3(.24,.16,.045)*goldHighlight*_Metal;
                // Coloured glass stays luminous under the enclosed royal vault.
                color=lerp(color,_BaseColor.rgb*1.05,_Glass*.75);
                half flicker=1+(.06*sin(_KGScenery.x*7+i.world.x)+.035*sin(_KGScenery.x*13))*_KGScenery.y;
                color=lerp(color,_BaseColor.rgb*flicker*1.2,_Glow);
                // The extended hall is fully hidden by atmosphere before a bay
                // is recycled, so no silhouette appears at the far handoff.
                float depth=dot(i.world-_WorldSpaceCameraPos,_KGSceneryForward.xyz);
                half fog=smoothstep(65,115,depth)*max(_Interior,_KGHorizon.z);
                color=lerp(color,half3(.018,.024,.04),fog);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
