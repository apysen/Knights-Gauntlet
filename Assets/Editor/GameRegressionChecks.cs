#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using KnightsGauntlet.Core;
using KnightsGauntlet.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

// Runs against the real presentation in Play mode. No purchases, daily attempts,
// score submissions or adventure rewards are performed by these checks.
public static class GameRegressionChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator _steps;
    private static GameManager _gm;
    private static GameSimulation _sim;
    private static GameView _view;
    private static BoardView _board;
    private static UIManager _ui;
    private static IAdService _ads;
    private static bool _enabled, _background, _reduced;
    private static readonly List<string> Results = new List<string>();
    private static readonly List<string> RuntimeErrors = new List<string>();
    public static string Status { get; private set; } = "Not run";
    public static int Passed { get; private set; }
    public static int Failed { get; private set; }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Call(object target, string name, params object[] values) => target.GetType().GetMethod(name, Flags).Invoke(target, values);
    private static void Check(bool condition, string name)
    {
        if (condition) Passed++; else Failed++;
        Results.Add((condition ? "PASS " : "FAIL ") + name);
        Status = Results[Results.Count - 1];
    }

    // Golden checksum captured from the original solver before optimization.
    // Includes every route step and outcome across 1,000 deterministic boards.
    private static void CheckEscapeSolverCompatibility()
    {
        var rng = new System.Random(481516);
        long hash = 17;
        for (int sample = 0; sample < 1000; sample++)
        {
            var traps = new List<(int col, int relRow)>();
            for (int row = -20; row <= 12; row++)
            for (int col = 0; col < 5; col++)
                if (rng.NextDouble() < (sample % 10) * .09) traps.Add((col, row));
            var result = EscapeSolver.Compute(rng.Next(5), traps);
            unchecked
            {
                hash = hash * 31 + (int)result.reason;
                hash = hash * 31 + (result.escaped ? 1 : 0);
                foreach (var step in result.path)
                {
                    hash = hash * 31 + step.Col;
                    hash = hash * 31 + step.RelRow;
                    hash = hash * 31 + step.DRow;
                }
            }
        }
        Check(hash == -8058822757117587928L, "Escape solver matches all 1,000 original routes/outcomes");
    }

    public static string Start()
    {
        if (!EditorApplication.isPlaying) return "Enter Play mode first.";
        if (_steps != null) return "Already running: " + Status;
        _gm = Object.FindFirstObjectByType<GameManager>();
        if (_gm == null) return "No GameManager.";
        _sim = Get<GameSimulation>(_gm,"_sim"); _view = Get<GameView>(_gm,"_view");
        _board = Get<BoardView>(_gm,"_board"); _ui = Get<UIManager>(_gm,"_ui");
        _enabled = _gm.enabled; _background = Application.runInBackground; _reduced = SaveData.IsReducedEffects();
        _ads = Get<IAdService>(_gm,"_ads"); Set(_gm,"_ads",new NoOpAdService());
        _gm.enabled = false; Application.runInBackground = true;
        if (_reduced) typeof(SaveData).GetField("_reducedEffectsCache",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,0);
        Results.Clear(); RuntimeErrors.Clear(); Passed = Failed = 0;
        CheckEscapeSolverCompatibility();
        Application.logMessageReceived += OnLog;
        _steps = Run(); EditorApplication.update += Pump;
        Status = "Running"; return Status;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) &&
            (stack.Contains("Assets/Scripts/") || stack.Contains("KnightsGauntlet"))) RuntimeErrors.Add(message);
    }

    private static void Pump()
    {
        try { if (!_steps.MoveNext()) Finish(); }
        catch (Exception exception) { Check(false,"Harness exception: " + exception); Finish(); }
    }

    private static void Finish()
    {
        EditorApplication.update -= Pump; _steps = null;
        Application.logMessageReceived -= OnLog;
        Check(RuntimeErrors.Count == 0,"No game runtime exceptions (" + RuntimeErrors.Count + ")");
        if (_gm != null)
        {
            Set(_gm,"_runEnded",true); _gm.ToMenu();
            Set(_gm,"_ads",_ads); _gm.enabled = _enabled;
        }
        if (_reduced) typeof(SaveData).GetField("_reducedEffectsCache",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,1);
        Application.runInBackground = _background;
        Status = "Completed: " + Passed + " passed, " + Failed + " failed";
        var report = new StringBuilder("# Game regression run\n\n" + DateTime.Now.ToString("O") + "\n\n" + Status + "\n\n");
        foreach (string result in Results) report.AppendLine("- " + result);
        report.AppendLine("\n## Scope\n\nReal GameManager/UI restart and pause flows; rendered board clipping; repeated movement and expired VFX; all adventure paths replayed in isolated simulations. Live billing, ad delivery, leaderboard submission and daily attempt consumption are excluded.");
        foreach (string error in RuntimeErrors) report.AppendLine("\nRuntime error: " + error);
        string folder = Path.GetFullPath(Application.dataPath + "/../ArtSource/Reports");
        Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder,"game-regression.md"),report.ToString());
    }

    private static IEnumerator Run()
    {
        // Assert stable row identity through fractional scroll and rewind.
        var flow = new BoardBiomeFlow(); flow.Advance(0,0); flow.Advance(40.25f,60);
        Check(flow.AtRow(-41) == BoardBiome.Drawbridge && flow.AtRow(-40) == BoardBiome.Grass,"Only incoming row changes at grass/bridge threshold");
        float low,high; flow.Region(BoardBiome.Drawbridge,45.25f,1,5,out low,out high);
        Check(Mathf.Abs(low + .25f)<.0001f,"Scenery transition follows the same moving row boundary");
        flow.Advance(80.25f,90);
        Check(flow.AtRow(-81)==BoardBiome.CastleStone && flow.AtRow(-80)==BoardBiome.Drawbridge && flow.AtRow(-40)==BoardBiome.Grass,"Three biome regions coexist by row");
        flow.Advance(78.25f,91);
        Check(flow.AtRow(-81)==BoardBiome.CastleStone && flow.AtRow(-80)==BoardBiome.Drawbridge,"Rewind preserves terrain history");
        flow.Reset(); Check(flow.AtRow(-1000)==BoardBiome.Grass,"New round clears terrain history");

        var player = Get<PieceView>(_view,"_player");
        _gm.StartAdventureLevel(AdventureLevels.All[0]);
        foreach(var skin in new[]{"emerald","silver","crimson","gold"})
        {
            _view.SetPlayerForm(PieceType.Knight,skin);
            var renderer=Get<Transform>(player,"_modelRoot").GetComponentInChildren<MeshRenderer>();
            var properties=new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
            Check(properties.GetFloat("_AccentStrength")== (skin=="gold"?0f:1f),"3D cosmetic accent applies and resets: " + skin);
        }
        _sim.TutorialClear(); _sim.TutorialSetPlayer(2,8);
        _view.Sync(_sim,true,0);
        // Exactly reproduce a completed one-shot effect still held by GameView.
        var effect = VfxPlayer.Play(Vfx.All[0],player.transform,Vector3.zero,1,15);
        Check(effect != null,"Movement VFX asset loads");
        Set(_view,"_activeMoveVfx",effect);
        float until = Time.realtimeSinceStartup+4f;
        while (effect != null && Time.realtimeSinceStartup < until) yield return null;
        Check(effect == null,"Movement effect naturally destroys itself");
        var reusable = VfxPlayer.Play(Vfx.All[0],player.transform,Vector3.zero,1,15,null,true);
        var effectId = reusable.GetEntityId();
        reusable.Cancel();
        Check(!reusable.gameObject.activeSelf,"Reusable movement effect cancels without destruction");
        reusable = VfxPlayer.Play(Vfx.All[0],player.transform,Vector3.zero,1,15,reusable,true);
        Check(reusable.GetEntityId()==effectId && reusable.gameObject.activeSelf,"Movement effect reuses the same object");
        until=Time.realtimeSinceStartup+4f;
        while(reusable.gameObject.activeSelf && Time.realtimeSinceStartup<until) yield return null;
        Check(!reusable.gameObject.activeSelf,"Reusable movement effect finishes and becomes inactive");
        Object.Destroy(reusable.gameObject);
        var originalRoot=Get<Transform>(player,"_modelRoot");
        player.Apply(_gm.theme,PieceType.Rook,true,"gold");
        player.Apply(_gm.theme,PieceType.Knight,true,"gold");
        Check(Get<Transform>(player,"_modelRoot")==originalRoot,"Piece transformation reuses cached knight model");
        foreach(string model in new[]{"knight","rook","bishop","queen","pawn"})
        {
            var asset=Resources.Load<GameObject>("Pieces/Optimized/"+model);
            long triangles=0;
            if(asset!=null) foreach(var mesh in asset.GetComponentsInChildren<MeshFilter>())
                for(int s=0;s<mesh.sharedMesh.subMeshCount;s++) triangles+=mesh.sharedMesh.GetIndexCount(s)/3;
            Check(triangles>0 && triangles<=12000,"Mobile triangle budget: "+model+" ("+triangles+")");
        }
        for (int i=0;i<8;i++)
        {
            int col = i%2==0 ? 1:2, row = i%2==0 ? 6:8;
            int fromCol = _sim.PlayerCol; float fromRow = _sim.PlayerY;
            Check(_sim.TryTap(col,row).Moved,"Repeated legal move " + (i+1));
            _view.PlayComicMove(fromCol,fromRow,PieceType.Knight,_sim);
            _view.Sync(_sim,true,0);
            Check(Get<bool>(player,"_hopActive"),"Hop starts on move " + (i+1));
            until=Time.realtimeSinceStartup+.22f; while(Time.realtimeSinceStartup<until) yield return null;
            var root=Get<Transform>(player,"_modelRoot");
            Check(!Get<bool>(player,"_hopActive") && root.localPosition.sqrMagnitude < .000001f,"Hop lands cleanly " + (i+1));
            Check(Quaternion.Angle(root.localRotation,BoardView.SurfaceOrientation*Quaternion.Euler(0,_gm.playerPieceRotation,0))<.01f,"Player keeps configured facing " + (i+1));
        }
        _sim.TutorialSetup(GameMode.Battle); _sim.TutorialAddPiece(PieceType.Knight,1,4);
        _view.Sync(_sim,true,0); _sim.Pieces[0].Y=2; _sim.Pieces[0].Base=2; _view.Sync(_sim,true,0);
        var pool=Get<List<PieceView>>(_view,"_piecePool");
        Check(Quaternion.Angle(Get<Transform>(pool[0],"_modelRoot").localRotation,BoardView.SurfaceOrientation*Quaternion.Euler(0,_gm.enemyPieceRotation,0))<.01f,"Enemy knight keeps configured facing after moving up");

        _sim.TutorialSetup(GameMode.Trap); _sim.TutorialSetPlayer(2,8); _view.Sync(_sim,true,0);
        _sim.TryTap(1,6); _view.PlayComicMove(2,8,PieceType.Knight,_sim); _view.Sync(_sim,true,0);
        until=Time.realtimeSinceStartup+.06f; while(Time.realtimeSinceStartup<until) yield return null;
        var visualStart=player.VisualPosition;
        _sim.TryTap(2,8); _view.PlayComicMove(1,6,PieceType.Knight,_sim); _view.Sync(_sim,true,0);
        Check(Vector3.Distance(visualStart,player.VisualPosition)<.001f,"Rapid moves start from the current visible position");
        _view.CancelTransientAnimations();

        _gm.StartAdventureLevel(AdventureLevels.All[36]); _view.Sync(_sim,true,0);
        _gm.StartGame(GameMode.Trap,Difficulty.Medium); _view.Sync(_sim,true,0);
        Check(!Get<bool>(_view,"_flagOn"),"Adventure flag cleared when starting survival");
        Check(!Get<GameObject>(_ui,"_bossHud").activeSelf,"Boss HUD cleared when starting survival");
        _gm.StartAdventureLevel(AdventureLevels.All[0]); _view.Sync(_sim,true,0);
        Check(Get<bool>(_view,"_flagOn"),"Goal flag survives coach-mark cleanup");

        foreach(GameMode mode in new[]{GameMode.Trap,GameMode.Battle})
        foreach(Difficulty difficulty in (Difficulty[])Enum.GetValues(typeof(Difficulty)))
        {
            _gm.StartGame(mode,difficulty);
            int ticks=0; while(_sim.Alive && ticks++<5000) _sim.Tick(1f/30f);
            Check(!_sim.Alive,"Natural death: " + mode + "/" + difficulty);
            Check(!_sim.TryTap(0,0).Moved,"Dead simulation rejects movement: " + mode + "/" + difficulty);
            Call(_gm,"EnterDead"); Call(_gm,"ShowRunResult");
            _view.Sync(_sim,false,0);
            yield return null;
            var gameOver=Get<GameObject>(_ui,"_gameOver");
            Check(gameOver.activeSelf,"Game-over screen visible: " + mode + "/" + difficulty);
            Set(_gm,"_runEnded",true); // Suppress publishing QA scores and awarding currency.
            Button restart=null;
            foreach(var button in gameOver.GetComponentsInChildren<Button>())
                if(button.GetComponentInChildren<TMP_Text>().text=="Restart") restart=button;
            Check(restart!=null,"Game-over restart button exists");
            restart.onClick.Invoke();
            Check(_sim.Alive && Get<object>(_gm,"_state").ToString()=="Playing","Restart after death: " + mode + "/" + difficulty);
            _gm.PauseGame(); Check(Get<object>(_gm,"_state").ToString()=="Paused","Pause " + mode + "/" + difficulty);
            _gm.ResumeGame(); Check(Get<object>(_gm,"_state").ToString()=="Playing","Resume " + mode + "/" + difficulty);
            Set(_gm,"_runEnded",true); _gm.ToMenu(); _gm.StartGame(mode==GameMode.Trap?GameMode.Battle:GameMode.Trap,difficulty);
            Check(_sim.Alive,"Switch mode after completed round: " + mode + "/" + difficulty);
        }

        foreach(var level in AdventureLevels.All)
        {
            _gm.StartAdventureLevel(level); _board.SyncBackground(_sim.ScrollCells,0,Get<BoardBiome?>(_gm,"_adventureBiomeOverride"));
            _view.Sync(_sim,true,0); Canvas.ForceUpdateCanvases();
            var label=Get<TMP_Text>(_ui,"_advMovesLabel"); label.ForceMeshUpdate();
            Check(!label.isTextOverflowing,"HUD fits " + level.Id + " " + level.Name);
            var biome=_gm.CurrentBiome; _gm.PauseGame();
            _board.SyncBackground(_sim.ScrollCells,0,Get<BoardBiome?>(_gm,"_adventureBiomeOverride"));
            Check(_gm.CurrentBiome==biome,"Pause preserves biome " + level.Id);
            _gm.ResumeGame();
            Check(Get<object>(_gm,"_state").ToString()=="Adventure","Resume adventure " + level.Id);
            if(level.IsBoss()) foreach(var phase in level.Phases) ReplayAdventure(phase,true);
            else if(level.ScrollSpeed<=0) ReplayAdventure(level,false);
            yield return null;
        }

        _gm.StartGame(GameMode.Trap,Difficulty.Medium); _board.SyncBackground(0,0);
        Call(_ui,"Update");
        _board.SyncBackground(40.25f,60); Check(VisibleBiomes()==2,"Grass and bridge coexist during scroll transition");
        _board.SyncBackground(45.25f,62); Check(VisibleBiomes()==2,"Transition continues across board");
        _board.SyncBackground(51.25f,65); Check(VisibleBiomes()==1 && _board.CurrentBiome==BoardBiome.Drawbridge,"Bridge replaces grass after full board length");
        _board.SyncBackground(80.25f,90); Check(VisibleBiomes()==2,"Bridge and castle coexist during scroll transition");
        _board.SyncBackground(91.25f,98); Check(VisibleBiomes()==1 && _board.CurrentBiome==BoardBiome.CastleStone,"Castle replaces bridge after full board length");
        Call(_ui,"Update");
        Check(Get<int>(_ui,"_biomeShown")==2,"HUD biome updates without a screen resize");
        foreach(float fraction in new[]{0f,.25f,.5f,.75f,.999f}) CheckClipping(fraction);
        _gm.StartAdventureLevel(AdventureLevels.All[0]);
        _board.SyncBackground(0,0,BoardBiome.Grass);
        int correct=0; for(int r=0;r<10;r++) for(int c=0;c<5;c++)
        {
            var cell=_board.WorldToCell(_board.ScreenToWorld(_gm.cam,_gm.cam.WorldToScreenPoint(_board.CellToWorld(c,r))));
            if(cell.col==c && Mathf.Abs(cell.simY-r)<.001f) correct++;
        }
        Check(correct==50,"All 50 projected cell targets round-trip");
        _gm.StartTrapTutorial(Difficulty.Medium,true); Check(_sim.Tutorial && !_sim.Adventure,"Trap tutorial resets adventure state");
        _gm.StartBattleTutorial(Difficulty.Medium,true); Check(_sim.Tutorial && _sim.Mode==GameMode.Battle,"Battle tutorial opens");
        _gm.ToMenu();
        foreach(string screen in new[]{"ShowSettings","ShowMarketplace","ShowCustomization","ShowAchievements","ShowBugReport","ShowTutorialPick"})
        {
            Call(_ui,screen); Check(true,"UI navigation " + screen); yield return null;
        }
        _ui.ShowLevelSelect(); Check(true,"Level selection opens");
        CheckPureSimulation();
    }

    [MenuItem("Tools/Knight's Gauntlet/Run Play Mode Regression Checks")]
    private static void StartFromMenu() => Debug.Log(Start());

    private static int VisibleBiomes()
    {
        var set=new HashSet<int>();
        foreach(var tile in Get<Array>(_board,"_tiles"))
        {
            var root=(Transform)tile.GetType().GetField("root").GetValue(tile);
            if(Mathf.Abs(root.position.y)>5.47f) continue;
            set.Add((int)tile.GetType().GetField("biome").GetValue(tile));
        }
        return set.Count;
    }

    private static void ReplayAdventure(LevelData level,bool boss)
    {
        var sim=new GameSimulation(); sim.TutorialSetup(GameMode.Trap); sim.TutorialClear();
        sim.AdvTransform=level.Transform; sim.MapRows=level.MapRows;
        if(level.Traps!=null) foreach(var t in level.Traps) sim.TutorialAddTrap(t.Col,t.Row);
        if(level.Cracked!=null) foreach(var t in level.Cracked) sim.AdventureAddCracked(t.Col,t.Row);
        if(level.Enemies!=null) foreach(var e in level.Enemies) sim.AdventureAddEnemy(e.Type,e.Col,e.Row,e.OffCol,e.OffRow,e.Hunter);
        sim.TutorialSetPlayer(level.StartCol,level.StartRow);
        bool goal=level.Objective!=Objective.CaptureAll;
        var path=boss ? AdventureSolver.SolveBoss(level.StartCol,level.StartRow,0,sim.AdvEnemies,sim.AdvTrapAt,
            Math.Max(9,level.MapRows-1),goal,level.GoalCol,level.GoalRow,sim.Cracked) :
            AdventureSolver.Solve(level.StartCol,level.StartRow,0,sim.AdvEnemies,sim.AdvTrapAt,Math.Max(9,level.MapRows-1),
                goal,level.GoalCol,level.GoalRow,level.Transform,PieceType.Knight,0,sim.Cracked);
        bool legal=path!=null;
        if(path!=null) foreach(var move in path)
        {
            if(!sim.TryTap(move.Col,move.Row).Moved || sim.AdventureAfterMove()) { legal=false; break; }
        }
        bool won=legal && (!goal || (sim.PlayerCol==level.GoalCol && Mathf.RoundToInt(sim.PlayerY)==level.GoalRow)) &&
            (level.Objective==Objective.Reach || sim.AdvAllCaptured());
        Check(won,"Replay complete route " + level.Id + " " + level.Name + (path==null?" (no route)":" ("+path.Count+" moves)"));
    }

    private static void CheckPureSimulation()
    {
        var a=new GameSimulation(); var b=new GameSimulation();
        a.Start(GameMode.Trap,Difficulty.Grandmaster,12345,true,false); b.Start(GameMode.Trap,Difficulty.Grandmaster,12345,true,false);
        bool same=a.Pieces.Count==b.Pieces.Count;
        for(int i=0;same && i<a.Pieces.Count;i++) same=a.Pieces[i].Col==b.Pieces[i].Col && a.Pieces[i].Y==b.Pieces[i].Y;
        Check(same && a.Pickups.Count==0,"Daily seeded rules are deterministic without consuming attempts");
        var sim=new GameSimulation(); sim.TutorialSetup(GameMode.Battle);
        for(int i=0;i<8 && !sim.TransformActive;i++)
        {
            sim.Pieces.Clear(); var move=sim.GetValidMoves()[0];
            sim.TutorialAddPiece(PieceType.Rook,move.Col,Mathf.RoundToInt(move.Y)); sim.TryTap(move.Col,move.Y);
        }
        Check(sim.TransformActive && sim.PlayerForm!=PieceType.Knight,"Battle capture chain transforms player");
        sim.TutorialEnd(); for(int i=0;i<3000 && sim.TransformActive;i++) sim.Tick(1f/30f);
        Check(!sim.TransformActive && sim.PlayerForm==PieceType.Knight,"Battle transformation expires");
        sim.Start(GameMode.Trap,Difficulty.Medium,12345); while(sim.Alive) sim.Tick(1f/30f);
        sim.Revive(); sim.Tick(1f/30f); Check(sim.Alive,"Revive grants grace and safe footing");
    }

    private static void CheckClipping(float fraction)
    {
        _board.SyncBackground(fraction,0,BoardBiome.Grass);
        var tileRoot=_board.transform.Find("ModeledTiles");
        var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var enabled=new bool[renderers.Length];
        var cam=_gm.cam; var target=new RenderTexture(480,Mathf.RoundToInt(480/cam.aspect),24);
        var previous=cam.targetTexture; var active=RenderTexture.active; var background=cam.backgroundColor;
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        try
        {
            for(int i=0;i<renderers.Length;i++) { enabled[i]=renderers[i].enabled; renderers[i].enabled &= renderers[i].transform.IsChildOf(tileRoot); }
            cam.backgroundColor=Color.black; cam.targetTexture=target; cam.Render(); RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0); texture.Apply();
            bool clean=true;
            foreach(float y in new[]{-5.08f,5.08f}) for(int col=-2;col<=2;col++)
            {
                var screen=cam.WorldToScreenPoint(new Vector3(col,y,0));
                var pixel=texture.GetPixel(Mathf.RoundToInt(screen.x),Mathf.RoundToInt(screen.y));
                clean &= pixel.maxColorComponent<.025f;
            }
            Check(clean,"No rendered tiles beyond perimeter at fractional scroll " + fraction);
        }
        finally
        {
            for(int i=0;i<renderers.Length;i++) if(renderers[i]!=null) renderers[i].enabled=enabled[i];
            cam.targetTexture=previous; cam.backgroundColor=background; RenderTexture.active=active;
            Object.DestroyImmediate(texture); target.Release(); Object.DestroyImmediate(target);
        }
    }
}
#endif
