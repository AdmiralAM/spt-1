namespace SPTItemIntelligence
{
    // II-owned category rules, retaining the accepted valuation palette.
    public static class BackgroundPalette
    {
        public static string Money(double value) => value < 10000 ? "" : value < 25000 ? "#526B3F" : value < 50000 ? "#294F31" : value < 75000 ? "#253552" : value < 100000 ? "#4A3854" : value < 250000 ? "#5A2C31" : "#5C4825";
        public static string Ammo(double penetration) => penetration < 10 ? "" : penetration <= 20 ? "#526B3F" : penetration <= 40 ? "#253552" : penetration <= 50 ? "#4A3854" : penetration <= 70 ? "#5A2C31" : "#5C4825";
        public static string Key(double value, bool fleaAllowed) => !fleaAllowed ? "#660415" : value < 10000 ? "#404040" : value < 20000 ? "#a3a3a3" : value < 30000 ? "#0c3b08" : value < 50000 ? "#08083b" : value < 75000 ? "#590b5e" : "#5e470b";
    }
}
