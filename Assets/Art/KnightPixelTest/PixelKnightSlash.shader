Shader "KnightsGauntlet/Pixel Knight Slash"
{
    Properties
    {
        _Progress ("Slash progress", Range(0,1)) = 0
        _GridSize ("Native pixel grid", Float) = 64
        _Thickness ("Stroke half-width", Range(.005,.06)) = .05
        _TrailLength ("Trail length", Range(.15,1)) = 1
        _Erosion ("Trailing breakup", Range(0,1)) = .45
        _Start ("Raised tip UV", Vector) = (.69,.92,0,0)
        _ControlA ("Upper curve UV", Vector) = (.98,.72,0,0)
        _ControlB ("Lower curve UV", Vector) = (.38,.32,0,0)
        _End ("Low tip UV", Vector) = (.18,.15,0,0)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float _Progress, _GridSize, _Thickness, _TrailLength, _Erosion;
            float4 _Start, _ControlA, _ControlB, _End;

            float2 Curve(float t)
            {
                float a = 1-t;
                return a*a*a*_Start.xy + 3*a*a*t*_ControlA.xy
                     + 3*a*t*t*_ControlB.xy + t*t*t*_End.xy;
            }

            half4 Frag(v2f_img input) : SV_Target
            {
                float p = _Progress;
                if (p <= 0 || p >= 1) return 0;
                // Animate the curve inside a fixed pixel canvas. The grid never
                // rotates with the stroke, and the output has binary alpha.
                float2 pixel = floor(input.uv * _GridSize);
                float2 uv = (pixel + .5) / _GridSize;
                float distanceToCurve = 100;
                float along = 0;
                [unroll] for (int j=0; j<24; j++)
                {
                    float2 a = Curve(j / 24.0);
                    float2 b = Curve((j+1) / 24.0);
                    float2 segment = b-a;
                    float part = saturate(dot(uv-a,segment) / max(dot(segment,segment),.000001));
                    float distance = length(uv-a-part*segment);
                    if (distance < distanceToCurve)
                    {
                        distanceToCurve = distance;
                        along = (j+part)/24.0;
                    }
                }
                float head = saturate(p/.32);
                float tail = max(0,head-_TrailLength);
                tail = lerp(tail,1,saturate((p-.55)/.45));
                if (along < tail || along > head) return 0;
                float width = _Thickness * lerp(.20,1,saturate((along-tail)/max(.01,head-tail)));
                if (distanceToCurve > width) return 0;
                float noise = frac(sin(dot(pixel+floor(p*10),float2(12.9898,78.233)))*43758.5453);
                float breakup = saturate((p-.5)/.5) * (1-saturate((along-tail)/.25)) * _Erosion;
                if (noise < breakup) return 0;

                // Three exact palette colors; no soft alpha or blurred bloom.
                float3 color = float3(.9568627,.7843137,.4);
                if (along < tail+.15) color = float3(.8,.5490196,.2431373);
                if (distanceToCurve < width*.45 || along > head-.07)
                    color = float3(1,.9411765,.8078431);
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = GammaToLinearSpace(color);
                #endif
                return half4(color,1);
            }
            ENDCG
        }
    }
}
