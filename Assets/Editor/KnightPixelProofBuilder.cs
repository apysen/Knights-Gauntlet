using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KnightsGauntlet.Core;
using KnightsGauntlet.View;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Isolated animation proof. Generates optional prefabs and captures the actual
// board without changing GameTheme, PieceView or the gameplay scene.
public static class KnightPixelProofBuilder
{
    private const string Folder = "Assets/Art/KnightPixelTest/";
    private static readonly int[] Sizes = { 48, 64, 96 };
    private static readonly int[] Durations = {125,125,125,125,125,125,125,125,70,35,145};

    [Serializable] private sealed class ReviewSample
    {
        public int bodyFrame;
        public float captureTime;
        public int durationMs;
        public ReviewSample(int body, float time, int duration)
        { bodyFrame=body; captureTime=time; durationMs=duration; }
    }
    [Serializable] private sealed class ReviewTimeline
    {
        public string effect = "Unity GPU pixel shader";
        public ReviewSample[] samples;
    }
    private static ReviewSample[] ReviewSamples()
    {
        var result = new List<ReviewSample>();
        for (int i=0; i<8; i++) result.Add(new ReviewSample(i,-1,125));
        foreach (var pair in new[] {new Vector2(0,70), new Vector2(.070f,20),
            new Vector2(.090f,15),new Vector2(.105f,20),new Vector2(.125f,20),
            new Vector2(.145f,20),new Vector2(.165f,20),new Vector2(.185f,20),
            new Vector2(.205f,20),new Vector2(.225f,25),new Vector2(.250f,20),
            new Vector2(.270f,20),new Vector2(.290f,60)})
            result.Add(new ReviewSample(pair.x < .07f ? 8 : pair.x < .105f ? 9 : pair.x < .25f ? 10 : 0,pair.x,(int)pair.y));
        return result.ToArray();
    }

