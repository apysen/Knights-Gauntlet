using UnityEngine;

namespace KnightsGauntlet.View
{
    // Short vibration patterns for powerup moments. Respects the player's vibration
    // setting (SaveData.IsCaptureHaptics) and does nothing off mobile.
    //
    // Android plays a real waveform (API 26+): timings alternate off/on in ms, with an
    // amplitude 0..255 per segment. Older Android plays the total "on" time as one buzz.
    // iOS has no amplitude control through Unity, so it gets one standard buzz, and only
    // for patterns marked strong (a buzz per tick would feel like a malfunction).
    public static class Haptics
    {
        // The War Horn blast: a hard hit that rings out.
        public static readonly long[] HornBlast = { 0, 70, 30, 90 };
        public static readonly int[] HornBlastAmp = { 0, 255, 0, 110 };
        // One count step: the faintest tap.
        public static readonly long[] Tick = { 0, 9 };
        public static readonly int[] TickAmp = { 0, 70 };
        // The slam on the peak: a double punch.
        public static readonly long[] Slam = { 0, 55, 45, 90 };
        public static readonly int[] SlamAmp = { 0, 255, 0, 200 };
        // Chronokinesis: a tick-tock, the second beat softer and later (time stretching).
        public static readonly long[] TickTock = { 0, 28, 150, 28, 260, 22 };
        public static readonly int[] TickTockAmp = { 0, 150, 0, 90, 0, 50 };
        // Divine Intervention: a gentle swell that rises and fades.
        public static readonly long[] Swell = { 0, 60, 0, 70, 0, 90, 0, 120 };
        public static readonly int[] SwellAmp = { 0, 40, 0, 90, 0, 160, 0, 60 };
        // Pathfinder: two quick taps, like a compass needle settling.
        public static readonly long[] Settle = { 0, 22, 70, 34 };
        public static readonly int[] SettleAmp = { 0, 100, 0, 170 };

        // Berserker Draught: a rumble that builds into a roar.
        public static readonly long[] Roar = { 0, 50, 0, 60, 0, 80, 0, 140 };
        public static readonly int[] RoarAmp = { 0, 60, 0, 120, 0, 190, 0, 255 };

        // Shop: a crisp double tap for a purchase, a dull buzz for "can't afford",
        // and a long cascade for buying shillings.
        public static readonly long[] Purchase = { 0, 18, 60, 30 };
        public static readonly int[] PurchaseAmp = { 0, 120, 0, 200 };
        public static readonly long[] Deny = { 0, 40, 50, 40 };
        public static readonly int[] DenyAmp = { 0, 70, 0, 70 };
        public static readonly long[] Jackpot = { 0, 30, 40, 30, 40, 30, 40, 30, 60, 120 };
        public static readonly int[] JackpotAmp = { 0, 120, 0, 150, 0, 180, 0, 210, 0, 255 };

        // Picking a mode or difficulty: one crisp tap.
        public static readonly long[] Select = { 0, 14 };
        public static readonly int[] SelectAmp = { 0, 140 };

        private static float _lastTime = -1f;

        public static void Play(long[] timings, int[] amplitudes, bool strong = false)
        {
            if (!Application.isMobilePlatform || !SaveData.IsCaptureHaptics()) return;
            if (timings == null || amplitudes == null || timings.Length != amplitudes.Length) return;
            _lastTime = Time.unscaledTime;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (vibrator == null) return;
                    if (version.GetStatic<int>("SDK_INT") >= 26)
                    {
                        // A new vibrate() replaces whatever pattern is still running.
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1))
                            vibrator.Call("vibrate", effect);
                    }
                    else
                    {
                        long on = 0;
                        for (int i = 1; i < timings.Length; i += 2) on += timings[i];
                        vibrator.Call("vibrate", on);
                    }
                }
            }
            catch { if (strong) Handheld.Vibrate(); }
#elif UNITY_IOS && !UNITY_EDITOR
            if (strong) Handheld.Vibrate();
#endif
        }

        // Seconds since the last pattern, so rapid callers (the count ticks) can skip
        // a tick rather than cut off a stronger pattern that is still playing.
        public static float SinceLast => _lastTime < 0f ? 999f : Time.unscaledTime - _lastTime;
    }
}
