using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    // The sound effect a game event maps to. Kept separate from the clip files so
    // call sites read as intent ("play the capture sound") not filenames.
    public enum Sfx
    {
        ShillingCollect, // trap mode: picked up a shilling
        ShieldBreak,     // battle: a transform absorbed a hit
        PlaySelect,      // pressed Play, or picked an adventure level
        PieceMove,       // battle/adventure: moved without capturing
        PieceCapture,    // battle/adventure: captured a piece
        TabSelect,       // difficulty / leaderboard tab / customization tab
        PageTurn,        // How to Play next / previous page
        Transform,          // battle/adventure: took on a captured piece's form (legacy pop)
        Untransform,        // form ran out when the timer expired
        Transformation,     // battle/adventure: a NEW transform begins (fires once per nightrider)
        AdventureNextLevel, // adventure: reached the level goal
        LootboxOpen,        // pressed Open on a lootbox
        LootboxScroll       // the reveal screen scrolls in (pauses music, fades it back after)
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
            { Sfx.ShieldBreak,     "SFX/JDSherbert - Pixel UI SFX Pack - Popup Open 5 (Square)" },
            { Sfx.PlaySelect,      "SFX/JDSherbert - Pixel UI SFX Pack - Select 5 (Square)" },
            { Sfx.PieceMove,       "SFX/JDSherbert - Pixel UI SFX Pack - Cancel 3 (Square)" },
            { Sfx.PieceCapture,    "SFX/JDSherbert - Pixel UI SFX Pack - Cancel 5 (Square)" },
            { Sfx.TabSelect,       "SFX/JDSherbert - Pixel UI SFX Pack - Cursor 1 (Square)" },
            { Sfx.PageTurn,        "SFX/JDSherbert - Pixel UI SFX Pack - Cursor 12 (Square)" },
            { Sfx.Transform,       "SFX/JDSherbert - Pixel UI SFX Pack - Popup Close 4 (Square)" },
            { Sfx.Untransform,     "SFX/JDSherbert - Pixel UI SFX Pack - Popup Close 5 (Square)" },
            { Sfx.Transformation,     "SFX/Transformation" },
            { Sfx.AdventureNextLevel, "SFX/Adventure Mode Next Level" },
            { Sfx.LootboxOpen,        "SFX/Lootbox Open" },
            { Sfx.LootboxScroll,      "SFX/Lootbox Scroll" },
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

            foreach (var kv in Files)
            {
                var clip = Resources.Load<AudioClip>(kv.Value);
                if (clip != null) _clips[kv.Key] = clip;
                else Debug.LogWarning("[SFX] Missing clip, add it at Assets/Resources/" + kv.Value + ".<ext>");
            }

            _menuTheme = Resources.Load<AudioClip>(MenuThemePath);
            if (_menuTheme == null)
                Debug.LogWarning("[SFX] Missing menu theme at Assets/Resources/" + MenuThemePath + ".<ext>");
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        public static void Play(Sfx id)
        {
            if (_instance == null || Muted) return;
            if (_instance._clips.TryGetValue(id, out var clip) && clip != null)
                _instance._src.PlayOneShot(clip, SfxVolume);
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
            m.volume = MusicVolume; // heal any volume a lootbox-scroll fade left partway
            m.clip = _instance._menuTheme;
            m.Play();
        }

        public static void StopMenuMusic()
        {
            if (_instance == null || _instance._music == null) return;
            _instance._music.Stop();
        }

        // Plays the lootbox scroll sting over a screen transition: the menu music pauses
        // while it plays, then fades back in once the scroll clip finishes so the sting
        // is heard cleanly. If the music wasn't playing, it simply plays the sting.
        private Coroutine _scrollCo;
        public static void PlayLootboxScroll()
        {
            if (_instance == null || Muted) return;
            if (_instance._scrollCo != null) _instance.StopCoroutine(_instance._scrollCo);
            _instance._scrollCo = _instance.StartCoroutine(_instance.ScrollThenFadeMusic());
        }

        private System.Collections.IEnumerator ScrollThenFadeMusic()
        {
            bool musicWasPlaying = _music != null && _music.isPlaying;
            if (musicWasPlaying) _music.Pause();

            float clipLen = 0f;
            if (_clips.TryGetValue(Sfx.LootboxScroll, out var clip) && clip != null)
            {
                clipLen = clip.length;
                _src.PlayOneShot(clip, SfxVolume);
            }
            yield return new WaitForSecondsRealtime(clipLen);

            if (musicWasPlaying && _music != null && !Muted)
            {
                float target = MusicVolume;
                _music.volume = 0f;
                _music.UnPause();
                const float fade = 0.8f;
                float t = 0f;
                while (t < fade)
                {
                    t += Time.unscaledDeltaTime;
                    _music.volume = Mathf.Lerp(0f, target, t / fade);
                    yield return null;
                }
                _music.volume = target;
            }
            _scrollCo = null;
        }
    }
}
