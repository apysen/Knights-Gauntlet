#if UNITY_IOS
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace KnightsGauntlet.Editor
{
    // Injects the ad-network SKAdNetworkItems into the generated Xcode Info.plist on every
    // iOS build. On iOS 14+, ad networks can only attribute installs (and pay out) if their
    // SKAdNetwork ID is listed here, so a missing list means lost ad revenue. Unity rebuilds
    // Info.plist each iOS build, so this must run as a post-process rather than a static edit.
    //
    // Runs at order 100 (after most other processors). Existing entries are preserved and
    // duplicates are skipped, so it composes safely with any IDs LevelPlay/adapters add.
    public static class SKAdNetworkPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath)) { Debug.LogWarning("[SKAdNetwork] Info.plist not found at " + plistPath); return; }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

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
            Debug.Log($"[SKAdNetwork] Added {added} new identifiers ({present.Count} total) to Info.plist.");
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
