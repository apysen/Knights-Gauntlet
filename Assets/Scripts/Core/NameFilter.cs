using System.Text;

namespace KnightsGauntlet.Core
{
    // Port of the JS name filter (normalizeForFilter + isNameAllowed). Normalizes
    // common leetspeak, strips non-letters, then rejects names containing a banned
    // substring. Pure C#, so it can be unit-tested and reused anywhere.
    //
    // This is moderation infrastructure (it only ever REJECTS names) required for
    // App Store user-generated-content compliance on the leaderboard.
    public static class NameFilter
    {
        private static readonly string[] Banned =
        {
            "fuck","shit","bitch","cunt","dick","cock","pussy","asshole","bastard",
            "slut","whore","fag","faggot","nigger","nigga","retard","rape","nazi",
            "hitler","kike","spic","chink","wank","twat","prick","douche","jizz",
            "cum","penis","vagina","boob","tits","sex","porn","anus","butt","damn"
        };

        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char raw in s.ToLowerInvariant())
            {
                char c = raw;
                switch (c)
                {
                    case '@': case '4': c = 'a'; break;
                    case '3': c = 'e'; break;
                    case '1': case '!': case '|': c = 'i'; break;
                    case '0': c = 'o'; break;
                    case '5': case '$': c = 's'; break;
                    case '7': c = 't'; break;
                }
                if (c >= 'a' && c <= 'z') sb.Append(c);
            }
            return sb.ToString();
        }

        public static bool IsAllowed(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var n = Normalize(name);
            if (n.Length == 0) return false;
            foreach (var w in Banned)
                if (n.Contains(w)) return false;
            return true;
        }
    }
}
