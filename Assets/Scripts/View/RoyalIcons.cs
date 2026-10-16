using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    // A shared line weight and drawing grid keeps HUD symbols visually related.
    public static class RoyalIcons
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (Cache.TryGetValue(name, out var sprite)) return sprite;
            const int n = 96;
            var lines = new List<Vector4>();
            void Line(float x, float y, float xx, float yy) => lines.Add(new Vector4(x, y, xx, yy));
            void Path(params float[] points)
            {
                for (int i = 0; i < points.Length - 2; i += 2)
                    Line(points[i], points[i + 1], points[i + 2], points[i + 3]);
            }
            switch (name)
            {
                case "settings":
                    for (int i = 0; i < 48; i++)
                    {
                        float a = i * Mathf.PI / 24f, b = (i + 1) * Mathf.PI / 24f;
                        float radiusA = i % 6 < 3 ? .35f : .27f;
                        float radiusB = (i + 1) % 6 < 3 ? .35f : .27f;
                        Line(.5f + Mathf.Cos(a) * radiusA, .5f + Mathf.Sin(a) * radiusA,
                            .5f + Mathf.Cos(b) * radiusB, .5f + Mathf.Sin(b) * radiusB);
                        Line(.5f + Mathf.Cos(a) * .12f, .5f + Mathf.Sin(a) * .12f,
                            .5f + Mathf.Cos(b) * .12f, .5f + Mathf.Sin(b) * .12f);
                    }
                    break;
                case "pause": Line(.35f,.24f,.35f,.76f); Line(.65f,.24f,.65f,.76f); break;
                case "restart":
                    for (int i = 0; i < 36; i++)
                    {
                        float a = (50f + i * 8f) * Mathf.Deg2Rad, b = a + 8f * Mathf.Deg2Rad;
                        Line(.5f + Mathf.Cos(a)*.30f,.5f + Mathf.Sin(a)*.30f,.5f + Mathf.Cos(b)*.30f,.5f + Mathf.Sin(b)*.30f);
                    }
                    Path(.53f,.76f,.72f,.74f,.73f,.93f); break;
                case "time":
                    Path(.28f,.82f,.72f,.82f,.70f,.65f,.39f,.36f,.28f,.18f,.72f,.18f,.62f,.37f,.31f,.65f,.28f,.82f);
                    Line(.37f,.27f,.63f,.27f); break;
                case "shield":
                    Path(.5f,.86f,.79f,.72f,.74f,.39f,.5f,.16f,.26f,.39f,.21f,.72f,.5f,.86f);
                    Path(.35f,.52f,.46f,.40f,.66f,.64f); break;
                case "path":
                    Path(.25f,.20f,.25f,.50f,.73f,.50f,.73f,.82f);
                    Path(.58f,.68f,.73f,.83f,.88f,.68f);
                    Path(.17f,.20f,.25f,.28f,.33f,.20f,.25f,.12f,.17f,.20f); break;
                default:
                    Path(.20f,.69f,.31f,.33f,.69f,.33f,.80f,.69f,.62f,.56f,.50f,.82f,.38f,.56f,.20f,.69f);
                    Line(.32f,.22f,.68f,.22f); break;
            }
            var texture = new Texture2D(n,n,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[n*n];
            float stroke = name == "pause" ? .034f : .022f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x+.5f)/n,(y+.5f)/n);
                float distance = 1f;
                foreach (var line in lines)
                {
                    var a = new Vector2(line.x,line.y); var d = new Vector2(line.z-line.x,line.w-line.y);
                    float t = Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.00001f,d.sqrMagnitude));
                    distance = Mathf.Min(distance,Vector2.Distance(p,a+d*t));
                }
                pixels[y*n+x] = new Color(1f,1f,1f,Mathf.Clamp01((stroke-distance)*n+.5f));
            }
            texture.SetPixels(pixels); texture.Apply();
            sprite = Sprite.Create(texture,new Rect(0,0,n,n),new Vector2(.5f,.5f),n);
            Cache.Add(name,sprite);
            return sprite;
        }
    }
}
