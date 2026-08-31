namespace SPTEconomy;

public static class QuestRewardHandbookPriceCatalog
{
    private static IReadOnlyDictionary<string, double> prices = new Dictionary<string, double>(StringComparer.Ordinal);

    public static void Initialize(IEnumerable<KeyValuePair<string, double>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var snapshot = source
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && double.IsFinite(pair.Value) && pair.Value > 0)
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);
        prices = snapshot;
    }

    public static bool TryGet(string templateId, out double price)
    {
        price = 0;
        return !string.IsNullOrWhiteSpace(templateId)
            && prices.TryGetValue(templateId, out price)
            && double.IsFinite(price)
            && price > 0;
    }
}
