Shader "KnightsGauntlet/MenuCircleReveal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Outside ("Menu background", Color) = (0,0,0,1)
        _Inside ("Portrait background", Color) = (0,0,0,1)
        _Ring ("Circle border", Color) = (1,.75,.2,1)
        _Center ("Center in pixels", Vector) = (0,0,0,0)
        _ScreenSize ("Screen size", Vector) = (1,1,0,0)
        _Radius ("Radius in pixels", Float) = 79
        _Reveal ("Interior reveal", Range(0,1)) = 0
        _RingWidth ("Border width", Float) = 2
        _RingAlpha ("Border alpha", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float4 _Outside, _Inside, _Ring, _Center, _ScreenSize;
            float _Radius, _Reveal, _RingWidth, _RingAlpha;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float d = length(i.uv * _ScreenSize.xy - _Center.xy);
                float inside = 1 - smoothstep(_Radius - 1, _Radius + 1, d);
                float border = (1 - smoothstep(_RingWidth * .5, _RingWidth * .5 + 1, abs(d - _Radius))) * _RingAlpha;
                fixed4 result = lerp(_Outside, _Inside, inside);
                result.a = 1 - inside * _Reveal;
                result.rgb = lerp(result.rgb, _Ring.rgb, border);
                result.a = max(result.a, border);
                return result;
            }
            ENDCG
        }
    }
}
