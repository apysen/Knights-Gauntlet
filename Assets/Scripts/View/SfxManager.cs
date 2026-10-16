using System.Collections.Generic;
using UnityEngine;

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
        Transform,          // battle/adventure: took on a captured piece's form (legacy pop)
        Untransform,        // form ran out when the timer expired
        Transformation,     // battle/adventure: a NEW transform begins (fires once per nightrider)
        AdventureNextLevel // adventure: reached the level goal
    }

    // One shared AudioSource that plays short UI/gameplay sounds. Clips live in
    // Assets/Resources/SFX/ and are loaded by filename at startup, so any layer can
    // fire a sound with a single static call: SfxManager.Play(Sfx.PieceCapture).
    //
    // GameManager creates one of these at boot. If a clip file is missing the entry
    // is simply skipped (with a warning), so a missing sound never breaks the game.
    public class SfxManager : MonoBehaviour
    {
        private static SfxManager _instance;
        private AudioSource _src;
        private AudioSource _music;
        private AudioSource _loopSrc; // a single looping gameplay sound (e.g. the transform buff)
        private AudioSource[] _captureSources;
        private int _captureSourceIndex;
        private readonly List<AudioClip> _captureClips = new List<AudioClip>();
        private int _lastCaptureClip = -1;
        private AudioClip _legacyCaptureClip;
        private const string LegacyCapturePath = "SFX/JDSherbert - Pixel UI SFX Pack - Cancel 5 (Square)";
        private float _lastCaptureHapticTime = -1f;
        private AudioClip _menuTheme;
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();

        // Looping background music. Add MenuTheme.wav under Assets/Resources/SFX/.
        private const string MenuThemePath = "SFX/MenuTheme";

        // ── Default volumes ──────────────────────────────────────────────────
        // EDIT THESE to change the out-of-the-box volume levels (0 = silent, 1 = full).
        // They only apply until the player moves a slider, which saves their own value.
        // Music is capped: it never plays louder than MaxMusicVolume (the full-right end
        // of the music slider), so the theme stays quietly in the background.
        public const float MaxMusicVolume     = 0.1f;   // hard ceiling: music tops out at 10%
        public const float DefaultMusicVolume = 0.1f;   // must be within [0, MaxMusicVolume]
        public const float MaxSfxVolume       = 0.1f;   // hard ceiling: sfx top out at 10%
        public const float DefaultSfxVolume   = 0.1f;   // must be within [0, MaxSfxVolume]

        // Current volumes, persisted so the player's choice survives a restart. Setting
        // MusicVolume also updates the live music source so a slider drag is heard at once.
        // Music is clamped to [0, MaxMusicVolume]; this is the real AudioSource volume,
        // not the 0..1 slider position (see the Settings slider for the mapping).
        public static float MusicVolume
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat("kg_music_vol", DefaultMusicVolume), 0f, MaxMusicVolume);
            set
            {
                float v = Mathf.Clamp(value, 0f, MaxMusicVolume);
                PlayerPrefs.SetFloat("kg_music_vol", v); PlayerPrefs.Save();
                if (_instance != null && _instance._music != null) _instance._music.volume = v;
            }
        }
        // SFX is clamped to [0, MaxSfxVolume] the same way music is, so both top out at 10%.
        public static float SfxVolume
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat("kg_sfx_vol", DefaultSfxVolume), 0f, MaxSfxVolume);
            set { PlayerPrefs.SetFloat("kg_sfx_vol", Mathf.Clamp(value, 0f, MaxSfxVolume)); PlayerPrefs.Save(); }
        }

        // Resources path for each sound (no file extension). Drop the matching audio
        // files into Assets/Resources/SFX/ with exactly these names. Two events reuse
        // the same base pack family but different variants, so each has its own file.
        private static readonly Dictionary<Sfx, string> Files = new Dictionary<Sfx, string>
        {
            { Sfx.ShillingCollect, "SFX/JDSherbert - Pixel UI SFX Pack - Popup Open 4 (Square)" },
            { Sfx.TrapLevelUp,     "SFX/JDSherbert - Pixel UI SFX Pack - Popup Open 1 (Square)" },
            { Sfx.ShieldBreak,     "SFX/JDSherbert - Pixel UI SFX Pack - Popup Open 5 (Square)" },
            { Sfx.PlaySelect,      "SFX/JDSherbert - Pixel UI SFX Pack - Select 5 (Square)" },
            { Sfx.PieceMove,       "SFX/JDSherbert - Pixel UI SFX Pack - Cancel 3 (Square)" },
            { Sfx.PieceCapture,    "SFX/Capture Slash" },
            { Sfx.TabSelect,       "SFX/JDSherbert - Pixel UI SFX Pack - Cursor 1 (Square)" },
            { Sfx.PageTurn,        "SFX/JDSherbert - Pixel UI SFX Pack - Cursor 12 (Square)" },
            { Sfx.Transform,       "SFX/JDSherbert - Pixel UI SFX Pack - Popup Close 4 (Square)" },
            { Sfx.Untransform,     "SFX/JDSherbert - Pixel UI SFX Pack - Popup Close 5 (Square)" },
            { Sfx.Transformation,     "SFX/Transformation" },
            { Sfx.AdventureNextLevel, "SFX/Adventure Mode Next Level" },
        };

        // Global mute, backed by a pref so a future Settings toggle can flip it.
        public static bool Muted
        {
            get => PlayerPrefs.GetInt("kg_muted", 0) == 1;
            set { PlayerPrefs.SetInt("kg_muted", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f; // 2D

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
            _music.volume = MusicVolume;

            _loopSrc = gameObject.AddComponent<AudioSource>();
            _loopSrc.playOnAwake = false;
            _loopSrc.loop = true;
            _loopSrc.spatialBlend = 0f;

            // Separate voices let fast capture chains rise in pitch without
            // retuning a sound that is still ringing from the previous hit.
            _captureSources = new AudioSource[4];
            for (int i = 0; i < _captureSources.Length; i++)
            {
                _captureSources[i] = gameObject.AddComponent<AudioSource>();
                _captureSources[i].playOnAwake = false;
                _captureSources[i].spatialBlend = 0f;
            }

            foreach (var kv in Files)
            {
                var clip = Resources.Load<AudioClip>(kv.Value);
                if (clip != null) _clips[kv.Key] = clip;
                else Debug.LogWarning("[SFX] Missing clip, add it at Assets/Resources/" + kv.Value + ".<ext>");
            }

            _legacyCaptureClip = Resources.Load<AudioClip>(LegacyCapturePath);
            if (_clips.TryGetValue(Sfx.PieceCapture, out var captureClip)) _captureClips.Add(captureClip);
            for (int i = 2; i <= 3; i++)
            {
                var variant = Resources.Load<AudioClip>("SFX/Capture Slash " + i);
                if (variant != null) _captureClips.Add(variant);
            }

            _menuTheme = Resources.Load<AudioClip>(MenuThemePath);
            if (_menuTheme == null)
                Debug.LogWarning("[SFX] Missing menu theme at Assets/Resources/" + MenuThemePath + ".<ext>");
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        public static void Play(Sfx id)
        {
            if (id == Sfx.PieceCapture) { PlayCapture(1); return; }
            if (_instance == null || Muted) return;
            if (_instance._clips.TryGetValue(id, out var clip) && clip != null)
                _instance._src.PlayOneShot(clip,
                    SfxVolume * (id == Sfx.PieceMove && !UIFactory.LegacyStyle ? 0.68f : 1f));
        }

        public static void PlayCapture(int comboMultiplier)
        {
            if (_instance == null) return;
            float combo = Mathf.InverseLerp(1f, 6f, Mathf.Max(1, comboMultiplier));
            if (!Muted && UIFactory.LegacyStyle)
            {
                // Legacy keeps the original pixel capture sound at its original pitch and level.
                if (_instance._legacyCaptureClip != null)
                    _instance._src.PlayOneShot(_instance._legacyCaptureClip, SfxVolume);
            }
            else if (!Muted && _instance._captureClips.Count > 0)
            {
                int count = _instance._captureClips.Count;
                int clipIndex = Random.Range(0, count - (count > 1 && _instance._lastCaptureClip >= 0 ? 1 : 0));
                if (count > 1 && _instance._lastCaptureClip >= 0 && clipIndex >= _instance._lastCaptureClip) clipIndex++;
                _instance._lastCaptureClip = clipIndex;
                var clip = _instance._captureClips[clipIndex];
                int voice = _instance._captureSourceIndex++ % _instance._captureSources.Length;
                var source = _instance._captureSources[voice];
                source.pitch = Mathf.Lerp(0.98f, 1.10f, combo);
                source.PlayOneShot(clip, SfxVolume * Mathf.Lerp(1.00f, 1.35f, combo));
            }
            _instance.PulseCaptureHaptic();
        }

        private void PulseCaptureHaptic()
        {
            if (!Application.isMobilePlatform || !SaveData.IsCaptureHaptics()) return;
            if (Time.unscaledTime - _lastCaptureHapticTime < 0.10f) return;
            _lastCaptureHapticTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (vibrator == null) return;
                    int sdk = version.GetStatic<int>("SDK_INT");
                    if (sdk >= 26)
                    {
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", 28L, 110))
                            vibrator.Call("vibrate", effect);
                    }
                    else vibrator.Call("vibrate", 28L);
                }
            }
            catch { Handheld.Vibrate(); }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        // Loops a gameplay sound until StopLoop is called (used for the transform buff,
        // which lasts a few seconds). Calling again swaps to the new clip and restarts.
        public static void PlayLoop(Sfx id)
        {
            if (_instance == null || Muted) return;
            var l = _instance._loopSrc;
            if (l == null) return;
            if (_instance._clips.TryGetValue(id, out var clip) && clip != null)
            {
                l.clip = clip;
                l.volume = SfxVolume;
                l.Play();
            }
        }

        public static void StopLoop()
        {
            if (_instance == null || _instance._loopSrc == null) return;
            _instance._loopSrc.Stop();
        }

        // Loops the menu theme. Safe to call repeatedly: if it's already playing it
        // does nothing (so bouncing between menu sub-screens never restarts it).
        public static void PlayMenuMusic()
        {
            if (_instance == null || Muted) return;
            var m = _instance._music;
            if (m == null || _instance._menuTheme == null) return;
            if (m.isPlaying && m.clip == _instance._menuTheme) return;
            m.volume = MusicVolume;
            m.clip = _instance._menuTheme;
            m.Play();
        }

        public static void StopMenuMusic()
        {
            if (_instance == null || _instance._music == null) return;
            _instance._music.Stop();
        }

    }
}
