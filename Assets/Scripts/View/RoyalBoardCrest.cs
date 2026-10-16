using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Mode feedback fitted between the header and projected board. UI objects are reused.
    public sealed class RoyalBoardCrest : MonoBehaviour
    {
        private GameManager _game;
        private UIManager _ui;
        private UIFactory _factory;
        private CanvasGroup _group;
        private RectTransform _root;
        private Canvas _canvas;
        private Camera _camera;
        private GameObject _adventure, _trap, _battle, _tutorial;
        private TMP_Text _caption, _trapTitle, _trapDetail, _multiplier, _battleDetail, _tutorialTitle;
        private readonly Image[] _stars = new Image[3], _sparkles = new Image[18];
        private readonly float[] _starAge = { 1f, 1f, 1f };
        private readonly Image[] _pips = new Image[GameConfig.TransformTrigger];
        private Image _progress, _ring;
        private TMP_Text _pieceGlyph;
        private RectTransform _icon;
        private int _run=-1, _rating=-1, _lastMoves=-1, _lastSecond=-1, _lastMultiplier=-1, _lastCaptures=-1;
        private PieceType _lastForm=(PieceType)(-1);
        private bool _lastTransformed;
        private float _multiplierPop=1f, _iconPop=1f;
        private static readonly Color Dim=new Color(.26f,.31f,.40f,1f);

        public void Initialize(GameManager game, UIManager ui, UIFactory factory, CanvasGroup group)
        {
            _game=game; _ui=ui; _factory=factory; _group=group;
            _root=(RectTransform)transform; _canvas=GetComponentInParent<Canvas>(); _camera=game.cam;
            var backdrop = gameObject.AddComponent<Image>();
            backdrop.sprite = UISprites.RoyalPanel(); backdrop.type = Image.Type.Sliced;
            backdrop.color = new Color(.03f, .055f, .11f, .94f); backdrop.raycastTarget = false;
            _adventure=Section("AdventureStars"); _trap=Section("BiomeProgress");
            _battle=Section("BattleCombo"); _tutorial=Section("TutorialObjective");
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*62f;
                OutlineStar(Picture(_adventure.transform,"EmptyStar",UISprites.Star(),new Vector2(x,15),new Vector2(42,42),Dim));
                Picture(_adventure.transform,"StarInset",UISprites.Star(),new Vector2(x,15),new Vector2(34,34),UIFactory.Bg);
                _stars[i]=Picture(_adventure.transform,"GoldStar",UISprites.Star(),new Vector2(x,15),new Vector2(42,42),UIFactory.Gold);
                OutlineStar(_stars[i]);
                for(int s=0;s<6;s++)
                {
                    var spark=Picture(_adventure.transform,"StarSpark",BoardView.UnitSquare(),Vector2.zero,new Vector2(3,3),UIFactory.Gold);
                    spark.rectTransform.localRotation=Quaternion.Euler(0,0,45); spark.enabled=false; _sparkles[i*6+s]=spark;
                }
            }
            _caption=Label(_adventure.transform,15,-21,360,UIFactory.Muted);
            _caption.outlineColor=Color.black;
            _caption.outlineWidth=.22f;
            _caption.fontMaterial.EnableKeyword("OUTLINE_ON");
            _caption.UpdateMeshPadding();
            _trapTitle=Label(_trap.transform,20,22,360,UIFactory.Text);
            Picture(_trap.transform,"ProgressTrack",BoardView.UnitSquare(),Vector2.zero,new Vector2(252,4),Dim);
            _progress=Picture(_trap.transform,"Progress",BoardView.UnitSquare(),Vector2.zero,new Vector2(252,4),UIFactory.Gold);
            _progress.type=Image.Type.Filled; _progress.fillMethod=Image.FillMethod.Horizontal;
            _trapDetail=Label(_trap.transform,15,-22,360,UIFactory.Muted);
            _multiplier=Label(_battle.transform,38,13,170,UIFactory.Gold);
            _multiplier.rectTransform.anchoredPosition=new Vector2(-48,13);
            _icon=(RectTransform)new GameObject("TransformationIcon",typeof(RectTransform)).transform;
            _icon.SetParent(_battle.transform,false); _icon.anchoredPosition=new Vector2(69,15); _icon.sizeDelta=new Vector2(48,48);
            Picture(_icon,"RingTrack",UISprites.Circle(),Vector2.zero,new Vector2(48,48),Dim);
            _ring=Picture(_icon,"CountdownRing",UISprites.Circle(),Vector2.zero,new Vector2(48,48),UIFactory.Gold);
            _ring.type=Image.Type.Filled; _ring.fillMethod=Image.FillMethod.Radial360; _ring.fillOrigin=2; _ring.fillClockwise=true;
            Picture(_icon,"Enamel",UISprites.Circle(),Vector2.zero,new Vector2(42,42),UIFactory.Panel);
            _pieceGlyph=Label(_icon,32,0,40,UIFactory.Gold);
            _pieceGlyph.gameObject.name="LegacyChessIcon";
            _pieceGlyph.font=UIFactory.ChessFont();
            for(int i=0;i<_pips.Length;i++)
                _pips[i]=Picture(_battle.transform,"CapturePip",UISprites.Circle(),new Vector2(53+i*16,-15),new Vector2(7,7),Dim);
            _battleDetail=Label(_battle.transform,15,-30,360,UIFactory.Muted);
            _tutorialTitle=Label(_tutorial.transform,18,-9,360,UIFactory.Text);
            Picture(_tutorial.transform,"Crown",RoyalIcons.Get("crown"),new Vector2(0,22),new Vector2(30,30),UIFactory.Gold);
            for(int side=-1;side<=1;side+=2)
                Picture(transform,"Filigree",BoardView.UnitSquare(),new Vector2(side*151,12),new Vector2(32,1),new Color(.87f,.73f,.41f,.4f));
        }
        private GameObject Section(string name)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(transform,false);
            ((RectTransform)go.transform).sizeDelta=new Vector2(360,82); return go;
        }
        private static void OutlineStar(Image star)
        {
            var outline=star.gameObject.AddComponent<Outline>();
            outline.effectColor=Color.black;
            outline.effectDistance=new Vector2(2f,-2f);
            outline.useGraphicAlpha=true;
        }
        private Image Picture(Transform parent,string name,Sprite sprite,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform; rt.anchoredPosition=position; rt.sizeDelta=size;
            var image=go.GetComponent<Image>(); image.sprite=sprite; image.color=color; image.raycastTarget=false; return image;
        }
        private TMP_Text Label(Transform parent,float size,float y,float width,Color color)
        {
            var label=_factory.Label(parent,"",size,color,FontStyles.Normal,width);
            label.rectTransform.sizeDelta=new Vector2(width,size+10); label.rectTransform.anchoredPosition=new Vector2(0,y);
            label.textWrappingMode=TextWrappingModes.NoWrap; label.enableAutoSizing=true;
            label.fontSizeMin=size*.75f; label.fontSizeMax=size; label.raycastTarget=false; return label;
        }
        private static void Visible(GameObject go,bool show) { if(go.activeSelf!=show)go.SetActive(show); }
        private void LateUpdate()
        {
            if(_camera==null || _canvas==null)return;
            if(_run!=_game.BoardHudRun)
            {
                _run=_game.BoardHudRun; _rating=-1; _lastMoves=_lastSecond=_lastMultiplier=_lastCaptures=-1;
                _lastForm=(PieceType)(-1); _lastTransformed=false; _multiplierPop=_iconPop=1f;
                for(int i=0;i<3;i++)_starAge[i]=1f;
            }
            bool tutorial=_game.BoardHudTutorial, adventure=_game.BoardHudAdventure&&!tutorial;
            bool battle=!tutorial&&!adventure&&_game.CurrentMode==GameMode.Battle;
            Visible(_tutorial,tutorial); Visible(_adventure,adventure); Visible(_battle,battle); Visible(_trap,!tutorial&&!adventure&&!battle);
            float dt=Time.unscaledDeltaTime;
            // The garden now occupies this space. Trap-region progress lives in
            // the existing header line; retain the functional stars/combo panels.
            if(!tutorial&&!adventure&&!battle) { _group.alpha=0f; return; }
            if(tutorial) { if(_tutorialTitle.text!=_game.BoardObjective)_tutorialTitle.text=_game.BoardObjective; }
            else if(adventure)UpdateAdventure(dt);
            else if(battle)UpdateBattle(dt);
            else UpdateTrap();
            float scale=Mathf.Max(.001f,_canvas.scaleFactor);
            float top=Screen.height*(1f-_ui.HudTopInsetFrac())-8f*scale;
            float bottom=_camera.WorldToScreenPoint(new Vector3(0,5.15f,-1.4f)).y+8f*scale;
            float room=(top-bottom)/scale;
            _group.alpha=room>=38f?1f:0f;
            if(room<38f)return;
            _root.localScale=Vector3.one*Mathf.Min(1f,room/82f);
            _root.anchoredPosition=new Vector2(0,((top+bottom)*.5f-Screen.height)/scale);
        }
        private void UpdateAdventure(float dt)
        {
            int moves=_game.AdventureMoves;
            int rating=moves<=_game.ThreeStarLimit?3:moves<=_game.TwoStarLimit?2:1;
            if(_rating>=0&&rating<_rating&&!SaveData.IsReducedEffects())
                for(int i=rating;i<_rating;i++)_starAge[i]=0f;
            _rating=rating;
            if(_lastMoves!=moves)
            {
                _lastMoves=moves;
                int remaining=(rating==3?_game.ThreeStarLimit:_game.TwoStarLimit)-moves+1;
                _caption.text=rating==1?"ONE STAR SECURED · FINISH THE LEVEL":
                    remaining+(remaining==1?" MOVE":" MOVES")+" UNTIL NEXT STAR DROPS";
            }
            for(int i=0;i<3;i++)
            {
                _starAge[i]=Mathf.Min(1f,_starAge[i]+dt/.32f);
                float t=_starAge[i]; bool lost=i>=rating, popping=lost&&t<1f;
                var star=_stars[i]; star.enabled=!lost||popping;
                star.rectTransform.anchoredPosition=new Vector2((i-1)*62,15+(popping?t*22:0));
                star.rectTransform.localScale=Vector3.one*(popping?1f+.4f*Mathf.Sin(t*Mathf.PI):1f);
                star.color=new Color(UIFactory.Gold.r,UIFactory.Gold.g,UIFactory.Gold.b,popping?1f-t:1f);
                for(int s=0;s<6;s++)
                {
                    var spark=_sparkles[i*6+s]; spark.enabled=popping;
                    if(!popping)continue;
                    float angle=s*Mathf.PI/3f;
                    spark.rectTransform.anchoredPosition=new Vector2((i-1)*62,15)+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(18+28*t);
                    spark.color=new Color(UIFactory.Gold.r,UIFactory.Gold.g,UIFactory.Gold.b,1f-t);
                }
            }
        }
        private void UpdateTrap()
        {
            float seconds=_game.SurvivalSeconds; int second=Mathf.FloorToInt(seconds);
            _progress.fillAmount=seconds<60?seconds/60:seconds<90?(seconds-60)/30:1;
            if(_lastSecond==second)return; _lastSecond=second;
            _trapTitle.text=seconds<60?"GARDENS  →  DRAWBRIDGE":seconds<90?"DRAWBRIDGE  →  CASTLE":"THE MOONSTONE KEEP";
            _trapDetail.text=seconds<90?"NEXT REGION IN "+Mathf.CeilToInt((seconds<60?60:90)-seconds)+"s":
                "SURVIVED "+Clock(second)+"  ·  BEST "+Clock(Mathf.FloorToInt(Mathf.Max(seconds,_game.TrapBestSeconds)));
        }
        private static string Clock(int seconds) => (seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
        private void UpdateBattle(float dt)
        {
            int multiplier=_game.ScoreMultiplier;
            if(_lastMultiplier!=multiplier)
            {
                if(_lastMultiplier>=0&&multiplier>_lastMultiplier&&!SaveData.IsReducedEffects())_multiplierPop=0;
                _lastMultiplier=multiplier; _multiplier.text="×"+multiplier;
            }
            bool transformed=_game.TransformActive; var form=transformed?_game.PlayerForm:PieceType.Knight;
            if(_lastForm!=form||_lastTransformed!=transformed)
            {
                if(transformed&&!SaveData.IsReducedEffects())_iconPop=0;
                _lastForm=form;
                _pieceGlyph.text=GameConfig.Glyph(form);
                // Original HTML PIECES colors and Unicode silhouettes.
                _pieceGlyph.color=UIFactory.Hex(form switch
                {
                    PieceType.Rook=>"#4080E0", PieceType.Bishop=>"#9060E0",
                    PieceType.Queen=>"#E04080", PieceType.Knight=>"#50C878",
                    PieceType.Nightrider=>"#50C878", _=>"#F0C040"
                });
                _lastTransformed=transformed; _lastSecond=_lastCaptures=-1;
            }
            _ring.fillAmount=transformed?Mathf.Clamp01(_game.TransformationSeconds/GameConfig.TransformSeconds):0;
            for(int i=0;i<_pips.Length;i++)
            {
                _pips[i].enabled=!transformed; _pips[i].color=i<_game.TransformationCaptures?UIFactory.Gold:Dim;
            }
            int second=Mathf.CeilToInt(_game.TransformationSeconds), captures=_game.TransformationCaptures;
            if(second!=_lastSecond||captures!=_lastCaptures)
            {
                _lastSecond=second; _lastCaptures=captures;
                _battleDetail.text=transformed?"MULTIPLIER  ·  "+second+"s LEFT":
                    captures+" / "+GameConfig.TransformTrigger+" CAPTURES TO TRANSFORM";
            }
            _multiplierPop=Mathf.Min(1,_multiplierPop+dt/.18f); _iconPop=Mathf.Min(1,_iconPop+dt/.22f);
            _multiplier.rectTransform.localScale=Vector3.one*(1+.16f*Mathf.Sin(_multiplierPop*Mathf.PI));
            _icon.localScale=Vector3.one*(1+.20f*Mathf.Sin(_iconPop*Mathf.PI));
        }
    }
}
