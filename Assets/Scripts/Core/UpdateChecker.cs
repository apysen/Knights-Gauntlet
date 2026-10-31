using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.AppUpdate;
using Google.Play.Common;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using System;
using UnityEngine.Networking;
#endif

namespace KnightsGauntlet.View
{
    // Forces the player onto the current version at launch whenever a newer one is live
    // on the store. It self-bootstraps, so nothing else needs to reference it.
    //
    // The two platforms get there very differently:
    //
    //   ANDROID - Google Play In-App Updates. Google's own full-screen flow handles all
    //     of the UI (download, install, restart), so there is no custom panel. Requires
    //     the "Google Play In-App Updates" plugin (Google.Play.AppUpdate), and only works
    //     on a build actually installed through Google Play.
    //
    //   IOS - Apple has no in-app update API at all, so we roll our own: ask the iTunes
    //     lookup endpoint what version is live, compare it against Application.version,
    //     and if ours is older show a blocking panel whose only action is a link out to
    //     the App Store. The player updates there and comes back; we cannot install it
    //     for them, which is why this gate is a panel rather than a download flow.
    //
    // Everything real is behind `!UNITY_EDITOR`, so the EDITOR compiles and runs fine
    // without either store present (the class is simply inert). Neither flow is testable
    // in the editor anyway - both need a store-installed build.
    public class UpdateChecker : MonoBehaviour
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private bool _running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("UpdateChecker");
            DontDestroyOnLoad(go);
            go.AddComponent<UpdateChecker>();
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private AppUpdateManager _manager;

        private void Start()
        {
            _manager = new AppUpdateManager();
            StartCoroutine(Run());
        }

        // Re-check when the player returns to the app: resumes an update that was
        // interrupted by backgrounding, and re-forces the prompt if they had backed
        // out of Google's dialog.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && !_running && _manager != null) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _running = true;
            var options = AppUpdateOptions.ImmediateAppUpdateOptions();

            while (true)
            {
                var infoOp = _manager.GetAppUpdateInfo();
                yield return infoOp;

                // Couldn't reach the Play Store (offline / not a Play install):
                // don't block the player.
                if (!infoOp.IsSuccessful) break;

                var info = infoOp.GetResult();
                bool needsUpdate =
                    info.UpdateAvailability == UpdateAvailability.UpdateAvailable ||
                    info.UpdateAvailability == UpdateAvailability.DeveloperTriggeredUpdateInProgress;

                if (!needsUpdate || !info.IsUpdateTypeAllowed(options)) break; // already up to date

                // Immediate flow: Google shows a full-screen prompt, downloads,
                // installs, and restarts the app on success, so the code after this
                // is only reached if the user backed out or the update failed.
                var request = _manager.StartUpdate(info, options);
                yield return request;

                // Not updated yet. Since the update is mandatory, wait a beat and
                // require it again rather than letting them continue.
                yield return new WaitForSeconds(1f);
            }

            _running = false;
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private GameObject _gate;

        private void Start() => StartCoroutine(Run());

        // Re-check on resume, so a player who backgrounded the app, updated in the App
        // Store, and came back finds the gate gone. Skipped once the gate is up: it is
        // the terminal state, and re-running would only rebuild it.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && !_running && _gate == null) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _running = true;

            // Apple's public lookup endpoint. Keyed off Application.identifier rather than a
            // hardcoded id so it cannot drift from the bundle id. Two caveats worth knowing:
            // it answers for the US storefront by default, and Apple's CDN caches the result,
            // so a freshly-released version takes a little while to start being enforced.
            string url = "https://itunes.apple.com/lookup?bundleId=" +
                         UnityWebRequest.EscapeURL(Application.identifier);

            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();

                // Offline, rate-limited, or the app is not on the store yet (first submission).
                // Fail open: never lock someone out of the game over a failed version check.
                if (req.result != UnityWebRequest.Result.Success) { _running = false; yield break; }

                StoreListing listing = null;
                try
                {
                    var payload = JsonUtility.FromJson<LookupResponse>(req.downloadHandler.text);
                    if (payload != null && payload.results != null && payload.results.Length > 0)
                        listing = payload.results[0];
                }
                catch (Exception e) { Debug.LogWarning("[UpdateChecker] lookup parse failed: " + e.Message); }

                if (listing != null && !string.IsNullOrEmpty(listing.version) &&
                    IsOlder(Application.version, listing.version))
                    ShowGate(listing.trackViewUrl);
            }

            _running = false;
        }

        // True if `current` is strictly behind `live`. Compares dotted numeric components
        // ("0.99" < "1.0" < "1.0.1"), treating a missing component as zero. Anything that
        // is not a plain integer returns false: an unparseable version must never be able
        // to lock a player out.
        private static bool IsOlder(string current, string live)
        {
            string[] a = current.Split('.'), b = live.Split('.');
            int len = Mathf.Max(a.Length, b.Length);

            for (int i = 0; i < len; i++)
            {
                if (!int.TryParse(i < a.Length ? a[i] : "0", out int x)) return false;
                if (!int.TryParse(i < b.Length ? b[i] : "0", out int y)) return false;
                if (x != y) return x < y;
            }
            return false; // identical
        }

        // A full-screen gate with exactly one way out: the App Store. Deliberately has no
        // dismiss button, mirroring the mandatory "immediate" flow used on Android.
        private void ShowGate(string storeUrl)
        {
            var ui = new UIFactory(null); // no font passed: UIFactory falls back to the pixel font
            ui.EnsureEventSystem();

            // Sorted above every gameplay and menu canvas so nothing can draw over the gate.
            var canvas = ui.CreateCanvas("UpdateGate", 5000);
            _gate = canvas.gameObject;
            DontDestroyOnLoad(_gate);

            var screen = ui.Screen(canvas.transform, UIFactory.Bg);
            var col = ui.Column(screen.transform, 20f);

            ui.Label(col, "UPDATE REQUIRED", 30f, UIFactory.Gold, TMPro.FontStyles.Bold);
            ui.Label(col, "A new version of Knight's Gauntlet is available. " +
                          "Please update to keep playing.", 18f, UIFactory.Text);

            // If the listing carried no direct URL, fall back to a store search so the
            // button is never a dead end.
            string target = string.IsNullOrEmpty(storeUrl)
                ? "https://apps.apple.com/search?term=" + UnityWebRequest.EscapeURL(Application.productName)
                : storeUrl;

            ui.Button(col, "UPDATE", () => Application.OpenURL(target));
        }

        // Shapes for JsonUtility. The lookup response carries many more fields than these;
        // JsonUtility simply ignores the ones we do not declare.
        [Serializable] private class LookupResponse { public StoreListing[] results; }
        [Serializable] private class StoreListing { public string version; public string trackViewUrl; }
#endif
    }
}
