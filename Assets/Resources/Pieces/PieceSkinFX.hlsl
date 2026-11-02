#ifndef KG_PIECE_SKIN_FX_INCLUDED
#define KG_PIECE_SKIN_FX_INCLUDED

// Procedural shop skins shared by every piece shader (Chess Piece, Matte Ceramic,
// Ink Porcelain). Skins are surface finishes, not models, so they follow the
// player through every Battle transformation. Patterns are generated in the
// piece's own scaled object space, so they stick to the mesh, need no UVs and
// look the same on the knight, rook, bishop, queen and pawn.
//
// The including shader must declare these in its UnityPerMaterial CBUFFER:
//   float _SkinFX;    0 none, 1 stained glass, 2 phantom, 3 gilded, 4 queensbane, 5 nightfall,
//                     6 starforged
//   float _SkinFade;  overall opacity, used by the phantom afterimages
//   float _SkinScale; 1 / piece transform scale, so patterns keep one size
#define KG_SKIN_GLASS 1
#define KG_SKIN_PHANTOM 2
#define KG_SKIN_GILDED 3
#define KG_SKIN_QUEENSBANE 4
#define KG_SKIN_NIGHTFALL 5
#define KG_SKIN_STARFORGED 6

struct KGSkinInput
{
    float3 skinPos;    // scaled object space (piece diameter is about 0.68)
    float3 positionWS; // unrolled world position
    float4 positionCS; // SV_POSITION, for dithering
    half3 normal;
    half3 view;
    half3 light;
    half trim;         // 1 on trim, eyes and feet; 0 on the main body
    half eye;          // 1 on eyes (and the matching head detail on other pieces)
};

// Object to world scale, so object space can be expressed in piece units.
float3 KGSkinObjectScale()
{
    float4x4 m = GetObjectToWorldMatrix();
    return float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
}

float3 KGSkinPosition(float3 positionOS)
{
    return positionOS * KGSkinObjectScale() * _SkinScale;
}

float KGSkinHash(float3 p)
{
    p = frac(p * .3183099 + .1);
    p *= 17;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float3 KGSkinHash3(float3 p)
{
    return float3(KGSkinHash(p), KGSkinHash(p + 17.31), KGSkinHash(p + 41.73));
}

float KGSkinNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3 - 2 * f);
    return lerp(
        lerp(lerp(KGSkinHash(i), KGSkinHash(i + float3(1,0,0)), f.x),
             lerp(KGSkinHash(i + float3(0,1,0)), KGSkinHash(i + float3(1,1,0)), f.x), f.y),
        lerp(lerp(KGSkinHash(i + float3(0,0,1)), KGSkinHash(i + float3(1,0,1)), f.x),
             lerp(KGSkinHash(i + float3(0,1,1)), KGSkinHash(i + float3(1,1,1)), f.x), f.y), f.z);
}

// x: distance to the nearest cell point, y: to the second nearest, z: cell id.
// y - x is small along cell borders.
float3 KGSkinVoronoi(float3 p)
{
    float3 b = floor(p);
    float3 f = frac(p);
    float d1 = 8, d2 = 8, id = 0;
    [unroll] for (int z = -1; z <= 1; z++)
    [unroll] for (int y = -1; y <= 1; y++)
    [unroll] for (int x = -1; x <= 1; x++)
    {
        float3 g = float3(x, y, z);
        float3 r = g + KGSkinHash3(b + g) - f;
        float d = dot(r, r);
        if (d < d1) { d2 = d1; d1 = d; id = KGSkinHash(b + g + 7.7); }
        else if (d < d2) d2 = d;
    }
    return float3(sqrt(d1), sqrt(d2), id);
}

half3 KGSkinJewel(float id)
{
    if (id < .2) return half3(.86, .10, .17);  // ruby
    if (id < .4) return half3(.13, .33, .94);  // sapphire
    if (id < .6) return half3(.07, .70, .36);  // emerald
    if (id < .8) return half3(1.0, .63, .10);  // amber
    return half3(.58, .18, .88);               // amethyst
}

// One layer of twinkling stars: a jittered point in about half of the cells.
half KGSkinStars(float3 p, float radius, float t)
{
    float3 cell = floor(p);
    float3 f = frac(p) - .5;
    float h = KGSkinHash(cell + 3.3);
    float d = length(f - (KGSkinHash3(cell) - .5) * .6);
    half twinkle = .55h + .45h * sin(t * (2 + h * 4) + h * 40);
    return (1 - smoothstep(0, radius, d)) * step(.5, h) * twinkle;
}

