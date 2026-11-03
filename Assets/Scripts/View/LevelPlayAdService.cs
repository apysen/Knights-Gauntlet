using System;
using System.Collections.Concurrent;
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
    //
    // Threading: on iOS the SDK calls straight back from native completion blocks, and on
    // Android from Java listener threads, so a callback can arrive off the Unity main thread.
    // Every SDK callback is therefore queued and run on the main thread by a tiny pump
    // object. Banner and rewarded setup are also isolated from each other, so a failure in
    // one can never stop the other from loading.
    public class LevelPlayAdService : IAdService
    {
        private readonly string _appKey, _bannerId, _rewardedId;
        private LevelPlayBannerAd _banner;
        private LevelPlayRewardedAd _rewarded;
        private bool _wantBanner, _bannerLoaded, _bannerRefreshPaused;
        private bool _setupDone;
        private Action _onReward, _onUnavailable;
        private Pump _pump;

        // Seconds before retrying a rewarded load that came back empty (no fill, offline).
        private const float RewardedRetrySeconds = 30f;

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
            var go = new GameObject("LevelPlayPump");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _pump = go.AddComponent<Pump>();

            ApplyPrivacy();            // COPPA/GDPR flags, set before Init
            RequestTrackingIfNeeded(); // iOS App Tracking Transparency (no-op elsewhere)
            LevelPlay.OnInitSuccess += _ => _pump.Post(OnReady);
            LevelPlay.OnInitFailed  += err => _pump.Post(() => Debug.LogWarning("[Ads] LevelPlay init failed: " + err));
            Debug.Log("[Ads] LevelPlay init starting (app key " + _appKey + ").");
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

        // Runs on the main thread (posted by the pump).
        //
        // Guarded because OnInitSuccess must only ever drive setup once. It fired twice per
        // launch while LevelPlayMediationSettings.EnableIronsourceSDKInitAPI was on, because
        // LevelPlayAutoInitializer called LevelPlay.Init as well as we did and each
        // completion raised the event - which built a second LevelPlayBannerAd for the same
        // ad unit, overwrote _banner with it, and left the first object loading and
        // auto-refreshing with nothing able to show or hide it. That setting is now off, so
        // this is belt-and-braces against the event ever repeating.
        private void OnReady()
        {
            if (_setupDone) return;
            _setupDone = true;

            Debug.Log("[Ads] LevelPlay initialized.");
            // Rewarded first: it is the format players actively ask for, so nothing in the
            // banner setup below may ever get in its way.
            Guard("rewarded setup", SetupRewarded);
            Guard("banner setup", SetupBanner);
            if (Debug.isDebugBuild) Guard("integration check", LevelPlay.ValidateIntegration);
        }

        private static void Guard(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { Debug.LogError("[Ads] " + what + " threw: " + e); }
        }

        // ── Rewarded ─────────────────────────────────────────────────────────
        private void SetupRewarded()
        {
            if (string.IsNullOrEmpty(_rewardedId)) return;
            _rewarded = new LevelPlayRewardedAd(_rewardedId);
            _rewarded.OnAdLoaded        += _ => _pump.Post(() => Debug.Log("[Ads] rewarded loaded."));
            _rewarded.OnAdLoadFailed    += err => _pump.Post(() =>
            {
                Debug.LogWarning("[Ads] rewarded load failed: " + err + ". Retrying in " + RewardedRetrySeconds + "s.");
                _pump.After(RewardedRetrySeconds, LoadRewarded);
            });
            _rewarded.OnAdRewarded      += (_, __) => _pump.Post(() => { _onReward?.Invoke(); Clear(); });
            _rewarded.OnAdClosed        += _ => _pump.Post(LoadRewarded);   // preload the next one
            _rewarded.OnAdDisplayFailed += (_, err) => _pump.Post(() =>
            {
                Debug.LogWarning("[Ads] rewarded display failed: " + err);
                _onUnavailable?.Invoke(); Clear(); LoadRewarded();
            });
            _rewarded.LoadAd();
        }

        private void Clear() { _onReward = _onUnavailable = null; }

        public bool IsRewardedReady => _rewarded != null && _rewarded.IsAdReady();
        public void LoadRewarded()
        {
            if (_rewarded == null || _rewarded.IsAdReady()) return;
            Guard("rewarded load", _rewarded.LoadAd);
        }
        public void ShowRewarded(Action onReward, Action onUnavailable)
        {
            if (!IsRewardedReady) { onUnavailable?.Invoke(); return; }
            _onReward = onReward; _onUnavailable = onUnavailable;
            _rewarded.ShowAd();
        }

        // ── Banner ───────────────────────────────────────────────────────────
        private void SetupBanner()
        {
            if (string.IsNullOrEmpty(_bannerId)) return;
            // DisplayOnLoad defaults to true, which makes the native SDK pop the banner back
            // up on every auto-refresh, even mid-run while we want it hidden. Turn it off so
            // the banner only ever appears through an explicit ShowAd from us.
            var cfg = new LevelPlayBannerAd.Config.Builder()
                .SetPosition(LevelPlayBannerPosition.BottomCenter)
                .SetDisplayOnLoad(false)
                .Build();
            _banner = new LevelPlayBannerAd(_bannerId, cfg);
            _banner.OnAdLoaded     += _ => _pump.Post(() =>
            {
                if (!_bannerLoaded) Debug.Log("[Ads] banner loaded.");
                _bannerLoaded = true;
                ApplyBanner();
            });
            _banner.OnAdLoadFailed += err => _pump.Post(() => Debug.LogWarning("[Ads] banner load failed: " + err));
            _banner.LoadAd();  // with DisplayOnLoad off this hides the view...
            ApplyBanner();     // ...so re-show it right away if a menu already wants it
        }

        public void ShowBanner() { _wantBanner = true; ApplyBanner(); }
        public void HideBanner() { _wantBanner = false; ApplyBanner(); }

        // The view is shown whenever a menu wants it, even before the first ad has loaded
        // (an empty banner view is transparent, and the iOS SDK may not fill a hidden view).
        // Auto-refresh is only paused once an ad has actually loaded: pausing a banner that
        // is still on its first request could stall it forever. While paused mid-run no
        // fresh ad can load and surface over the HUD; refresh resumes on the next menu.
        private void ApplyBanner()
        {
            if (_banner == null) return;
            Guard("banner show/hide", () =>
            {
                if (_wantBanner)
                {
                    if (_bannerRefreshPaused) { _banner.ResumeAutoRefresh(); _bannerRefreshPaused = false; }
                    _banner.ShowAd();
                }
                else
                {
                    _banner.HideAd();
                    if (_bannerLoaded && !_bannerRefreshPaused) { _banner.PauseAutoRefresh(); _bannerRefreshPaused = true; }
                }
            });
        }

        // ── Lifecycle + debug ────────────────────────────────────────────────
        public void OnAppPause(bool paused) => LevelPlay.SetPauseGame(paused);
        // Opens LevelPlay's mediation Test Suite: shows this device's advertising ID and
        // lets you verify each network's adapter. Reachable from Settings in debug builds.
        public void LaunchTestSuite() => LevelPlay.LaunchTestSuite();

        // Runs queued SDK callbacks on the Unity main thread, plus simple delayed calls.
        private class Pump : MonoBehaviour
        {
            private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

            public void Post(Action a) { if (a != null) _queue.Enqueue(a); }

            public void After(float seconds, Action a) => StartCoroutine(AfterCo(seconds, a));
            private System.Collections.IEnumerator AfterCo(float seconds, Action a)
            {
                yield return new WaitForSecondsRealtime(seconds);
                a?.Invoke();
            }

            private void Update()
            {
                while (_queue.TryDequeue(out var a))
                {
                    try { a(); }
                    catch (Exception e) { Debug.LogError("[Ads] callback threw: " + e); }
                }
            }
        }
    }
}