    [MenuItem("Knight's Gauntlet/Art/Build Pixel Knight Animation Proof")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "PixelKnight.mat");
        if (material == null)
        {
            var shader = Resources.Load<Shader>("ComicEffectOverlay");
            if (shader == null) throw new InvalidOperationException("ComicEffectOverlay shader missing.");
            material = new Material(shader) { name = "Pixel Knight Proof" };
            AssetDatabase.CreateAsset(material, Folder + "PixelKnight.mat");
        }
        var slashTemplate = AssetDatabase.LoadAssetAtPath<Material>(Folder + "PixelSlashRender.mat");
        if (slashTemplate == null)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "PixelKnightSlash.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Pixel slash shader not ready.");
            slashTemplate = new Material(shader) { name = "Pixel Slash Render" };
            AssetDatabase.CreateAsset(slashTemplate,Folder + "PixelSlashRender.mat");
        }
        foreach (int size in Sizes)
        {
            string path = Folder + "knight-" + size + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = size;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
            var rects = new SpriteMetaData[Durations.Length];
            for (int i = 0; i < rects.Length; i++)
                rects[i] = new SpriteMetaData { name = "knight_" + i.ToString("D2"),
                    rect = new Rect(i * size, 0, size, size), alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(.5f, .22f) };
#pragma warning disable CS0618
            importer.spritesheet = rects;
#pragma warning restore CS0618
            importer.SaveAndReimport();
            var frames = LoadFrames(size);
            if (frames.Length != Durations.Length) throw new InvalidOperationException("Expected 11 sprites at " + size);
            var idle = Clip(size, "Idle", frames, 0, 8, true);
            var capture = Clip(size, "Capture", frames, 8, 3, false);
            string controllerPath = Folder + "Knight" + size + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                var idleState = machine.AddState("Idle"); idleState.motion = idle;
                var attackState = machine.AddState("Capture"); attackState.motion = capture;
                machine.defaultState = idleState;
                var attack = idleState.AddTransition(attackState);
                attack.hasExitTime = true; attack.exitTime = 1; attack.duration = 0;
                var recover = attackState.AddTransition(idleState);
                recover.hasExitTime = true; recover.exitTime = 1; recover.duration = 0;
            }
            var go = new GameObject("PixelKnight_" + size + "_Demo");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0]; sr.sharedMaterial = material; sr.sortingOrder = 200;
                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                var effect = new GameObject("PixelSlash"); effect.transform.SetParent(go.transform,false);
                var effectRenderer = effect.AddComponent<SpriteRenderer>();
                effectRenderer.sharedMaterial = material; effectRenderer.sortingOrder = 199;
                effectRenderer.enabled = false;
                var slash = go.AddComponent<PixelKnightSlash>();
                slash.PixelSize = size; slash.ShaderTemplate = slashTemplate;
                slash.EffectRenderer = effectRenderer; slash.BodyAnimator = animator;
                go.transform.localScale = Vector3.one * DisplayScale(size);
                PrefabUtility.SaveAsPrefabAsset(go, Folder + "PixelKnight-" + size + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Pixel knight] Built 250ms rear-view proofs with 3 body poses and a separate pixel shader slash.");
    }

    private static Sprite[] LoadFrames(int size) => AssetDatabase.LoadAllAssetsAtPath(Folder + "knight-" + size + ".png")
        .OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();

    private static float DisplayScale(int size)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "knight-" + size + ".png");
        int bottom = size, top = 0;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            if (texture.GetPixel(x, y).a > .5f) { bottom = Math.Min(bottom, y); top = Math.Max(top, y); }
        return .95f * size / (top - bottom + 1);
    }

    private static AnimationClip Clip(int size, string name, Sprite[] sprites, int from, int count, bool loop)
    {
        string path = Folder + "Knight" + size + "-" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = 40;
        var keys = new ObjectReferenceKeyframe[count + 1];
        float time = 0;
        for (int i = 0; i < count; i++)
        {
            keys[i] = new ObjectReferenceKeyframe { time = time, value = sprites[from + i] };
            time += Durations[from + i] * .001f;
        }
        // Object-reference curves retain their final key for one sample interval.
        // Put the terminal hold one interval early so clip and GIF timings agree.
        keys[count] = new ObjectReferenceKeyframe { time = time - 1f / clip.frameRate, value = sprites[from + count - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    [MenuItem("Knight's Gauntlet/Art/Capture Pixel Knight Board Proof")]
    public static void CaptureBoard()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Capture the art proof in Edit mode.");
        var previous = SceneManager.GetActiveScene();
        var random = UnityEngine.Random.state;
        var clip = Shader.GetGlobalVector("_KGBoardClip");
        var roll = Shader.GetGlobalVector("_KGBoardRoll");
        var end = Shader.GetGlobalVector("_KGBoardEnd");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        GameTheme theme = null;
        RenderTexture target = null;
        Texture2D image = null;
        var active = RenderTexture.active;
        try
        {
            theme = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameTheme>("Assets/GameTheme.asset"));
            var camera = new GameObject("PixelKnightProofCamera").AddComponent<Camera>();
            camera.enabled = false; camera.nearClipPlane = .1f;
            new GameObject("PixelKnightProofLight").AddComponent<Light>().type = LightType.Directional;
            var board = new GameObject("CurrentProductionBoard").AddComponent<BoardView>();
            board.Init(theme); board.SyncBackground(0);
            board.FitCamera(camera, .12f, .05f);
            var view = new GameObject("CurrentPiecesForContext").AddComponent<GameView>();
            view.Init(theme, board);
            var sim = new GameSimulation(); sim.Start(GameMode.Battle, Difficulty.Easy, 17);
            sim.Tutorial = true; sim.Pieces.Clear(); sim.Pickups.Clear();
            sim.Pieces.Add(new GamePiece(PieceType.Rook, 0, 2));
            sim.Pieces.Add(new GamePiece(PieceType.Bishop, 4, 3));
            sim.Pieces.Add(new GamePiece(PieceType.Pawn, 1, 7));
            view.Sync(sim, false, 0);
            var player = (PieceView)typeof(GameView).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            player.gameObject.SetActive(false);
            var sprites = new List<SpriteRenderer>();
            var effects = new List<PixelKnightSlash>();
            foreach (int size in Sizes)
            {
                int col = Array.IndexOf(Sizes, size) * 2;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PixelKnight-" + size + ".prefab"));
                go.GetComponent<Animator>().enabled = false;
                go.transform.position = board.CellToWorld(col, 5.3f);
                go.transform.rotation = camera.transform.rotation;
                sprites.Add(go.GetComponent<SpriteRenderer>());
                effects.Add(go.GetComponent<PixelKnightSlash>());
                var label = new GameObject(size + "px label").AddComponent<TextMeshPro>();
                label.font = UIFactory.ComicFont(); label.text = size + " PX";
                label.fontSize = 1.8f; label.alignment = TextAlignmentOptions.Center;
                label.color = UIFactory.Gold;
                label.rectTransform.sizeDelta = new Vector2(1, .3f);
                label.transform.rotation = camera.transform.rotation;
                label.transform.position = go.transform.position + camera.transform.up * 1.12f;
            }
            foreach (var root in scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            camera.cullingMask = 1 << 31;
            int height = 960;
            int width = Mathf.RoundToInt(height * (float)Screen.width / Mathf.Max(1, Screen.height));
            target = RenderTexture.GetTemporary(width, height, 24);
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target; camera.aspect = (float)width / height;
            string output = Path.GetFullPath("ArtSource/PixelKnightTest/board");
            Directory.CreateDirectory(output);
            var banks = Sizes.Select(LoadFrames).ToArray();
            var samples = ReviewSamples();
            Directory.CreateDirectory(Path.GetFullPath("ArtSource/PixelKnightTest"));
            File.WriteAllText("ArtSource/PixelKnightTest/review-timeline.json",JsonUtility.ToJson(new ReviewTimeline { samples=samples },true));
            for (int frame = 0; frame < samples.Length; frame++)
            {
                for (int i = 0; i < 3; i++)
                {
                    sprites[i].sprite = banks[i][samples[frame].bodyFrame];
                    effects[i].RenderAtCaptureTime(samples[frame].captureTime);
                }
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, frame.ToString("D2") + ".png"), image.EncodeToPNG());
            }
            CaptureSpriteReviews(samples);
            Debug.Log("[Pixel knight] Actual board comparison saved to " + output);
        }
        finally
        {
            RenderTexture.active = active;
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (image != null) Object.DestroyImmediate(image);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            if (theme != null) Object.DestroyImmediate(theme);
            Shader.SetGlobalVector("_KGBoardClip", clip);
            Shader.SetGlobalVector("_KGBoardRoll", roll);
            Shader.SetGlobalVector("_KGBoardEnd", end);
            UnityEngine.Random.state = random;
        }
    }

    private static void CaptureSpriteReviews(ReviewSample[] samples)
    {
        // Capture the real shader+sprite composition at its native resolution.
        // Same quad, same foot pivot, integer pixel grid, no supersampling.
        foreach (int size in Sizes)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PixelKnight-" + size + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var cameraObject = new GameObject("NativePixelProofCamera");
            var target = RenderTexture.GetTemporary(size,size,24,RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(size,size,TextureFormat.RGBA32,false);
            var previous = RenderTexture.active;
            try
            {
                var origin = new Vector3(1000,1000,1000);
                go.transform.position = origin; go.transform.localScale = Vector3.one;
                go.GetComponent<Animator>().enabled = false;
                foreach (var child in go.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled=false;
                camera.orthographic=true; camera.orthographicSize=.5f; camera.aspect=1;
                camera.transform.position=origin+new Vector3(0,.28f,-10);
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.clear;
                camera.cullingMask=1<<30; camera.targetTexture=target;
                var body=go.GetComponent<SpriteRenderer>(); var effect=go.GetComponent<PixelKnightSlash>();
                var bank=LoadFrames(size);
                string path="ArtSource/PixelKnightTest/shader-review/"+size;
                Directory.CreateDirectory(path);
                for (int i=0; i<samples.Length; i++)
                {
                    body.sprite=bank[samples[i].bodyFrame];
                    effect.RenderAtCaptureTime(samples[i].captureTime);
                    camera.Render(); RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,size,size),0,0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(path,i.ToString("D2")+".png"),pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active=previous;
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(go);
                Object.DestroyImmediate(pixels); RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
