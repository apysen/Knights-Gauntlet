Shader "Knight's Gauntlet/Horizon Sky"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _KGHorizon, _KGScenery;
            struct Attributes { float3 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings Vert(Attributes v) { Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS);return o; }
            float Ellipse(float2 p,float2 center,float2 radius)
            {
                return length((p-center)/radius);
            }
            float Cloud(float2 p,float2 center,float2 scale)
            {
                float2 q=(p-center)/scale;
                float d=Ellipse(q,float2(0,0),float2(1,.35));
                d=min(d,Ellipse(q,float2(-.4,.17),float2(.47,.44)));
                d=min(d,Ellipse(q,float2(.12,.30),float2(.47,.59)));
                d=min(d,Ellipse(q,float2(.52,.08),float2(.40,.33)));
                return 1-smoothstep(.77,1.12,d);
            }
            float Segment(float2 p,float2 a,float2 b)
            {
                float2 d=b-a;
                return length(p-a-d*saturate(dot(p-a,d)/max(dot(d,d),.0000001)));
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.positionCS.xy/_ScreenParams.xy;
                float skyHeight=max(.15,1-_KGHorizon.x);
                float2 p=float2(uv.x,(uv.y-_KGHorizon.x)/skyHeight);
                float altitude=saturate(p.y);
                half3 sky=lerp(half3(.42,.58,.64),half3(.12,.29,.43),smoothstep(0,1,altitude));
                float aspect=_ScreenParams.x/_ScreenParams.y;
                float2 sunCenter=float2(.20,_KGHorizon.x+skyHeight*.38);
                float sun=length((uv-sunCenter)*float2(aspect,1));
                float halo=exp(-sun*sun/.0028);
                sky+=half3(.15,.105,.025)*halo;
                float disk=1-smoothstep(.020,.022,sun);
                sky=lerp(sky,half3(1,.84,.47),disk*.94);

                // Separate layers drift at different speeds, leaving the centre
                // of the sky open. The clouds remain present with Reduced Effects.
                float t=_KGScenery.x;
                float cloud1=Cloud(p,float2(frac(.15+t*.0035)*1.7-.35,.22),float2(.22,.17));
                float cloud2=Cloud(p,float2(frac(.67+t*.0021)*1.8-.4,.62),float2(.29,.18));
                float cloud3=Cloud(p,float2(frac(.88+t*.0013)*1.8-.4,.88),float2(.19,.12));
                float clouds=saturate(max(cloud1*.72,max(cloud2*.61,cloud3*.42)));
                half3 cloudColor=lerp(half3(.57,.66,.69),half3(.86,.87,.82),saturate(p.y+.25));
                sky=lerp(sky,cloudColor,clouds);

                // A small flock crosses for eight seconds, once every 38 seconds.
                // Wings are thin paired strokes with a shared flight direction.
                float flight=fmod(t,38)-12;
                if(_KGScenery.y>.5 && flight>0 && flight<8)
                {
                    float phase=flight/8;
                    float direction=fmod(floor(t/38),2)<1?1:-1;
                    float x=lerp(-.16,1.16,phase);
                    if(direction<0)x=1-x;
                    float birds=0;
                    [unroll] for(int b=0;b<4;b++)
                    {
                        float2 center=float2(x-direction*b*.037,_KGHorizon.x+skyHeight*(.46+b*.055)+sin(phase*3.14159)*.018);
                        float2 q=(uv-center)*float2(aspect,1);
                        float flap=sin(t*6.5+b*.8)*.004;
                        float d=min(Segment(q,float2(-.006,flap),float2(0,0)),Segment(q,float2(0,0),float2(.006,flap)));
                        float aa=max(fwidth(d),.00025);
                        birds=max(birds,1-smoothstep(.00065,.00065+aa,d));
                    }
                    sky=lerp(sky,half3(.045,.085,.11),birds*.75);
                }
                half3 below=half3(.055,.10,.14);
                half3 color=lerp(below,sky,smoothstep(_KGHorizon.x-.035,_KGHorizon.x+.015,uv.y));
                color=lerp(color,half3(.035,.045,.065),_KGHorizon.z*.95);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
