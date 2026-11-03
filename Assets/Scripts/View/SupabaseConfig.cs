using UnityEngine;

namespace KnightsGauntlet.View
{
    // Holds the Supabase URL + publishable key in
    //   Assets/Resources/SupabaseConfig.asset
    // (Create > Knight's Gauntlet > Supabase Config). The asset IS committed: the iOS
    // build is made on a separate Mac from the repo, and without this file that build
    // silently runs with the online leaderboard off. The key is public by design (it
    // ships inside every build anyway); the data is protected by Row Level Security.
    [CreateAssetMenu(fileName = "SupabaseConfig", menuName = "Knight's Gauntlet/Supabase Config")]
    public class SupabaseConfig : ScriptableObject
    {
        [Tooltip("e.g. https://yourproject.supabase.co")]
        public string url;

        [Tooltip("anon / publishable key: public by design, your data is protected by RLS, not by hiding this")]
        public string key;

        // Finds the asset in any Assets/**/Resources/ folder. Returns null if absent.
        public static SupabaseConfig Load() => Resources.Load<SupabaseConfig>("SupabaseConfig");
    }
}
