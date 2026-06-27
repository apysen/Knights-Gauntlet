using UnityEngine;

namespace KnightsGauntlet.View
{
    // Holds the Supabase URL + anon key OUTSIDE any committed script or scene, so
    // the file can be gitignored. Create the asset at
    //   Assets/Resources/SupabaseConfig.asset
    // (Create > Knight's Gauntlet > Supabase Config), fill it in, then gitignore
    // it. A fresh clone without this file just runs with the online leaderboard
    // disabled (personal scores still work).
    [CreateAssetMenu(fileName = "SupabaseConfig", menuName = "Knight's Gauntlet/Supabase Config")]
    public class SupabaseConfig : ScriptableObject
    {
        [Tooltip("e.g. https://yourproject.supabase.co")]
        public string url;

        [Tooltip("anon / publishable key — public by design; your data is protected by RLS, not by hiding this")]
        public string key;

        // Finds the asset in any Assets/**/Resources/ folder. Returns null if absent.
        public static SupabaseConfig Load() => Resources.Load<SupabaseConfig>("SupabaseConfig");
    }
}
