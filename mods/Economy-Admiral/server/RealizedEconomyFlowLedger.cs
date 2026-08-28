using System.Text.Json;
using System.Text.Json.Serialization;

namespace EconomyAdmiral;

public enum EconomyFlowDirection { Inflow, Outflow }
public enum EconomyFlowChannel { QuestReward, TraderPurchase, TraderSale, Barter, Craft, RaidLoot, Insurance, Consumption, Manual, Unknown }

public sealed record EconomyFlowEvent(
    DateTimeOffset Timestamp,
    string ItemId,
    EconomyFlowDirection Direction,
    EconomyFlowChannel Channel,
    double Quantity,
    double? ReferenceValue,
    string Attribution = "Unknown");

public sealed record EconomyFlowAggregate(
    string ItemId,
    EconomyFlowChannel Channel,
    double InflowQuantity,
    double OutflowQuantity,
    double NetQuantity,
    double? InflowReferenceValue,
    double? OutflowReferenceValue,
    int EventCount,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen);

/// <summary>
/// Policy-free local realized-flow evidence. It intentionally knows nothing about SPT mutation policy:
/// callers must provide events only at proven transaction boundaries. Unknown attribution/channel is
/// preserved rather than guessed. Aggregation is deterministic and bounded by key cardinality.
/// </summary>
public sealed class RealizedEconomyFlowLedger
{
    private readonly int maxAggregateKeys;
    private readonly Dictionary<(string ItemId, EconomyFlowChannel Channel), MutableAggregate> aggregates = new();

    public RealizedEconomyFlowLedger(int maxAggregateKeys = 20_000)
    {
        if (maxAggregateKeys <= 0) throw new ArgumentOutOfRangeException(nameof(maxAggregateKeys));
        this.maxAggregateKeys = maxAggregateKeys;
    }

    public void Observe(EconomyFlowEvent flow)
    {
        if (string.IsNullOrWhiteSpace(flow.ItemId)) throw new ArgumentException("ItemId is required", nameof(flow));
        if (!double.IsFinite(flow.Quantity) || flow.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(flow), "Quantity must be finite and positive");
        if (flow.ReferenceValue is { } value && (!double.IsFinite(value) || value < 0)) throw new ArgumentOutOfRangeException(nameof(flow), "ReferenceValue must be finite and non-negative");

        var key = (flow.ItemId, flow.Channel);
        if (!aggregates.TryGetValue(key, out var aggregate))
        {
            if (aggregates.Count >= maxAggregateKeys)
                throw new InvalidOperationException($"Realized-flow aggregate key cap {maxAggregateKeys} reached; refusing unbounded growth");
            aggregate = new MutableAggregate(flow.ItemId, flow.Channel, flow.Timestamp);
            aggregates.Add(key, aggregate);
        }

        aggregate.Add(flow);
    }

    public IReadOnlyList<EconomyFlowAggregate> Snapshot() => aggregates.Values
        .Select(x => x.Freeze())
        .OrderBy(x => x.ItemId, StringComparer.Ordinal)
        .ThenBy(x => x.Channel)
        .ToArray();

    public string ToDeterministicJson() => JsonSerializer.Serialize(
        Snapshot(),
        new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });

    private sealed class MutableAggregate(string itemId, EconomyFlowChannel channel, DateTimeOffset firstSeen)
    {
        private double inflow;
        private double outflow;
        private double inflowValue;
        private double outflowValue;
        private bool hasInflowValue;
        private bool hasOutflowValue;
        private int count;
        private DateTimeOffset first = firstSeen;
        private DateTimeOffset last = firstSeen;

        public void Add(EconomyFlowEvent flow)
        {
            if (flow.Direction == EconomyFlowDirection.Inflow)
            {
                inflow += flow.Quantity;
                if (flow.ReferenceValue is { } v) { inflowValue += v; hasInflowValue = true; }
            }
            else
            {
                outflow += flow.Quantity;
                if (flow.ReferenceValue is { } v) { outflowValue += v; hasOutflowValue = true; }
            }
            count++;
            if (flow.Timestamp < first) first = flow.Timestamp;
            if (flow.Timestamp > last) last = flow.Timestamp;
        }

        public EconomyFlowAggregate Freeze() => new(
            itemId, channel, inflow, outflow, inflow - outflow,
            hasInflowValue ? inflowValue : null,
            hasOutflowValue ? outflowValue : null,
            count, first, last);
    }
}
