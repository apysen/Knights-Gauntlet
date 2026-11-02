using UnityEngine;
using UnityEngine.Audio;

namespace KnightsGauntlet.View
{
    // War Horn rally sounds (Battle powerup). The horn and the slam get their own voice so
    // the round-robin bank voices (which jitter pitch for moves and coin clinks) can never
    // retune them while they ring. The count-up tick is one clip, pitched up a D major
    // pentatonic as the count climbs, landing on the octave D just before the slam's D major
    // stab. Clips are built by ArtSource/Audio/MusicBuild/build_warhorn.py (CC0 VSCO 2 CE).
    public partial class SfxManager
    {
        private AudioSource _rallyStingSrc;
        private AudioSource[] _rallyTickSources;
        private int _rallyTickIndex;

        // Semitones above the tick clip's own D, climbing one octave across the count.
        private static readonly int[] RallyTickScale = { 0, 2, 4, 7, 9, 12 };

        private void InitRally(AudioMixerGroup sfxGroup)
        {
            _rallyStingSrc = gameObject.AddComponent<AudioSource>();
            _rallyStingSrc.playOnAwake = false;
            _rallyStingSrc.spatialBlend = 0f;
            _rallyStingSrc.outputAudioMixerGroup = sfxGroup;
            _rallyStingSrc.priority = 48; // a headline moment: keep it over coins and moves

            // Ticks can come every 45 ms and each rings about 0.16 s, so a few voices let
            // a new, higher tick start without retuning the one still ringing.
            _rallyTickSources = new AudioSource[4];
            for (int i = 0; i < _rallyTickSources.Length; i++)
            {
                _rallyTickSources[i] = gameObject.AddComponent<AudioSource>();
                _rallyTickSources[i].playOnAwake = false;
                _rallyTickSources[i].spatialBlend = 0f;
                _rallyTickSources[i].outputAudioMixerGroup = sfxGroup;
                _rallyTickSources[i].priority = 100;
            }
        }

        // The Berserker Draught's sting rings about 1.3 s, so it shares this voice too.
        private static bool IsRallySting(Sfx id) => id == Sfx.PowerupWarHorn || id == Sfx.RallySlam || id == Sfx.PowerupBerserker;

        private void PlayRallySting(Sfx id)
        {
            if (_rallyStingSrc == null || !_banks.TryGetValue(id, out var bank)) return;
            _rallyStingSrc.pitch = 1f;
            _rallyStingSrc.PlayOneShot(bank.Clips[0], SfxVolume);
        }

        // One count-up tick. progress is the count's progress 0..1 toward the peak.
        public static void PlayRallyTick(float progress)
        {
            if (_instance == null || Muted) return;
            if (_instance._rallyTickSources == null || !_instance._banks.TryGetValue(Sfx.RallyTick, out var bank)) return;
            float p = Mathf.Clamp01(progress);
            int step = Mathf.Min(RallyTickScale.Length - 1, Mathf.FloorToInt(p * RallyTickScale.Length));
            var source = _instance._rallyTickSources[_instance._rallyTickIndex++ % _instance._rallyTickSources.Length];
            source.pitch = Mathf.Pow(2f, RallyTickScale[step] / 12f);
            // Taper about 2 dB up the climb so the high steps don't poke out (as the shilling
            // chain does), plus a touch of level jitter so repeats on one step differ.
            source.PlayOneShot(bank.Clips[0], SfxVolume * Db(-2f * p + Random.Range(-0.8f, 0.8f)));
        }
    }
}
