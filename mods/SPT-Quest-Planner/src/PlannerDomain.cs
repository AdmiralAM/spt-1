namespace SPTQuestPlanner;

public enum QuestState
{
    Unknown = 0,
    Locked = 1,
    Available = 2,
    Started = 3,
    Success = 4,
    Failed = 5
}

public sealed record QuestNode(
    string QuestId,
    string? TraderId,
    string? NameKey,
    int? MinimumLevel,
    bool Repeatable);

public sealed record PrerequisiteEdge(
    string SourceQuestId,
    string TargetQuestId,
    IReadOnlySet<QuestState> AcceptedSourceStates,
    string? GroupId = null);

public sealed record ItemRequirement(
    string QuestId,
    string ConditionId,
    IReadOnlyList<string> TemplateIds,
    double RequiredCount,
    bool FoundInRaid,
    string Phase);

public sealed record PlannerGraphValidation(
    IReadOnlyList<string> DuplicateQuestIds,
    IReadOnlyList<PrerequisiteEdge> DanglingEdges,
    IReadOnlyList<IReadOnlyList<string>> Cycles)
{
    public bool IsValid => DuplicateQuestIds.Count == 0 && DanglingEdges.Count == 0 && Cycles.Count == 0;
}

public sealed class PlannerGraph
{
    private readonly IReadOnlyDictionary<string, QuestNode> _nodes;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PrerequisiteEdge>> _incoming;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PrerequisiteEdge>> _outgoing;

    private PlannerGraph(
        IReadOnlyDictionary<string, QuestNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<PrerequisiteEdge>> incoming,
        IReadOnlyDictionary<string, IReadOnlyList<PrerequisiteEdge>> outgoing)
    {
        _nodes = nodes;
        _incoming = incoming;
        _outgoing = outgoing;
    }

    public IReadOnlyDictionary<string, QuestNode> Nodes => _nodes;

    public IReadOnlyList<PrerequisiteEdge> GetPrerequisites(string questId) =>
        _incoming.TryGetValue(questId, out var value) ? value : Array.Empty<PrerequisiteEdge>();

    public IReadOnlyList<PrerequisiteEdge> GetDependents(string questId) =>
        _outgoing.TryGetValue(questId, out var value) ? value : Array.Empty<PrerequisiteEdge>();

    public IReadOnlySet<string> GetReachableDependents(string questId)
    {
        if (!_nodes.ContainsKey(questId)) return new HashSet<string>(StringComparer.Ordinal);

        HashSet<string> visited = new(StringComparer.Ordinal);
        Queue<string> pending = new();
        pending.Enqueue(questId);

        while (pending.Count > 0)
        {
            string current = pending.Dequeue();
            foreach (var edge in GetDependents(current))
            {
                if (!visited.Add(edge.TargetQuestId)) continue;
                pending.Enqueue(edge.TargetQuestId);
            }
        }

        visited.Remove(questId);
        return visited;
    }

    public static (PlannerGraph Graph, PlannerGraphValidation Validation) Build(
        IEnumerable<QuestNode> nodes,
        IEnumerable<PrerequisiteEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        List<string> duplicates = new();
        Dictionary<string, QuestNode> nodeIndex = new(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.QuestId)) continue;
            if (!nodeIndex.TryAdd(node.QuestId, node)) duplicates.Add(node.QuestId);
        }

        Dictionary<string, List<PrerequisiteEdge>> incoming = new(StringComparer.Ordinal);
        Dictionary<string, List<PrerequisiteEdge>> outgoing = new(StringComparer.Ordinal);
        List<PrerequisiteEdge> dangling = new();

        foreach (var edge in edges)
        {
            if (!nodeIndex.ContainsKey(edge.SourceQuestId) || !nodeIndex.ContainsKey(edge.TargetQuestId))
            {
                dangling.Add(edge);
                continue;
            }

            Add(incoming, edge.TargetQuestId, edge);
            Add(outgoing, edge.SourceQuestId, edge);
        }

        var graph = new PlannerGraph(
            nodeIndex,
            Freeze(incoming),
            Freeze(outgoing));

        PlannerGraphValidation validation = new(
            duplicates.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            dangling,
            FindCycles(nodeIndex.Keys, graph._outgoing));

        return (graph, validation);
    }

    private static void Add(Dictionary<string, List<PrerequisiteEdge>> index, string key, PrerequisiteEdge edge)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<PrerequisiteEdge>();
            index[key] = list;
        }
        list.Add(edge);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<PrerequisiteEdge>> Freeze(
        Dictionary<string, List<PrerequisiteEdge>> source) =>
        source.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<PrerequisiteEdge>)pair.Value.ToArray(),
            StringComparer.Ordinal);

    private static IReadOnlyList<IReadOnlyList<string>> FindCycles(
        IEnumerable<string> questIds,
        IReadOnlyDictionary<string, IReadOnlyList<PrerequisiteEdge>> outgoing)
    {
        Dictionary<string, byte> state = new(StringComparer.Ordinal);
        List<string> stack = new();
        Dictionary<string, int> stackPositions = new(StringComparer.Ordinal);
        List<IReadOnlyList<string>> cycles = new();

        foreach (string questId in questIds)
        {
            if (state.TryGetValue(questId, out byte value) && value != 0) continue;
            Visit(questId, state, stack, stackPositions, outgoing, cycles);
        }

        return cycles;
    }

    private static void Visit(
        string questId,
        Dictionary<string, byte> state,
        List<string> stack,
        Dictionary<string, int> stackPositions,
        IReadOnlyDictionary<string, IReadOnlyList<PrerequisiteEdge>> outgoing,
        List<IReadOnlyList<string>> cycles)
    {
        state[questId] = 1;
        stackPositions[questId] = stack.Count;
        stack.Add(questId);

        if (outgoing.TryGetValue(questId, out var edges))
        {
            foreach (var edge in edges)
            {
                string next = edge.TargetQuestId;
                state.TryGetValue(next, out byte nextState);
                if (nextState == 0)
                {
                    Visit(next, state, stack, stackPositions, outgoing, cycles);
                }
                else if (nextState == 1 && stackPositions.TryGetValue(next, out int start))
                {
                    string[] cycle = stack.Skip(start).Append(next).ToArray();
                    cycles.Add(cycle);
                }
            }
        }

        stack.RemoveAt(stack.Count - 1);
        stackPositions.Remove(questId);
        state[questId] = 2;
    }
}
