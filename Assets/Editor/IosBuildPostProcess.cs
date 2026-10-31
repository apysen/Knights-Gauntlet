#if UNITY_IOS
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;   // AddFileToEmbedFrameworks
using UnityEngine;

namespace KnightsGauntlet.Editor
{
    // Fills in the Info.plist keys Unity does not generate itself, on every iOS build.
    // Unity rebuilds Info.plist from scratch each build, so these must be applied as a
    // post-process rather than edited into the Xcode project by hand.
    //
    // Three things get written:
    //   - SKAdNetworkItems: on iOS 14+, ad networks can only attribute installs (and pay
    //     out) if their SKAdNetwork ID is listed. A missing list means lost ad revenue.
    //   - NSUserTrackingUsageDescription: the App Tracking Transparency prompt's text.
    //     Without it, iOS kills the app the moment LevelPlayAdService requests tracking,
    //     so this key and the LEVELPLAY_ATT define must ship together.
    //   - ITSAppUsesNonExemptEncryption: declares we use no non-exempt encryption, which
    //     skips the export-compliance questionnaire on every single TestFlight upload.
    //   - GADApplicationIdentifier: the AdMob app id. The Google Mobile Ads SDK hard-crashes
    //     at launch if this is missing - see AdMobApplicationIdentifier below.
    //
    // It also raises the CocoaPods deployment targets - see PatchPodDeploymentTargets.
    //
    // Runs at order 100 (after most other processors, and crucially after the External
    // Dependency Manager has run `pod install`). Existing SKAdNetwork entries are
    // preserved and duplicates skipped, so it composes safely with any IDs the LevelPlay
    // adapters add themselves.
    public static class IosBuildPostProcess
    {
        // Shown verbatim in the iOS tracking permission dialog, under the system's own
        // "Allow <App> to track your activity...?" line. Apple rejects builds whose text
        // does not explain the benefit to the user, so keep this concrete.
        private const string TrackingUsageDescription =
            "Allowing tracking lets us show ads that match your interests instead of random ones. " +
            "It also helps us measure which ads work, so we can keep Knight's Gauntlet free.";

        // AdMob comes in as a LevelPlay mediation adapter, and the Google Mobile Ads SDK
        // REQUIRES its app id in Info.plist. If the key is missing it does not degrade
        // gracefully - GADApplicationVerifyPublisherInitializedCorrectly raises an uncaught
        // NSException on a background queue and the app dies at launch with no splash.
        // Android tolerates its absence, which is why this only ever bites on iOS.
        //
        // This is the iOS app id from the AdMob console (App settings). It is NOT the Android
        // one - they differ per platform - and it is NOT an ad unit id (those use '/', app ids
        // use '~').
        private const string AdMobApplicationIdentifier = "ca-app-pub-8625750656535254~2166038450";

