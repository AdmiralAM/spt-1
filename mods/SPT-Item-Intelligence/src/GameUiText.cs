namespace SPTItemIntelligence
{
    internal static class GameUiText
    {
        internal static bool Russian { get; private set; }
        internal static int LanguageKey { get; private set; }

        internal static void SetRussian(bool russian)
        {
            if (Russian == russian) return;
            Russian = russian;
            LanguageKey++;
        }

        internal static string T(string english, string russian) => Russian ? russian : english;
    }
}
