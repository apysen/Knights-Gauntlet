using System;
using UnityEngine;
using Unity.Services.LevelPlay;   // LevelPlay 9.x lives in this namespace

namespace KnightsGauntlet.View
{
    // LevelPlay (Unity mediation, formerly ironSource) implementation of IAdService, for
    // the LevelPlay 9.x API in the com.unity.services.levelplay package. The App Key
    // initializes the SDK; the banner and rewarded formats each use an Ad Unit ID. The
    // banner is only ever shown on the screens UIManager allows (menu, level select,
    // adventure result/fail, game over), via ShowBanner/HideBanner and the _wantBanner
    // gate below. Signatures verified against the installed 9.5.0 package.
    public class LevelPlayAdService : IAdService
    {
        private readonly string _appKey, _bannerId, _rewardedId;
        private LevelPlayBannerAd _banner;
        private LevelPlayRewardedAd _rewarded;
        private bool _wantBanner, _bannerLoaded;
        private Action _onReward, _onUnavailable;

        public LevelPlayAdService(string androidAppKey, string iosAppKey,
                                  string androidBannerId, string iosBannerId,
                                  string androidRewardedId, string iosRewardedId)
        {
#if UNITY_IOS
            _appKey = iosAppKey; _bannerId = iosBannerId; _rewardedId = iosRewardedId;
#else
            _appKey = androidAppKey; _bannerId = androidBannerId; _rewardedId = androidRewardedId;
#endif
        }

        public void Initialize()
        {
            if (string.IsNullOrEmpty(_appKey)) return;
            ApplyPrivacy();          // COPPA/GDPR flags, set before Init
            RequestTrackingIfNeeded(); // iOS App Tracking Transparency (no-op elsewhere)
            LevelPlay.OnInitSuccess += _ => OnReady();
            LevelPlay.OnInitFailed  += err => Debug.LogWarning("[Ads] LevelPlay init failed: " + err);
            LevelPlay.Init(_appKey);
        }

        // Privacy flags must be set before Init. This is NOT a child-directed app, so COPPA
        // is false. GDPR consent should reflect a real user choice: wire SetGdprConsent to
        // your consent dialog (or the LevelPlay dashboard's consent management) for EU users.
        // We default to granted here so beta test devices serve ads; replace before launch.
        private void ApplyPrivacy()
        {
            LevelPlayPrivacySettings.SetCOPPA(false);
            LevelPlayPrivacySettings.SetGDPRConsent(true); // TODO: drive from a real consent flow before release
        }

        // Call this from a consent dialog once the user answers.
        public static void SetGdprConsent(bool granted) => LevelPlayPrivacySettings.SetGDPRConsent(granted);

        // iOS 14.5+ requires an App Tracking Transparency prompt before the IDFA is usable.
        // Guarded by LEVELPLAY_ATT so the build never breaks: to enable, install the
        // "com.unity.ads.ios-support" package, add LEVELPLAY_ATT to Player Settings scripting
        // defines, and set an NSUserTrackingUsageDescription in iOS Player Settings.
        private void RequestTrackingIfNeeded()
        {
#if UNITY_IOS && LEVELPLAY_ATT
            if (Unity.Advertisement.IosSupport.ATTrackingStatusBinding.GetAuthorizationTrackingStatus()
                == Unity.Advertisement.IosSupport.ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
                Unity.Advertisement.IosSupport.ATTrackingStatusBinding.RequestAuthorizationTracking();
#endif
        }

        private void OnReady()
        {
            Debug.Log("[Ads] LevelPlay initialized.");
            if (Debug.isDebugBuild) LevelPlay.ValidateIntegration(); // logs adapter/integration status

            if (!string.IsNullOrEmpty(_bannerId))
            {
                _banner = new LevelPlayBannerAd(_bannerId); // defaults to a bottom-centre banner
                _banner.OnAdLoaded     += _ => { _bannerLoaded = true; if (_wantBanner) _banner.ShowAd(); else _banner.HideAd(); };
                _banner.OnAdLoadFailed += err => Debug.LogWarning("[Ads] banner load failed: " + err);
                _banner.LoadAd();
            }

            if (!string.IsNullOrEmpty(_rewardedId))
            {
                _rewarded = new LevelPlayRewardedAd(_rewardedId);
                _rewarded.OnAdRewarded      += (_, __) => { _onReward?.Invoke(); Clear(); };
                _rewarded.OnAdClosed        += _ => _rewarded.LoadAd();                    // preload the next one
                _rewarded.OnAdDisplayFailed += (_, __) => { _onUnavailable?.Invoke(); Clear(); _rewarded.LoadAd(); };
                _rewarded.LoadAd();
            }
        }

        private void Clear() { _onReward = _onUnavailable = null; }

        // ── Banner ───────────────────────────────────────────────────────────
        public void ShowBanner() { _wantBanner = true; if (_bannerLoaded) _banner?.ShowAd(); }
        public void HideBanner() { _wantBanner = false; _banner?.HideAd(); }

        // ── Rewarded ─────────────────────────────────────────────────────────
        public bool IsRewardedReady => _rewarded != null && _rewarded.IsAdReady();
        public void LoadRewarded() { if (_rewarded != null && !_rewarded.IsAdReady()) _rewarded.LoadAd(); }
        public void ShowRewarded(Action onReward, Action onUnavailable)
        {
            if (!IsRewardedReady) { onUnavailable?.Invoke(); return; }
            _onReward = onReward; _onUnavailable = onUnavailable;
            _rewarded.ShowAd();
        }

        // ── Lifecycle + debug ────────────────────────────────────────────────
        public void OnAppPause(bool paused) => LevelPlay.SetPauseGame(paused);
        // Opens LevelPlay's mediation Test Suite: shows this device's advertising ID and
        // lets you verify each network's adapter. Reachable from Settings in debug builds.
        public void LaunchTestSuite() => LevelPlay.LaunchTestSuite();
    }
}