        // Anything below this in the generated Pods project gets raised to it. Matches the
        // app's own iOS target (Player Settings -> Target minimum iOS Version).
        private const int MinimumDeploymentTarget = 15;

        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            PatchPodDeploymentTargets(pathToBuiltProject);
            EmbedDynamicPodFrameworks(pathToBuiltProject);

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath)) { Debug.LogWarning("[iOS] Info.plist not found at " + plistPath); return; }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

            root.SetString("NSUserTrackingUsageDescription", TrackingUsageDescription);
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetString("GADApplicationIdentifier", AdMobApplicationIdentifier);

            PlistElementArray items = (root["SKAdNetworkItems"] as PlistElementArray) ?? root.CreateArray("SKAdNetworkItems");

            // Collect already-present IDs (case-insensitive) so we never duplicate.
            var present = new HashSet<string>();
            foreach (var e in items.values)
                if (e is PlistElementDict d && d["SKAdNetworkIdentifier"] is PlistElementString s)
                    present.Add(s.value.ToLowerInvariant());

            int added = 0;
            foreach (var id in Identifiers)
            {
                if (!present.Add(id)) continue; // already listed
                items.AddDict().SetString("SKAdNetworkIdentifier", id);
                added++;
            }

            plist.WriteToFile(plistPath);
            Debug.Log($"[iOS] Info.plist: tracking + encryption keys set, {added} new SKAdNetwork identifiers ({present.Count} total).");
        }

        // Most ad-SDK pods ship as static libraries, but at least one (AppLovinSDK) ships a
        // genuinely DYNAMIC framework. UnityFramework links against it, so the app records a
        // load-time dependency on @rpath/AppLovinSDK.framework/AppLovinSDK - but nothing ever
        // copies that framework into the .app, so dyld fails and iOS kills the process before
        // Unity starts. The symptom is brutal to diagnose: the app bounces straight back to
        // the home screen with no splash and no managed stack trace.
        //
        // The cause is structural. The External Dependency Manager's Podfile declares every
        // pod under the 'UnityFramework' target and leaves the 'Unity-iPhone' app target
        // empty. CocoaPods only generates an "[CP] Embed Pods Frameworks" phase for *app*
        // targets, and a framework target cannot embed its own dynamic dependencies - so no
        // embed phase is ever created. We add the missing embed ourselves.
        //
        // Rather than hardcode AppLovin (whose pod directory is version-stamped, e.g.
        // applovin-ios-sdk-13.6.3), we inspect every vendored xcframework's device slice and
        // embed exactly those whose Mach-O filetype is MH_DYLIB. That stays correct if a pod
        // switches between static and dynamic in a future SDK update.
        private static void EmbedDynamicPodFrameworks(string pathToBuiltProject)
        {
            string podsDir = Path.Combine(pathToBuiltProject, "Pods");
            string pbxPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            if (!Directory.Exists(podsDir) || !File.Exists(pbxPath)) return;

            var proj = new PBXProject();
            proj.ReadFromFile(pbxPath);
            string appTarget = proj.GetUnityMainTargetGuid();

            int embedded = 0;
            foreach (string xcf in Directory.GetDirectories(podsDir, "*.xcframework", SearchOption.AllDirectories))
            {
                if (!ContainsDynamicDeviceSlice(xcf)) continue;

                // Path must be relative to the Xcode project root for PBXSourceTree.Source.
                string rel = xcf.Substring(pathToBuiltProject.Length).TrimStart('/', '\\');
                if (proj.ContainsFileByProjectPath(rel)) continue; // already added

                string guid = proj.AddFile(rel, rel, PBXSourceTree.Source);
                proj.AddFileToEmbedFrameworks(appTarget, guid);
                embedded++;
                Debug.Log("[iOS] Embedding dynamic pod framework: " + Path.GetFileName(xcf));
            }

            if (embedded == 0) return;

            // dyld needs to be told to look inside the app bundle for them.
            proj.AddBuildProperty(appTarget, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
            proj.WriteToFile(pbxPath);
            Debug.Log($"[iOS] Embedded {embedded} dynamic pod framework(s) into the app target.");
        }

        // True if the xcframework's iOS *device* slice holds a dynamic library. Static pods
        // store an ar archive ("!<arch>") there; dynamic ones store a Mach-O whose filetype
        // field is MH_DYLIB (6). Simulator slices are ignored - they never ship.
        private static bool ContainsDynamicDeviceSlice(string xcframeworkPath)
        {
            foreach (string slice in Directory.GetDirectories(xcframeworkPath))
            {
                string name = Path.GetFileName(slice).ToLowerInvariant();
                if (!name.Contains("ios") || name.Contains("simulator") || name.Contains("maccatalyst")) continue;

                foreach (string fw in Directory.GetDirectories(slice, "*.framework"))
                {
                    string binary = Path.Combine(fw, Path.GetFileNameWithoutExtension(fw));
                    if (!File.Exists(binary)) continue;

                    try
                    {
                        using (var fs = File.OpenRead(binary))
                        {
                            var head = new byte[16];
                            if (fs.Read(head, 0, 16) < 16) continue;

                            uint magic = System.BitConverter.ToUInt32(head, 0);
                            // 64- and 32-bit little-endian thin Mach-O. Anything else (notably
                            // an "!<arch>" static archive) is not a dylib.
                            if (magic != 0xFEEDFACF && magic != 0xFEEDFACE) continue;

                            const uint MH_DYLIB = 6;
                            if (System.BitConverter.ToUInt32(head, 12) == MH_DYLIB) return true;
                        }
                    }
                    catch (IOException) { /* unreadable slice: treat as static */ }
                }
            }
            return false;
        }

        // Several ad-SDK pods (GoogleMobileAdsResources, the IronSource privacy-info
        // bundles) still declare an iOS 10 minimum in their podspecs. Xcode 26 refuses to
        // build anything below iOS 12, so the archive fails with:
        //   "IPHONEOS_DEPLOYMENT_TARGET is set to 10.0, but the range of supported
        //    deployment target versions is 12.0 to 26.5.99"
        //
        // The Podfile's own `platform :ios, '15.0'` does NOT fix this: a pod target's
        // podspec minimum overrides it. The usual remedy is a Podfile post_install hook,
        // but the External Dependency Manager regenerates the Podfile from scratch on
        // every build, so that edit would not survive. Patching the Pods project after
        // `pod install` has run does survive, which is why it lives here.
        private static void PatchPodDeploymentTargets(string pathToBuiltProject)
        {
            string pbxproj = Path.Combine(pathToBuiltProject, "Pods", "Pods.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxproj)) return; // no CocoaPods integration in this build

            var lines = File.ReadAllLines(pbxproj);
            const string key = "IPHONEOS_DEPLOYMENT_TARGET = ";
            int raised = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                int at = lines[i].IndexOf(key, System.StringComparison.Ordinal);
                if (at < 0) continue;

                int valueStart = at + key.Length;
                int end = lines[i].IndexOf(';', valueStart);
                if (end < 0) continue;

                string value = lines[i].Substring(valueStart, end - valueStart);
                // Compare on the major version only ("10.0" -> 10); that is all that
                // distinguishes the offending pods from the acceptable ones.
                if (!int.TryParse(value.Split('.')[0], out int major) || major >= MinimumDeploymentTarget) continue;

                lines[i] = lines[i].Substring(0, valueStart) + MinimumDeploymentTarget + ".0" + lines[i].Substring(end);
                raised++;
            }

            if (raised == 0) return;
            File.WriteAllLines(pbxproj, lines);
            Debug.Log($"[iOS] Raised {raised} pod build config(s) to iOS {MinimumDeploymentTarget}.0.");
        }

        // Apple requires these lowercase. Keep this list in sync with your ad networks'
        // published SKAdNetwork IDs (LevelPlay + each mediated network updates theirs over time).
        private static readonly string[] Identifiers =
        {
            "44jx6755aq.skadnetwork",
            "cstr6suwn9.skadnetwork",
            "5a6flpkh64.skadnetwork",
            "c6k4g5qg8m.skadnetwork",
            "m8dbw4sv7c.skadnetwork",
            "t38b2kh725.skadnetwork",
            "lr83yxwka7.skadnetwork",
            "v72qych5uu.skadnetwork",
            "e5fvkxwrpn.skadnetwork",
            "7ug5zh24hu.skadnetwork",
            "5lm9lj6jb7.skadnetwork",
            "5l3tpt7t6e.skadnetwork",
            "5f5u5tfb26.skadnetwork",
            "zq492l623r.skadnetwork",
            "zmvfpc5aq8.skadnetwork",
            "tl55sbb4fm.skadnetwork",
            "9nlqeag3gk.skadnetwork",
            "pwa73g5rt2.skadnetwork",
            "488r3q3dtq.skadnetwork",
            "2u9pt9hc89.skadnetwork",
            "av6w8kgt66.skadnetwork",
            "s39g8k73mm.skadnetwork",
            "ppxm28t8ap.skadnetwork",
            "3rd42ekr43.skadnetwork",
            "n9x2a789qt.skadnetwork",
            "4w7y6s5ca2.skadnetwork",
            "a8cz6cu7e5.skadnetwork",
            "f38h382jlk.skadnetwork",
            "xga6mpmplv.skadnetwork",
            "x44k69ngh6.skadnetwork",
            "2fnua5tdw4.skadnetwork",
            "3sh42y64q3.skadnetwork",
            "v79kvwwj4g.skadnetwork",
            "f7s53z58qe.skadnetwork",
            "k674qkevps.skadnetwork",
            "4fzdc2evr5.skadnetwork",
            "mlmmfzh3r3.skadnetwork",
            "6yxyv74ff7.skadnetwork",
            "97r2b46745.skadnetwork",
            "3qy4746246.skadnetwork",
            "5tjdwbrq8w.skadnetwork",
            "g6gcrrvk4p.skadnetwork",
            "hs6bdukanm.skadnetwork",
            "424m5254lk.skadnetwork",
            "w9q455wk68.skadnetwork",
            "vhf287vqwu.skadnetwork",
            "4pfyvq9l8r.skadnetwork",
            "kbd757ywx3.skadnetwork",
            "238da6jt44.skadnetwork",
            "4dzt52r2t5.skadnetwork",
            "mp6xlyr22a.skadnetwork",
            "22mmun2rn5.skadnetwork",
            "mj797d8u6f.skadnetwork",
            "p78axxw29g.skadnetwork",
            "294l99pt4k.skadnetwork",
            "8s468mfl3y.skadnetwork",
            "prcb7njmu6.skadnetwork",
            "wg4vff78zm.skadnetwork",
            "578prtvx9j.skadnetwork",
            "klf5c3l5u5.skadnetwork",
            "uw77j35x4d.skadnetwork",
            "wzmmz9fp6w.skadnetwork",
            "f73kdq92p3.skadnetwork",
            "mqn7fxpca7.skadnetwork",
            "v9wttpbfk9.skadnetwork",
            "k6y4y55b64.skadnetwork",
            "ydx93a7ass.skadnetwork",
            "9t245vhmpl.skadnetwork",
            "77y3x8wds4.skadnetwork",
            "glqzh8vgby.skadnetwork",
            "9rd848q2bz.skadnetwork",
            "yclnxrl5pm.skadnetwork",
            "32z4fx6l9h.skadnetwork",
            "a2p9lx4jpn.skadnetwork",
            "4468km3ulz.skadnetwork",
            "feyaarzu9v.skadnetwork",
        };
    }
}
#endif
