namespace KnightsGauntlet.View
{
    // What a top-5 finish in the Daily Challenge pays out. Shillings are modest now that
    // the prize also includes consumable powerups, which are the real draw: a 1st place
    // haul is worth roughly what the old flat 10,000 was, but most of it arrives as
    // Chronokinesis/Divine Intervention charges rather than wallet.
    public struct DailyPrize
    {
        public int Shillings;
        public int Chrono;      // Chronokinesis charges
        public int Divine;      // Divine Intervention charges
        public int Powerups => Chrono + Divine;
    }

    public static class DailyPrizes
    {
        // Index 0 = 1st place.
        private static readonly DailyPrize[] Table =
        {
            new DailyPrize { Shillings = 3000, Chrono = 2, Divine = 2 },
            new DailyPrize { Shillings = 2000, Chrono = 1, Divine = 1 },
            new DailyPrize { Shillings = 1200, Chrono = 1, Divine = 1 },
            new DailyPrize { Shillings =  800, Chrono = 1, Divine = 0 },
            new DailyPrize { Shillings =  600, Chrono = 0, Divine = 1 },
        };

        public static int Places => Table.Length;
        public static bool Pays(int place) => place >= 1 && place <= Table.Length;
        public static DailyPrize For(int place) => Table[place - 1];   // 1-based

        // Compact two-line badge for the daily leaderboard rows.
        public static string Badge(int place)
        {
            var p = For(place);
            string s = "+" + p.Shillings;
            if (p.Powerups > 0)
                s += "\n<size=65%><color=#8A8FA3>+" + p.Powerups + (p.Powerups == 1 ? " powerup" : " powerups") + "</color></size>";
            return s;
        }

        // Full breakdown for the "you placed!" popup.
        public static string Describe(int place)
        {
            var p = For(place);
            string s = "<size=140%><color=#F0C040>+" + p.Shillings + " shillings</color></size>";
            if (p.Chrono > 0) s += "\n<color=#F0C040>+" + p.Chrono + " " + Powerups.Chronokinesis.Name + "</color>";
            if (p.Divine > 0) s += "\n<color=#F0C040>+" + p.Divine + " " + Powerups.DivineIntervention.Name + "</color>";
            return s;
        }
    }
}
