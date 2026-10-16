Shader "Knight's Gauntlet/Board Clipped Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Assets/Resources/BoardRoll.hlsl"
            #pragma multi_compile_instancing
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _KGBoardClip;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 world : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(KGRollPosition(output.world));
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_KGBoardClip.w > .5)
                {
                    clip(_KGBoardClip.x - abs(input.world.x));
                    float projectedY = input.world.y - input.world.z * _KGBoardClip.z;
                    clip(_KGBoardClip.y + projectedY);
                    clip(_KGBoardRoll.w > .5 ? _KGBoardRoll.z - input.world.y : _KGBoardClip.y - projectedY);
                }
                half4 color = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv) * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
}
