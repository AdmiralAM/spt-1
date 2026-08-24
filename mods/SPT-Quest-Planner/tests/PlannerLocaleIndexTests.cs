using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerLocaleIndexTests
{
    [Fact]
    public void Build_ParsesQuestAndItemNames()
    {
        const string json = "{\"schemaVersion\":9,\"locale\":\"en\",\"questNames\":{\"q1\":\"Debut\"},\"itemNames\":{\"tpl1\":\"MS2000 Marker\"}}";

        PlannerLocaleIndex index = PlannerLocaleIndexBuilder.Build(json);

        Assert.Equal("en", index.Locale);
        Assert.Equal("Debut", index.QuestName("q1"));
        Assert.Equal("MS2000 Marker", index.ItemName("tpl1"));
    }

    [Fact]
    public void MissingName_FallsBackToId()
    {
        PlannerLocaleIndex index = new(
            "en",
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal("q-x", index.QuestName("q-x"));
        Assert.Equal("tpl-x", index.ItemName("tpl-x"));
    }

    [Fact]
    public void QuestLabel_PrefersLocalizedNameOverTopologyFallback()
    {
        PlannerTopologyIndex topology = new(
            new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerTopologyQuest("q1", null, "technical-key", null, false, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
            },
            new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
        PlannerLocaleIndex locale = new(
            "en",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["q1"] = "Localized Quest" },
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal("Localized Quest", PlannerQuestLabels.Resolve(topology, locale, "q1"));
    }
}
