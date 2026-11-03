using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace KnightsGauntlet.View
{
    // The sound effect a game event maps to. Kept separate from the clip files so
    // call sites read as intent ("play the capture sound") not filenames.
    public enum Sfx
    {
        ShillingCollect, // trap mode: picked up a shilling
        TrapLevelUp,     // trap mode: crossed a score threshold (speed level up)
        ShieldBreak,     // battle: a transform absorbed a hit
        PlaySelect,      // pressed Play, or picked an adventure level
        PieceMove,       // battle/adventure: moved without capturing
        PieceCapture,    // battle/adventure: captured a piece
        TabSelect,       // difficulty / leaderboard tab / customization tab
        PageTurn,        // How to Play next / previous page
        Transform,          // battle/adventure: took on a captured piece's form
        Untransform,        // form ran out when the timer expired
        Transformation,     // battle/adventure: a NEW transform begins (fires once per nightrider)
        AdventureNextLevel, // adventure: reached the level goal
        CoinLand,        // a flying shilling lands in the HUD counter
        BossPhase,       // the queen drops a phase (layered on the capture)
        QueenShieldUp,   // a boss phase starts with the queen's bubble shield up
        QueenShieldPop,  // her last minion falls and the bubble pops
        PowerupChrono,   // Chronokinesis activated
        PowerupDivine,   // Divine Intervention activated
        PowerupPath,     // Divine Path (adventure) activated
        TransformHum,    // the quiet loop while a transformation lasts
        PowerupWarHorn,  // War Horn (battle) activated: the horn blast
        RallyTick,       // War Horn count-up step (use PlayRallyTick for the rising pitch)
        RallySlam,       // War Horn count lands on the peak multiplier
        PowerupBerserker, // Berserker Draught (battle) activated: pop, gulp, flame, hit
        FuryCrackle,     // a capture made while the draught burns (quiet fire burst)
        MenuEntrance,    // main menu entrance: a soft airy swell (UI group)
        MenuTitleTinks,  // main menu entrance: one tiny tink per 3D title letter landing (UI group)
        KnightTap        // main menu: tapped the knight portrait, a light armour clank (UI group)
    }

    // One shared AudioSource that plays short UI/gameplay sounds. Clips live in
    // Assets/Resources/SFX/ and are loaded by filename at startup, so any layer can
    // fire a sound with a single static call: SfxManager.Play(Sfx.PieceCapture).
    //
    // GameManager creates one of these at boot. If a clip file is missing the entry
    // is simply skipped (with a warning), so a missing sound never breaks the game.
    //
    // Music lives in SfxManager.Music.cs.
    public partial class SfxManager : MonoBehaviour
    {
        private static SfxManager _instance;
        private AudioSource _src;
        private AudioSource _uiSrc; // menu taps, so they sit on the UI mixer group

        // Variant banks for the 3D style: numbered round robins, picked without repeating
        // the last one, with a little pitch and volume jitter so repeats never sound
        // identical. Own voices so the jitter never retunes another sound.
        private class Bank { public readonly List<AudioClip> Clips = new List<AudioClip>(); public int Last = -1; public float LastTime = -10f; }
        private readonly Dictionary<Sfx, Bank> _banks = new Dictionary<Sfx, Bank>();
        private AudioSource[] _bankSources;
        private int _bankSourceIndex;
        private AudioSource _loopSrc; // a single looping gameplay sound (e.g. the transform buff)
        private AudioSource[] _captureSources;
        private int _captureSourceIndex;
        private readonly List<AudioClip> _captureClips = new List<AudioClip>();
        private int _lastCaptureClip = -1;

        // Shilling chain, the classic platformer coin run: each pickup soon after the
        // last climbs one step up a major scale, capped at an octave so it never
        // turns shrill, and resets after a pause. Own voices so a rising note never
        // retunes one that is still ringing.
        private const float ShillingChainWindow = 2f;
        private static readonly int[] ShillingScale = { 0, 2, 4, 5, 7, 9, 11, 12 };
        private AudioSource[] _shillingSources;
        private int _shillingSourceIndex, _shillingChain;
        private float _lastShillingTime = -10f;
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();

        // Mixer with Music, SFX and UI groups under Master. Loaded from Resources so
        // no scene wiring is needed. All groups sit at 0 dB, so routing through it
        // changes nothing you can hear; it is the place for the Chronokinesis low-pass
        // and per-bus trims later. Exposed volumes: MusicVolume, SfxVolume, UiVolume
        // (in dB). If the asset is missing, sources just play unrouted.
        private const string MixerPath = "Audio/KGMixer";

        // Game sounds (CC0, see ArtSource/Audio/LICENSES.md). Each entry is a bank:
        // "Name" loads "Name 1", "Name 2", ... if numbered files exist, else "Name" alone.
        // The few sounds without a bank use the single clips in Files below.
        private static readonly Dictionary<Sfx, string> ModernFiles = new Dictionary<Sfx, string>
        {
            { Sfx.PieceMove,       "SFX/Move Wood" },
            { Sfx.ShillingCollect, "SFX/Coin Ting" },
            { Sfx.TrapLevelUp,     "SFX/Level Up Fanfare" },
            { Sfx.CoinLand,        "SFX/Coin Land" },
            { Sfx.BossPhase,       "SFX/Boss Phase Hit" },
            { Sfx.QueenShieldUp,   "SFX/Queen Shield Up" },
            { Sfx.QueenShieldPop,  "SFX/Queen Shield Pop" },
            { Sfx.PowerupChrono,   "SFX/Powerup Chronokinesis" },
            { Sfx.PowerupDivine,   "SFX/Powerup Divine" },
            { Sfx.PowerupPath,     "SFX/Powerup Divine Path" },
            // The transformation set, from the score's palette (ArtSource/Audio/MusicBuild).
            { Sfx.Transformation,  "SFX/Transform Whoosh" },
            { Sfx.TransformHum,    "SFX/Transform Hum" },
            { Sfx.Untransform,     "SFX/Transform Poof" },  // the form's timer ran out
            { Sfx.ShieldBreak,     "SFX/Transform Break" }, // the form shattered taking a hit
            // The War Horn rally (SfxManager.Rally.cs, built by MusicBuild/build_warhorn.py).
            { Sfx.PowerupWarHorn,  "SFX/Powerup War Horn" },
            { Sfx.RallyTick,       "SFX/Rally Tick" },
            { Sfx.RallySlam,       "SFX/Rally Slam" },
            { Sfx.PowerupBerserker, "SFX/Powerup Berserker" },
            { Sfx.FuryCrackle,     "SFX/Fury Crackle" },
            // Adventure goal reached: horn call over Bb, C, landing on D major (MusicBuild/
            // build_level_complete.py).
            { Sfx.AdventureNextLevel, "SFX/Adventure Level Complete" },
            // Main menu (synthesized, MusicBuild/build_warhorn.py). Played on the UI voice.
            { Sfx.MenuEntrance,    "SFX/Menu Entrance Whoosh" },
            { Sfx.MenuTitleTinks,  "SFX/Menu Title Tinks" },
            { Sfx.KnightTap,       "SFX/Knight Clank" },    // Knight Clank 1-3, round robin
            // UI clicks (synthesized, MusicBuild/build_ui_clicks.py), round robin on the UI voice.
            { Sfx.TabSelect,       "SFX/UI Tap" },          // UI Tap 1-4: every button and tab
            { Sfx.PageTurn,        "SFX/Page Turn" },       // Page Turn 1-3: How to Play pages
            { Sfx.PlaySelect,      "SFX/Play Select" },     // Play Select 1-2: Play, level pick
        };

        // Per-sound trim (dB) on top of the file's own level, plus a minimum gap so a
        // burst of coins landing together doesn't pile up into one loud clatter.
        private static float BankTrimDb(Sfx id) => id == Sfx.CoinLand ? -2f : 0f;
        private static float BankMinGap(Sfx id) => id == Sfx.CoinLand ? 0.06f : id == Sfx.FuryCrackle || id == Sfx.KnightTap ? 0.08f : 0f;
        // Melodic or musical sounds get no pitch jitter (the shilling chain sets its own).
        private static bool BankJitterPitch(Sfx id) => id == Sfx.PieceMove || id == Sfx.CoinLand || id == Sfx.QueenShieldPop || id == Sfx.FuryCrackle;

        // Sounds that belong on the UI group rather than SFX.
        private static bool IsUi(Sfx id) => id == Sfx.PlaySelect || id == Sfx.TabSelect || id == Sfx.PageTurn ||
                                            id == Sfx.MenuEntrance || id == Sfx.MenuTitleTinks || id == Sfx.KnightTap;

        // ── Default volumes ──────────────────────────────────────────────────
        // EDIT THESE to change the out-of-the-box volume levels (0 = silent, 1 = full).
        // They only apply until the player moves a slider, which saves their own value.
        // Music is capped: it never plays louder than MaxMusicVolume (the full-right end
        // of the music slider), so the theme stays quietly in the background.
        // Leave both ceilings at 0.1 until the clips are loudness-normalized: they are
        // what keeps today's hot, mismatched files at a sane level, and raising them
        // alone would make the game much louder for everyone with a saved volume.
        public const float MaxMusicVolume     = 0.1f;   // hard ceiling: music tops out at 10%
        public const float DefaultMusicVolume = 0.1f;   // must be within [0, MaxMusicVolume]
        public const float MaxSfxVolume       = 0.1f;   // hard ceiling: sfx top out at 10%
        public const float DefaultSfxVolume   = 0.1f;   // must be within [0, MaxSfxVolume]

        // Current volumes, persisted so the player's choice survives a restart. The music
        // sources pick up a new MusicVolume on the next frame, so a slider drag is heard at once.
        // Music is clamped to [0, MaxMusicVolume]; this is the real AudioSource volume,
        // not the 0..1 slider position (see the Settings slider for the mapping).
        public static float MusicVolume
        {
            get
            {
                // Cached: the music voices read this every frame.
                if (_musicVolCache < 0f) _musicVolCache = Mathf.Clamp(PlayerPrefs.GetFloat("kg_music_vol", DefaultMusicVolume), 0f, MaxMusicVolume);
                return _musicVolCache;
            }
            set
            {
                float v = Mathf.Clamp(value, 0f, MaxMusicVolume);
                _musicVolCache = v;
                PlayerPrefs.SetFloat("kg_music_vol", v); PlayerPrefs.Save();
            }
        }
        private static float _musicVolCache = -1f;
        // SFX is clamped to [0, MaxSfxVolume] the same way music is, so both top out at 10%.
        public static float SfxVolume
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat("kg_sfx_vol", DefaultSfxVolume), 0f, MaxSfxVolume);
            set { PlayerPrefs.SetFloat("kg_sfx_vol", Mathf.Clamp(value, 0f, MaxSfxVolume)); PlayerPrefs.Save(); }
        }

        // Single clips (Resources path, no extension) for sounds without a bank above.
        private static readonly Dictionary<Sfx, string> Files = new Dictionary<Sfx, string>
        {
            { Sfx.PieceCapture,    "SFX/Capture Slash" },
        };

        // Global mute, backed by a pref so a future Settings toggle can flip it.
        public static bool Muted
        {
            get => PlayerPrefs.GetInt("kg_muted", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("kg_muted", value ? 1 : 0); PlayerPrefs.Save();
                // Muting used to only block new sounds, so music and loops played on.
                if (_instance == null) return;
                _instance.OnMutedChanged(value);
                if (value && _instance._loopSrc != null) _instance._loopSrc.Stop();
            }
        }

        // Converts a decibel offset to a linear volume multiplier.
        private static float Db(float db) => Mathf.Pow(10f, db / 20f);

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            var mixer = Resources.Load<AudioMixer>(MixerPath);
            if (mixer == null) Debug.LogWarning("[SFX] Missing mixer at Assets/Resources/" + MixerPath + ".mixer");
            AudioMixerGroup musicGroup = Group(mixer, "Music"), sfxGroup = Group(mixer, "SFX"), uiGroup = Group(mixer, "UI");

            // Priority (0 = most important) decides who loses a voice when the phone runs
            // out: music and the transform sting first, then UI, captures, coins, the rest.
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f; // 2D
            _src.outputAudioMixerGroup = sfxGroup;
            _src.priority = 128;

            _uiSrc = gameObject.AddComponent<AudioSource>();
            _uiSrc.playOnAwake = false;
            _uiSrc.spatialBlend = 0f;
            _uiSrc.outputAudioMixerGroup = uiGroup;
            _uiSrc.priority = 64;

            InitMusic(musicGroup);
            InitRally(sfxGroup);

            _loopSrc = gameObject.AddComponent<AudioSource>();
            _loopSrc.playOnAwake = false;
            _loopSrc.loop = true;
            _loopSrc.spatialBlend = 0f;
            _loopSrc.outputAudioMixerGroup = sfxGroup;
            _loopSrc.priority = 32;

            // Separate voices let fast capture chains rise in pitch without
            // retuning a sound that is still ringing from the previous hit.
            _captureSources = new AudioSource[4];
            for (int i = 0; i < _captureSources.Length; i++)
            {
                _captureSources[i] = gameObject.AddComponent<AudioSource>();
                _captureSources[i].playOnAwake = false;
                _captureSources[i].spatialBlend = 0f;
                _captureSources[i].outputAudioMixerGroup = sfxGroup;
                _captureSources[i].priority = 96;
            }

            _shillingSources = new AudioSource[3];
            for (int i = 0; i < _shillingSources.Length; i++)
            {
                _shillingSources[i] = gameObject.AddComponent<AudioSource>();
                _shillingSources[i].playOnAwake = false;
                _shillingSources[i].spatialBlend = 0f;
                _shillingSources[i].outputAudioMixerGroup = sfxGroup;
                _shillingSources[i].priority = 112;
            }

            _bankSources = new AudioSource[3];
            for (int i = 0; i < _bankSources.Length; i++)
            {
                _bankSources[i] = gameObject.AddComponent<AudioSource>();
                _bankSources[i].playOnAwake = false;
                _bankSources[i].spatialBlend = 0f;
                _bankSources[i].outputAudioMixerGroup = sfxGroup;
                _bankSources[i].priority = 112;
            }

            foreach (var kv in ModernFiles)
            {
                var bank = new Bank();
                for (int i = 1; ; i++)
                {
                    var variant = Resources.Load<AudioClip>(kv.Value + " " + i);
                    if (variant == null) break;
                    bank.Clips.Add(variant);
                }
                if (bank.Clips.Count == 0)
                {
                    var single = Resources.Load<AudioClip>(kv.Value);
                    if (single != null) bank.Clips.Add(single);
                }
                if (bank.Clips.Count > 0) _banks[kv.Key] = bank;
                else Debug.LogWarning("[SFX] Missing clip, add it at Assets/Resources/" + kv.Value + ".<ext>");
            }

            foreach (var kv in Files)
            {
                var clip = Resources.Load<AudioClip>(kv.Value);
                if (clip != null) _clips[kv.Key] = clip;
                else Debug.LogWarning("[SFX] Missing clip, add it at Assets/Resources/" + kv.Value + ".<ext>");
            }

            if (_clips.TryGetValue(Sfx.PieceCapture, out var captureClip)) _captureClips.Add(captureClip);
            for (int i = 2; i <= 3; i++)
            {
                var variant = Resources.Load<AudioClip>("SFX/Capture Slash " + i);
                if (variant != null) _captureClips.Add(variant);
            }
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        // Finds a direct child group of Master by name (null if the mixer or group is missing).
        private static AudioMixerGroup Group(AudioMixer mixer, string name)
        {
            if (mixer == null) return null;
            foreach (var g in mixer.FindMatchingGroups("Master/" + name))
                if (g.name == name) return g;
            return null;
        }

        public static void Play(Sfx id)
        {
            if (id == Sfx.PieceCapture) { PlayCapture(1); return; }
            if (_instance == null || Muted) return;
            if (id == Sfx.RallyTick) { PlayRallyTick(0f); return; }
            if (IsRallySting(id)) { _instance.PlayRallySting(id); return; }
            if (_instance._banks.TryGetValue(id, out var bank))
            {
                if (IsUi(id)) _instance.PlayUiBank(id, bank); else _instance.PlayBank(id, bank);
                return;
            }
            if (_instance._clips.TryGetValue(id, out var clip) && clip != null)
                (IsUi(id) ? _instance._uiSrc : _instance._src).PlayOneShot(clip,
                    SfxVolume * (id == Sfx.PieceMove ? 0.68f : 1f));
        }

        // Picks the next round-robin clip (never the same one twice running), or -1 if the
        // sound fired again inside its minimum gap.
        private static int PickBank(Sfx id, Bank bank)
        {
            float now = Time.unscaledTime;
            if (now - bank.LastTime < BankMinGap(id)) return -1;
            bank.LastTime = now;
            int count = bank.Clips.Count;
            int pick = Random.Range(0, count - (count > 1 && bank.Last >= 0 ? 1 : 0));
            if (count > 1 && bank.Last >= 0 && pick >= bank.Last) pick++;
            bank.Last = pick;
            return pick;
        }

        // UI-group sounds of the 3D style: the round robin alone gives the variety, no pitch
        // jitter, since the UI voice is shared with the menu taps.
        private void PlayUiBank(Sfx id, Bank bank)
        {
            int pick = PickBank(id, bank);
            if (pick >= 0) _uiSrc.PlayOneShot(bank.Clips[pick], SfxVolume);
        }

        private void PlayBank(Sfx id, Bank bank)
        {
            int pick = PickBank(id, bank);
            if (pick < 0) return;
            var source = _bankSources[_bankSourceIndex++ % _bankSources.Length];
            // +/-4% pitch and +/-1.5 dB on the round-robin sounds; musical stings stay as written.
            bool jitter = BankJitterPitch(id);
            source.pitch = jitter ? Random.Range(0.96f, 1.04f) : 1f;
            float db = BankTrimDb(id) + (jitter ? Random.Range(-1.5f, 1.5f) : 0f);
            source.PlayOneShot(bank.Clips[pick], SfxVolume * Db(db));
        }

        public static void PlayShilling()
        {
            if (_instance == null || Muted) return;
            AudioClip clip = null;
            if (_instance._banks.TryGetValue(Sfx.ShillingCollect, out var bank)) clip = bank.Clips[0];
            else _instance._clips.TryGetValue(Sfx.ShillingCollect, out clip);
            if (clip == null) return;
            float now = Time.unscaledTime;
            _instance._shillingChain = now - _instance._lastShillingTime <= ShillingChainWindow
                ? Mathf.Min(_instance._shillingChain + 1, ShillingScale.Length - 1) : 0;
            _instance._lastShillingTime = now;
            var source = _instance._shillingSources[_instance._shillingSourceIndex++ % _instance._shillingSources.Length];
            source.pitch = Mathf.Pow(2f, ShillingScale[_instance._shillingChain] / 12f);
            // Taper about 2 dB across the climb so the high notes don't poke out, plus
            // a little volume jitter. No pitch jitter: the pitch is the melody.
            float taper = Db(-2f * _instance._shillingChain / (ShillingScale.Length - 1));
            source.PlayOneShot(clip, SfxVolume * taper * Db(Random.Range(-1f, 1f)));
        }

        public static void PlayCapture(int comboMultiplier)
        {
            if (_instance == null) return;
            float combo = Mathf.InverseLerp(1f, 6f, Mathf.Max(1, comboMultiplier));
            if (!Muted && _instance._captureClips.Count > 0)
            {
                int count = _instance._captureClips.Count;
                int clipIndex = Random.Range(0, count - (count > 1 && _instance._lastCaptureClip >= 0 ? 1 : 0));
                if (count > 1 && _instance._lastCaptureClip >= 0 && clipIndex >= _instance._lastCaptureClip) clipIndex++;
                _instance._lastCaptureClip = clipIndex;
                var clip = _instance._captureClips[clipIndex];
                int voice = _instance._captureSourceIndex++ % _instance._captureSources.Length;
                var source = _instance._captureSources[voice];
                // Combo ramp plus +/-3% random pitch so repeats don't sound identical.
                // Volume tops out at 1.15x so it won't clip once the ceilings are raised.
                source.pitch = Mathf.Lerp(0.98f, 1.10f, combo) * Random.Range(0.97f, 1.03f);
                source.PlayOneShot(clip, SfxVolume * Mathf.Lerp(1.00f, 1.15f, combo));
            }
        }

        // Plays a gameplay sound on the dedicated loop voice until StopLoop is called.
        // Calling again swaps to the new clip and restarts. Pass loop: false for a sting
        // that should play once but still be cut short by StopLoop (the transformation:
        // looping its 7 s sting made it repeat for the whole held form).
        public static void PlayLoop(Sfx id, bool loop = true)
        {
            if (_instance == null || Muted) return;
            var l = _instance._loopSrc;
            if (l == null) return;
            AudioClip clip = null;
            if (_instance._banks.TryGetValue(id, out var bank)) clip = bank.Clips[0];
            else _instance._clips.TryGetValue(id, out clip);
            if (clip != null)
            {
                _instance._loopFadeOut = false;
                l.clip = clip;
                l.loop = loop;
                l.volume = SfxVolume;
                l.Play();
            }
        }

        // Stops the loop voice with a short fade (fast enough to feel immediate, slow
        // enough not to click).
        public static void StopLoop()
        {
            if (_instance == null || _instance._loopSrc == null) return;
            if (_instance._loopSrc.isPlaying) _instance._loopFadeOut = true;
        }

        private bool _loopFadeOut;
        private const float LoopFadeSeconds = 0.12f;

        private void UpdateLoopFade()
        {
            if (!_loopFadeOut || _loopSrc == null) return;
            _loopSrc.volume = Mathf.MoveTowards(_loopSrc.volume, 0f, SfxVolume / LoopFadeSeconds * Time.unscaledDeltaTime);
            if (_loopSrc.volume <= 0f) { _loopSrc.Stop(); _loopFadeOut = false; }
        }
    }
}