// Screen-space dither threshold (interleaved gradient noise). Lets an opaque
// pass look translucent without sorting problems on a self-overlapping mesh.
float KGSkinDither(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(.06711056, .00583715))));
}

half3 KGSkinShadeCore(KGSkinInput i)
{
    int fx = (int)(_SkinFX + .5);
    float t = _Time.y;
    // Piece-relative world offset in piece units, for screen-facing sweeps and
    // rising wisps. World up for standing pieces is -Z.
    float3 rel = (i.positionWS - GetObjectToWorldMatrix()._m03_m13_m23) * _SkinScale;
    half ndl = dot(i.normal, i.light);
    half wrap = saturate((ndl + .4h) / 1.4h);
    half fres = 1 - saturate(dot(i.normal, i.view));
    half ndh = saturate(dot(i.normal, normalize(i.light + i.view)));

    if (fx == KG_SKIN_GLASS)
    {
        float3 cell = KGSkinVoronoi(i.skinPos * 7);
        half lead = 1 - smoothstep(.05, .1, cell.y - cell.x);
        half3 pane = KGSkinJewel(cell.z);
        // Light drifts through the panes like sun moving behind a window.
        half drift = .5h + .5h * sin(dot(rel, float3(2.2, -1.4, 1.0)) * 4 - t * 1.5);
        half within = .6h + .3h * KGSkinNoise(i.skinPos * 34);
        half3 glass = pane * (.42h + .38h * wrap + .55h * drift * drift + .35h * fres) * within;
        glass += pane * pane * .25h; // a little saturated self-glow
        glass += half3(1, .97, .9) * pow(ndh, 48) * .55h;
        half3 came = half3(.085, .08, .075) * (.55h + .9h * wrap) + half3(.55, .55, .6) * pow(ndh, 22) * .35h;
        return lerp(glass, came, max(lead, i.trim * .9h));
    }
    if (fx == KG_SKIN_PHANTOM)
    {
        // Wisps rise up the piece (toward -Z) over time.
        float3 w = float3(rel.x * 5.5, rel.y * 5.5, (rel.z + t * .32) * 4.5);
        half wisp = KGSkinNoise(w + i.skinPos * 2.5) * .65h + KGSkinNoise(w * 2.3) * .35h;
        half3 deep = half3(.04, .13, .22);
        half3 bright = half3(.62, 1, .97);
        half3 c = lerp(deep, bright, saturate(fres * fres * 1.3h + wisp * .3h + wrap * .12h));
        c += bright * pow(ndh, 28) * .3h;
        c = lerp(c, bright * 1.15h, saturate(i.trim * .25h + i.eye)); // eyes glow
        half flicker = .92h + .08h * sin(t * 3.1 + rel.z * 9);
        half alpha = saturate(.3h + fres * .85h + (wisp - .5h) * .4h + i.eye) * flicker * _SkinFade;
        clip(alpha - KGSkinDither(i.positionCS.xy) - .001);
        return c;
    }
    if (fx == KG_SKIN_GILDED)
    {
        // Engraved filigree: two families of swirling lines.
        float3 q = i.skinPos * 24;
        float f = q.x + 1.8 * sin(q.y * .8 + q.z * .5) + 1.2 * sin(q.z * 1.3);
        float g = q.z + 1.8 * sin(q.x * .7 + q.y * .6) + 1.1 * sin(q.y * 1.2);
        half engrave = max(1 - smoothstep(0, .13, abs(sin(f))), 1 - smoothstep(0, .13, abs(sin(g))));
        engrave *= 1 - i.trim;
        half3 albedo = lerp(half3(1, .68, .17), half3(.36, .19, .07), i.trim);
        albedo *= 1 - engrave * .55h;
        half3 c = albedo * (.14h + .72h * wrap * wrap);
        c += albedo * pow(ndh, 14) * 1.3h * (1 - engrave * .7h);
        c += half3(1, .96, .85) * pow(ndh, 90) * .5h;
        c += albedo * fres * .35h;
        // A slow shine sweeps across the piece every few seconds.
        float s = dot(rel, normalize(float3(1, 1, -.6)));
        float sweep = frac(t * .28) * 3.4 - 1.7;
        half glint = exp(-(s - sweep) * (s - sweep) * 49);
        c += half3(1, .95, .8) * glint * .85h * (1 - engrave * .6h) * (1 - i.trim * .5h);
        return c;
    }
    if (fx == KG_SKIN_QUEENSBANE)
    {
        half dent = KGSkinNoise(i.skinPos * 7);
        half scratch = smoothstep(.8, .92, KGSkinNoise(i.skinPos * float3(80, 5, 34)));
        half3 albedo = lerp(half3(.74, .77, .82) * (.72h + .38h * dent), half3(.1, .1, .12), i.trim);
        half3 c = albedo * (.24h + .8h * wrap);
        c += lerp(half3(.9, .93, 1), half3(.3, .3, .34), i.trim) * pow(ndh, 26) * .7h;
        c += half3(.85, .88, .95) * scratch * (.25h + .35h * wrap) * (1 - i.trim);
        // Glowing red fractures, only over parts of the armor.
        float3 v = KGSkinVoronoi(i.skinPos * 4.5 + 3.1);
        half edge = v.y - v.x;
        half mask = smoothstep(.42, .62, KGSkinNoise(i.skinPos * 3 + 9));
        half crack = (1 - smoothstep(0, .05, edge)) * mask;
        half halo = (1 - smoothstep(0, .18, edge)) * mask;
        half beat = .55h + .45h * pow(.5h + .5h * sin(t * 2.2), 3);
        half3 red = half3(1, .16, .07);
        c = lerp(c, red * (1.1h + .5h * beat), crack);
        c += red * halo * .3h * beat;
        c += red * fres * fres * i.trim * .35h * beat;
        return c;
    }
    if (fx == KG_SKIN_NIGHTFALL)
    {
        // Completely black armor with glowing red eyes. A faint cool sheen and a
        // red rim keep the silhouette readable on the dark board.
        half3 c = half3(.03, .03, .038) * (.45h + .9h * wrap);
        c += half3(.32, .33, .38) * pow(ndh, 40) * .32h;
        c += half3(.55, .05, .04) * pow(fres, 3) * .22h;
        half pulse = .85h + .15h * sin(t * 2.6);
        half3 eyeGlow = lerp(half3(1, .1, .05), half3(1, .55, .4), pow(1 - fres, 3) * .6h) * 1.6h * pulse;
        return lerp(c, eyeGlow, i.eye);
    }
    if (fx == KG_SKIN_STARFORGED)
    {
        // A window into deep space: a slowly churning nebula with twinkling stars.
        float3 q = i.skinPos * 5 + float3(0, 0, t * .05);
        float n1 = KGSkinNoise(q + KGSkinNoise(q * 1.7 + t * .08) * 1.5);
        float n2 = KGSkinNoise(q * 2.3 - 4.1);
        half cloud = saturate((n1 * .9h + n2 * .3h - .42h) * 1.6h);
        half3 c = lerp(half3(.02, .015, .07), lerp(half3(.42, .12, .78), half3(.1, .38, .95), n2), cloud);
        c = lerp(c, half3(.95, .3, .72), smoothstep(.72, .95, n1) * .45h);
        c += half3(1, .95, .9) * KGSkinStars(i.skinPos * 14, .2, t) * 1.4h;
        c += half3(.75, .85, 1) * KGSkinStars(i.skinPos * 30 + 7.7, .12, t * 1.3) * .7h;
        // Just enough form shading to keep the silhouette, plus a starlight rim.
        c *= .55h + .45h * wrap;
        c += half3(.5, .55, 1) * pow(fres, 3) * .38h;
        c += pow(ndh, 60) * .25h;
        // Trim is polished dark silver; eyes burn starlight white.
        half3 silver = half3(.5, .53, .66) * (.28h + .8h * wrap) + half3(.85, .9, 1) * pow(ndh, 30) * .7h;
        c = lerp(c, silver, i.trim * .75h);
        return lerp(c, half3(.85, .95, 1) * 1.6h, i.eye);
    }
    return half3(1, 0, 1);
}

// Returns the skinned color. Only call when _SkinFX > .5. May clip: Phantom is always
// part see-through, and any finish below full _SkinFade dithers out (trail afterimages).
half3 KGSkinShade(KGSkinInput i)
{
    half3 c = KGSkinShadeCore(i);
    if ((int)(_SkinFX + .5) != KG_SKIN_PHANTOM && _SkinFade < .999)
        clip(_SkinFade - KGSkinDither(i.positionCS.xy) - .001);
    return c;
}

#endif
