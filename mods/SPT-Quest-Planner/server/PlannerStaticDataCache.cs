using SPTQuestPlanner;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTQuestPlanner.Server;

[Injectable]
public sealed class PlannerStaticDataCache
{
    private readonly Lazy<PlannerStaticData> _data;

    public PlannerStaticDataCache(TemplateTable templateTable)
    {
        _data = new Lazy<PlannerStaticData>(
            () => Build(templateTable),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public PlannerStaticData Get() => _data.Value;

    private static PlannerStaticData Build(TemplateTable templateTable)
    {
        QuestExtractionResult extraction = QuestExtractor.Extract(templateTable.Quests);
        QuestObjectiveExtractionResult objectives = QuestObjectiveExtractor.Extract(templateTable.Quests);
        var (graph, validation) = PlannerGraph.Build(extraction.Nodes, extraction.Prerequisites);

        return new PlannerStaticData(
            extraction,
            objectives,
            graph,
            validation);
    }
}

public sealed record PlannerStaticData(
    QuestExtractionResult Extraction,
    QuestObjectiveExtractionResult ObjectiveExtraction,
    PlannerGraph Graph,
    PlannerGraphValidation Validation);
