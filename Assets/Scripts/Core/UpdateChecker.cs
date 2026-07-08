using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.AppUpdate;
using Google.Play.Common;
#endif

namespace KnightsGauntlet.View
{
    // Forces a Google Play "immediate" update on launch whenever a newer version
    // is live on the Play Store. Google's own full-screen flow handles all of the
    // UI (download, install, restart) — there is no custom panel.
    //
    // It self-bootstraps at launch, so nothing else needs to reference it.
    //
    // NOTE: everything real is behind `UNITY_ANDROID && !UNITY_EDITOR`, so:
    //   - the EDITOR compiles and runs fine WITHOUT the plugin imported (inert), and
    //   - an actual ANDROID BUILD requires the "Google Play In-App Updates" plugin
    //     (Google.Play.AppUpdate). In-app updates only work on a build installed
    //     through Google Play anyway, so there's nothing to test in the editor.
    public class UpdateChecker : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AppUpdateManager _manager;
        private bool _running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("UpdateChecker");
            DontDestroyOnLoad(go);
            go.AddComponent<UpdateChecker>();
        }

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
                // installs, and restarts the app on success — so the code after this
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
    }
}
