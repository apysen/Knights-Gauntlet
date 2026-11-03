#if UNITY_IOS || UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KnightsGauntlet.Editor
{
    // Batchmode entry point for exporting the iOS Xcode project from CI or a terminal.
    public static class CliBuild
    {
        public static void ExportIos()
        {
            string output = "ios-export";

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[CliBuild] No enabled scenes in EditorBuildSettings.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[CliBuild] Exporting iOS project to '{output}' with {scenes.Length} scene(s): {string.Join(", ", scenes)}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[CliBuild] Result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} time={summary.totalTime}");

            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
#endif
