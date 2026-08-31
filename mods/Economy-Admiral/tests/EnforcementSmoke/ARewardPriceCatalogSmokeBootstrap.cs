using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class ARewardPriceCatalogSmokeBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        QuestRewardHandbookPriceCatalog.Initialize(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["same-tpl"] = 100d,
            ["tpl-a"] = 100d,
            ["tpl-b"] = 100d,
            ["tpl-c"] = 100d,
        });
    }
}
