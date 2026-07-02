using System;

namespace KnightsGauntlet.View
{
    // Network-agnostic ad seam. The game only ever talks to this interface, so a
    // real network (AdMob, AppLovin MAX, Unity LevelPlay, …) can be dropped in
    // later as a single new implementation without touching game logic.
    //
    // Two placements are wired up:
    //   • Banner   — shown on the game-over screen.
    //   • Rewarded — the optional "extra life" on death.
    public interface IAdService
    {
        void Initialize();

        // Rewarded (extra life). onReward fires only if the user finishes the ad;
        // onUnavailable fires if no ad is ready or the network isn't wired in yet.
        bool IsRewardedReady { get; }
        void LoadRewarded();
        void ShowRewarded(Action onReward, Action onUnavailable);

        // Banner (game-over screen).
        void ShowBanner();
        void HideBanner();
    }

    // Default until a real network is integrated. Does nothing, and reports
    // rewarded ads as unavailable so the "watch ad" path falls back gracefully.
    public class NoOpAdService : IAdService
    {
        public void Initialize() { }
        public bool IsRewardedReady => false;
        public void LoadRewarded() { }
        public void ShowRewarded(Action onReward, Action onUnavailable) { onUnavailable?.Invoke(); }
        public void ShowBanner() { }
        public void HideBanner() { }
    }
}
