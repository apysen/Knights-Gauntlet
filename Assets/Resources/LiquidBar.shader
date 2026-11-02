// A volatile liquid gauge for UI (the run-options difficulty meter), drawn over the
// whole container rect. UIManager.RunOptionsJuice drives the uniforms like a health bar:
//   _Fill   the liquid level 0..1 (spring-driven, so changes overshoot and slosh)
//   _Slosh  0..1 how hard the leading surface is waving right now
//   _Boil   0..1 extra turbulence, bubbles and flicker (Grandmaster)
// Vertex colour alpha carries CanvasGroup fades.
Shader "KnightsGauntlet/LiquidBar"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Deep ("Liquid deep colour", Color) = (.1,.5,.25,1)
        _Bright ("Liquid bright colour", Color) = (.5,1,.65,1)
        _Fill ("Fill", Range(0,1)) = .5
        _Slosh ("Slosh", Range(0,1)) = 0
        _Boil ("Boil", Range(0,1)) = 0
        _Aspect ("Width / height", Float) = 14
        _Notches ("Segment count", Float) = 4
        _T ("Unscaled time", Float) = 0
        _Flow ("Flow phase", Float) = 0
        _Swirl ("Swirl phase", Float) = 0
        _Rise ("Bubble rise phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            float4 _Deep, _Bright;
            // Phases that speed up while boiling are integrated on the CPU (UIManager.RunOptionsJuice)
            // and passed in, never computed as time * speed here: a speed that changes would
            // rescale the whole elapsed time and make the liquid lurch.
            float _Fill, _Slosh, _Boil, _Aspect, _Notches, _T, _Flow, _Swirl, _Rise;

            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p) { return noise(p) * .55 + noise(p * 2.1 + 3.7) * .3 + noise(p * 4.3 + 7.1) * .15; }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _T;
                // Square-pixel space: x runs 0.._Aspect, y 0..1.
                float2 p = float2(i.uv.x * _Aspect, i.uv.y);

                // ── Container: a capsule with a dark glass interior ─────────────
                float r = .5;
                float2 c = float2(clamp(p.x, r, _Aspect - r), .5);
                float dist = length(p - c) - r;                 // <0 inside the capsule
                float shape = 1 - smoothstep(-.03, .02, dist);
                if (shape <= 0) return 0;
                float inner = 1 - smoothstep(-.16, -.1, dist);  // inside the rim
                fixed3 col = lerp(fixed3(.05, .07, .12), fixed3(.09, .13, .2), i.uv.y);

                // ── Liquid: leading surface waves (more while sloshing / boiling) ─
                float boil = _Boil;
                float wave = sin(i.uv.y * 9 + t * 7) * .018 + sin(i.uv.y * 23 - t * 13) * .008;
                wave *= 1 + _Slosh * 5 + boil * 2.5;
                wave += (noise(float2(i.uv.y * 6, t * 6)) - .5) * .05 * boil;
                float front = _Fill * _Aspect + wave * _Aspect * .09;
                float inLiquid = 1 - smoothstep(front - .02, front + .02, p.x);

                // Churning interior bands, scrolling and warping.
                float2 q = float2(p.x * .9 - _Flow, i.uv.y * 1.6 + t * .2);
                float churn = fbm(q + fbm(q * 1.7 + _Swirl) * 1.2);
                float depth = smoothstep(0, 1, i.uv.y);
                fixed3 liquid = lerp(_Deep.rgb, _Bright.rgb, saturate(depth * .75 + churn * .45 - .1));

                // Rising bubbles in columns (cells of width w, in bar heights), only below the surface.
                // A fixed grid (it never reshuffles); boiling reveals more bubbles and swells them.
                float w = 1 / 4.0;
                float cell = floor(p.x / w);
                float bubbles = 0;
                for (int k = -1; k <= 1; k++)
                {
                    float id = cell + k;
                    float rnd = hash(float2(id, 3.1));
                    float by = frac(rnd * 7 + _Rise * (.35 + rnd * .5));
                    float shown = step(hash(float2(id, 5.5)), .5 + boil * .5);
                    float bx = (id + .2 + .6 * hash(float2(id, 9.7))) * w + sin(t * 3 + id) * .03;
                    float br = .07 + .06 * hash(float2(id, 1.3)) + boil * .03;
                    float bd = length(float2(p.x - bx, i.uv.y - by));
                    float ring = smoothstep(br, br - .03, bd) - smoothstep(br - .035, br - .065, bd) * .6;
                    bubbles = max(bubbles, ring * shown * step(bx, front - .1) * (1 - by * .3));
                }
                liquid += bubbles * .55 * _Bright.rgb + bubbles * .15;

                // Foam along the leading surface, and a glossy stripe across the top.
                float foam = (1 - smoothstep(0, .12 + _Slosh * .15, abs(p.x - front))) * inLiquid;
                liquid = lerp(liquid, saturate(_Bright.rgb * 1.25 + .2), foam * .85);
                float gloss = smoothstep(.62, .78, i.uv.y) * (1 - smoothstep(.8, .9, i.uv.y));
                liquid += gloss * .28;
                // Volatile flicker when boiling.
                liquid *= 1 + boil * (.18 * sin(t * 31) * sin(t * 17.3) + .1 * (churn - .5));

                col = lerp(col, liquid, inLiquid * inner);

                // Segment notches at each difficulty step, etched over everything.
                float seg = frac(i.uv.x * _Notches);
                float notch = (1 - smoothstep(0, .006 * _Notches, min(seg, 1 - seg))) * step(.02, i.uv.x) * step(i.uv.x, .98);
                col = lerp(col, fixed3(.02, .03, .06), notch * .75 * inner);

                // Rim: a thin bright edge on top, darker below (glass catching light).
                float rim = (1 - inner) * shape;
                col = lerp(col, lerp(fixed3(.16, .2, .3), fixed3(.86, .74, .45), i.uv.y), rim);

                return fixed4(col, shape * i.color.a);
            }
            ENDCG
        }
    }
}
