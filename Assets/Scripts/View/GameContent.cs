namespace KnightsGauntlet.View
{
    // Editable text content for the How-to and Patch-notes screens. (The original
    // had SVG illustrations per how-to page; those can be added later as sprites.)
    public static class GameContent
    {
        public static readonly (string title, string body)[] HowTo =
        {
            ("Move like a knight",
             "You move in an L-shape, just like a chess knight. Tap any highlighted square to leap there. (Grandmaster hides the highlights.)"),
            ("Two ways to play",
             "Trap mode: thread through the gaps in the falling trap rows and survive. Battle mode: capture the enemy pieces for points."),
            ("Trap tactics",
             "Trap mode is about progress, not combat: thread through the gaps in each falling row to push forward. When every gap is out of reach, remember — sometimes moving backward is the best way forward."),
            ("Build a combo",
             "In Battle, capturing on back-to-back moves builds your multiplier — and cheaper pieces add the most. You always get at least one free miss before it resets, more on easier modes."),
            ("Transform",
             "Capture 3 without breaking your combo and you become that 3rd piece for 5 seconds, gaining its movement. Make a knight your 3rd and you turn into a nightrider that copies each piece it then captures."),
            ("Stay aggressive",
             "Every capture refills the 5-second timer, so keep hunting. While transformed, getting caught costs you the form and your multiplier — but not the run."),
        };

        public static readonly (string version, string date, string notes)[] Patches =
        {
            ("Unity 0.1", "2026-06",
             "First Unity build.\n\n• Trap and Battle modes ported from the web version\n• Achievements and skins\n• Native leaderboards (Game Center / Play Games)\n• Swappable art theming"),
        };
    }
}
