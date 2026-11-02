using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace KnightsGauntlet.View
{
    // Background music cue. Each is a version of the same theme (ArtSource/Audio/MusicBuild).
    public enum Music { Menu, Trap, Battle, Adventure, Boss }

    // The score: one theme in D, played as a menu version and one loop per mode. Trap,
    // Battle and Adventure each come in three arrangements (meadow, bridge, castle) that
    // are the same length with the same notes at the same times, so the music can
    // crossfade between them mid-bar as the board's biome changes and stay in step.
    //
    // Two loop voices take turns: the new one is started with PlayScheduled at an exact
    // dspTime and a matching timeSamples, then the two crossfade. Going from the menu into
    // play, a one-bar launch phrase plays first and the gameplay loop is scheduled to
    // start exactly where the phrase lands.
    public partial class SfxManager
    {
        private class MusicVoice
        {
            public AudioSource Src;
            public float Gain;                     // 0..1, multiplied by the music volume
            public float From, To = 0f, FadeSeconds;
            public double FadeStartDsp;
        }

        private readonly MusicVoice[] _voices = new MusicVoice[2];
        private int _active;                       // the voice that is (or is about to be) the cue
        private AudioSource _stingSrc;             // launch phrases and the game over sting
        private Music? _wantMusic;                 // the cue the game asked for, even if mute kept it silent
        private Music? _cue;                       // what the loop voices are playing
        private AudioClip[] _cueClips;
        private int _cueBiome = -1, _biome;
        private double _cueStartDsp;
        private float _duckUntil = -1f, _duck = 1f; // music dips under stings and on pause
        private bool _pauseDuck;
        private readonly Dictionary<Music, AudioClip[]> _score = new Dictionary<Music, AudioClip[]>();
        private readonly Dictionary<Music, AudioClip> _launches = new Dictionary<Music, AudioClip>();
        private AudioClip _gameOverTheme;

        private const string ScorePath = "SFX/Music/";
        private static readonly string[] BiomeNames = { "Meadow", "Bridge", "Castle" }; // BoardBiome order

        // The score files sit at -16 LUFS; the old menu theme was -12.4. This keeps the music
        // as loud in game as it was, at the same slider position.
        private const float ScoreTrimDb = 3.6f;
        private const float MusicDuckDb = -6f;
        private const double ScheduleLead = 0.1;   // seconds ahead that scheduled starts are placed

        // The launch phrase is one bar of the gameplay cue's tempo; the loop starts on its last
        // downbeat. Tempos (4/4): Trap, Battle and Boss 132, Adventure 66 (the menu is 88).
        private static double BarSeconds(Music cue)
            => cue == Music.Adventure ? 60.0 / 66 * 4 : 60.0 / 132 * 4;

        private void InitMusic(AudioMixerGroup musicGroup)
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.outputAudioMixerGroup = musicGroup;
                s.priority = 0;
                s.volume = 0f;
                _voices[i] = new MusicVoice { Src = s };
            }
            _stingSrc = gameObject.AddComponent<AudioSource>();
            _stingSrc.playOnAwake = false;
            _stingSrc.spatialBlend = 0f;
            _stingSrc.outputAudioMixerGroup = musicGroup;
            _stingSrc.priority = 0;

            LoadScore(Music.Menu, "Score Menu");
            LoadScore(Music.Boss, "Score Boss");
            foreach (var cue in new[] { Music.Trap, Music.Battle, Music.Adventure })
                LoadScore(cue, null);
            foreach (var cue in new[] { Music.Trap, Music.Battle, Music.Adventure, Music.Boss })
            {
                var l = Resources.Load<AudioClip>(ScorePath + "Launch " + cue);
                if (l != null) _launches[cue] = l;
            }
            _gameOverTheme = Resources.Load<AudioClip>(ScorePath + "Game Over Theme");
        }

        // A single file ("Score Menu"), or one per biome ("Score Trap Meadow", ...).
        private void LoadScore(Music cue, string single)
        {
            AudioClip[] clips;
            if (single != null) clips = new[] { Resources.Load<AudioClip>(ScorePath + single) };
            else
            {
                clips = new AudioClip[BiomeNames.Length];
                for (int i = 0; i < clips.Length; i++)
                    clips[i] = Resources.Load<AudioClip>(ScorePath + "Score " + cue + " " + BiomeNames[i]);
            }
            foreach (var c in clips)
                if (c == null) { Debug.LogWarning("[SFX] Missing score cue " + cue + " in Assets/Resources/" + ScorePath); return; }
            _score[cue] = clips;
        }

        private void Update()
        {
            UpdateLoopFade();
            UpdateMusic();
        }

        // Loops the menu theme. Safe to call repeatedly: if it's already playing it
        // does nothing (so bouncing between menu sub-screens never restarts it).
        public static void PlayMenuMusic() => PlayMusic(Music.Menu);

        // Starts a cue, crossfading from whatever was playing. Calling it again with the
        // cue that's already playing does nothing (a retry keeps the track going). From
        // the menu or from silence, gameplay cues open with their launch phrase.
        // biome: the board's biome at the start (BoardBiome as an int), so the first
        // bars already have the right arrangement.
        public static void PlayMusic(Music cue, int biome = -1)
        {
            if (_instance == null) return;
            var me = _instance;
            me._wantMusic = cue;
            me._pauseDuck = false;
            if (biome >= 0) me._biome = biome;
            if (Muted) return;
            me.StartCue(cue);
        }

        private void StartCue(Music cue)
        {
            if (!_score.TryGetValue(cue, out var clips)) { FadeOutMusic(0.6f); _cue = null; return; }
            int b = clips.Length == 1 ? 0 : Mathf.Clamp(_biome, 0, clips.Length - 1);
            if (_cue == cue && Active.Src.isPlaying && System.Array.IndexOf(clips, Active.Src.clip) >= 0) return;
            foreach (var c in clips) c.LoadAudioData(); // all arrangements ready for biome switches
            bool fromAnotherCue = _cue.HasValue && Active.Src.isPlaying && Active.Gain > 0.01f;
            AudioClip launch = null;
            if (cue != Music.Menu && (!fromAnotherCue || _cue == Music.Menu)) _launches.TryGetValue(cue, out launch);
            // Menu after game over or at boot: a slow fade in. Gameplay cue to gameplay
            // cue (no launch): a one-second crossfade.
            float fadeIn = launch != null ? 0f : cue == Music.Menu ? 2.5f : 1.0f;
            Begin(clips, b, cue, launch, launch != null ? 0.6f : 1.0f, fadeIn);
        }

        private MusicVoice Active => _voices[_active];

        private void Begin(AudioClip[] clips, int b, Music cue, AudioClip launch, float fadeOut, float fadeIn)
        {
            double now = AudioSettings.dspTime;
            double start = now + ScheduleLead;
            FadeOutMusic(Mathf.Max(0.05f, fadeOut));
            if (launch != null)
            {
                _stingSrc.Stop();
                _stingSrc.clip = launch;
                _stingSrc.volume = MusicVolume * Db(ScoreTrimDb);
                _stingSrc.PlayScheduled(start);
                start += BarSeconds(cue);
            }
            _active = 1 - _active;
            var v = Active;
            v.Src.Stop();
            v.Src.clip = clips[b];
            v.Src.loop = true;
            v.Src.timeSamples = 0;
            v.Src.PlayScheduled(start);
            SetFade(v, fadeIn <= 0f ? 1f : 0f, 1f, fadeIn, start);
            _cue = cue;
            _cueClips = clips;
            _cueBiome = b;
            _cueStartDsp = start;
        }

        // Fades every loop voice out and stops it when silent.
        private void FadeOutMusic(float seconds)
        {
            double now = AudioSettings.dspTime;
            foreach (var v in _voices)
                if (v.Src.isPlaying || v.Gain > 0f) SetFade(v, v.Gain, 0f, seconds, now);
        }

        private static void SetFade(MusicVoice v, float from, float to, float seconds, double startDsp)
        {
            v.From = from; v.To = to; v.FadeSeconds = Mathf.Max(0.001f, seconds); v.FadeStartDsp = startDsp;
            if (seconds <= 0f) v.Gain = to;
        }

        // Called every frame by GameManager with the board's biome; crossfades to the
        // matching arrangement in step with the one playing.
        public static void SetMusicBiome(int biome)
        {
            if (_instance == null) return;
            var me = _instance;
            me._biome = biome;
            if (!me._cue.HasValue || me._cueClips == null || me._cueClips.Length < 2 || Muted) return;
            int b = Mathf.Clamp(biome, 0, me._cueClips.Length - 1);
            if (b == me._cueBiome) return;
            me.SwitchArrangement(b);
        }

        private const float BiomeCrossfadeSeconds = 2.5f;

        private void SwitchArrangement(int b)
        {
            var clip = _cueClips[b];
            double now = AudioSettings.dspTime;
            var old = Active;
            if (now + ScheduleLead < _cueStartDsp)
            {
                // Still in the launch phrase: just swap what will start on the downbeat.
                old.Src.Stop();
                old.Src.clip = clip;
                old.Src.timeSamples = 0;
                old.Src.PlayScheduled(_cueStartDsp);
                _cueBiome = b;
                return;
            }
            double at = now + ScheduleLead;
            // Where the playing arrangement will be at `at`, in samples: every arrangement
            // has the same length, so the new one starts at the same point in the music.
            long pos = (long)System.Math.Round((at - _cueStartDsp) * clip.frequency) % clip.samples;
            _active = 1 - _active;
            var v = Active;
            v.Src.Stop();
            v.Src.clip = clip;
            v.Src.loop = true;
            v.Src.timeSamples = (int)pos;
            v.Src.PlayScheduled(at);
            SetFade(v, 0f, 1f, BiomeCrossfadeSeconds, at);
            SetFade(old, old.Gain, 0f, BiomeCrossfadeSeconds, at);
            _cueBiome = b;
        }

        public static void StopMusic(float fadeSeconds = 0.8f)
        {
            if (_instance == null) return;
            _instance._wantMusic = null;
            _instance._pauseDuck = false;
            _instance._cue = null;
            _instance.FadeOutMusic(fadeSeconds);
        }

        // The theme's falling phrase, for a death or a failed level. Plays on the music
        // channel (music slider), over whatever the loops are doing.
        public static void PlayGameOverSting()
        {
            if (_instance == null || Muted || _instance._gameOverTheme == null) return;
            var s = _instance._stingSrc;
            s.Stop();
            s.clip = _instance._gameOverTheme;
            s.volume = MusicVolume * Db(ScoreTrimDb);
            s.Play();
        }

        // Dips the music for a moment so a sting reads clearly over it.
        public static void DuckMusic(float seconds)
        {
            if (_instance == null) return;
            _instance._duckUntil = Mathf.Max(_instance._duckUntil, Time.unscaledTime + seconds);
        }

        // Holds the music dipped while the pause menu is up.
        public static void SetMusicPaused(bool paused)
        {
            if (_instance != null) _instance._pauseDuck = paused;
        }

        private void OnMutedChanged(bool muted)
        {
            if (muted)
            {
                foreach (var v in _voices) v.Src.Pause();
                _stingSrc.Pause();
                return;
            }
            bool any = false;
            foreach (var v in _voices) { v.Src.UnPause(); any |= v.Src.isPlaying; }
            _stingSrc.UnPause();
            // Started muted, so the cue never began: start it now.
            if (!any && _wantMusic.HasValue) { _cue = null; StartCue(_wantMusic.Value); }
        }

        private void UpdateMusic()
        {
            float duckTarget = _pauseDuck || Time.unscaledTime < _duckUntil ? Db(MusicDuckDb) : 1f;
            _duck = Mathf.MoveTowards(_duck, duckTarget, 2f * Time.unscaledDeltaTime);
            double now = AudioSettings.dspTime;
            float trim = Db(ScoreTrimDb);
            float baseVol = MusicVolume * trim * _duck;
            foreach (var v in _voices)
            {
                float u = Mathf.Clamp01((float)((now - v.FadeStartDsp) / v.FadeSeconds));
                if (now >= v.FadeStartDsp)
                {
                    // Equal-power curves: the arrangements are different instruments, so their
                    // sum stays level through a crossfade.
                    v.Gain = v.To > v.From
                        ? v.From + (v.To - v.From) * Mathf.Sin(u * Mathf.PI / 2f)
                        : v.To + (v.From - v.To) * Mathf.Cos(u * Mathf.PI / 2f);
                }
                v.Src.volume = baseVol * v.Gain;
                if (u >= 1f && v.To <= 0f && v.Src.isPlaying) v.Src.Stop();
            }
            if (_stingSrc.isPlaying) _stingSrc.volume = MusicVolume * Db(ScoreTrimDb);
        }
    }
}
