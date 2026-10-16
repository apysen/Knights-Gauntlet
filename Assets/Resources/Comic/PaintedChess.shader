Shader "Knight's Gauntlet/Painted Chess"
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
            #pragma fragment PaintedFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 PaintedFrag(v2f input) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(input.texcoord);
                half3 paint = tex.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                paint = LinearToGammaSpace(paint);
                #endif
                // Classify the authored colors in sRGB even in a linear project.
                half value = max(paint.r, max(paint.g, paint.b));
                half low = min(paint.r, min(paint.g, paint.b));
                // Gold has a separate hue from all six approved body colors.
                // Protect the gold, low-chroma ivory, and dark ink from skin tinting.
                half gold = step(paint.r * 0.52, paint.g) * step(paint.g * 0.9, paint.r)
                    * step(paint.b, paint.r * 0.3);
                half body = smoothstep(0.12, 0.25, value - low)
                    * smoothstep(0.10, 0.22, value) * (1 - gold);
                half original = step(0.995, min(input.color.r, min(input.color.g, input.color.b)));
                fixed3 recolored = input.color.rgb * lerp(0.65, 1.0, value);
                fixed3 color = lerp(tex.rgb, recolored, body * (1 - original));
                fixed alpha = tex.a * input.color.a;
                return fixed4(color * alpha, alpha);
            }
            ENDCG
        }
    }
}
