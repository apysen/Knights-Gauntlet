Shader "Knight's Gauntlet/Comic Palette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment ComicFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 ComicFrag(v2f input) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(input.texcoord);
                // Neutral midtones carry the skin color. Ink and paper highlights
                // retain their own colors, including for the dark Nightfall skin.
                half value = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                half ink = 1 - smoothstep(0.14, 0.25, value);
                half paper = smoothstep(0.93, 0.98, value);
                half light = lerp(0.40, 1.0, saturate((value - 0.25) / 0.48));
                half highlight = saturate((value - 0.73) / 0.18);
                fixed3 armor = lerp(input.color.rgb * light, fixed3(1, 0.973, 0.906), highlight * 0.45);
                fixed3 color = lerp(armor, tex.rgb, max(ink, paper));
                // A modest paper lift keeps dark cosmetic variants readable.
                color = lerp(color, max(color, fixed3(0.16, 0.13, 0.21)),
                             (1 - ink) * (1 - paper) * 0.35);
                fixed alpha = tex.a * input.color.a;
                return fixed4(color * alpha, alpha);
            }
            ENDCG
        }
    }
}
