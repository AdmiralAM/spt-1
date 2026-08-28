using System.Diagnostics;
using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

public sealed record ChannelObservationCoverage
{
    public required AcquisitionChannel Channel { get; init; }
    public required string State { get; init; }
    public required int ObservedSourceCount { get; init; }
    public string? Diagnostic { get; init; }
}

public sealed record FinalDbSourceObservation
{
    public required IReadOnlyList<AcquisitionSourceEvidence> Sources { get; init; }
    public required IReadOnlyList<AcquisitionCostPath> CostPaths { get; init; }
    public required IReadOnlyList<ChannelObservationCoverage> ChannelCoverage { get; init; }
    public required EffectiveAcquisitionGraphResult AcquisitionGraph { get; init; }
    public required double StartupMilliseconds { get; init; }
}

[Injectable]
public sealed class FinalDbSourceObservationService(
    TemplateTable templates,
    TradersTable traders,
    HideoutTable hideout)
{
    private static readonly HashSet<string> CurrencyTemplates = new(StringComparer.Ordinal)
    {
        "5449016a4bdc2d6f028b456f", // RUB
        "5696686a4bdc2da3298b456a", // USD
        "569668774bdc2da2298b4568", // EUR
    };

    public FinalDbSourceObservation Build(VanillaBaselineSnapshot baseline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        var stopwatch = Stopwatch.StartNew();
        var sources = new List<AcquisitionSourceEvidence>();
        var paths = new List<AcquisitionCostPath>();
        var handbook = templates.Handbook.Items
            .Where(x => x.Price is > 0)
            .GroupBy(x => x.Id.ToString(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Price!.Value, StringComparer.Ordinal);

        AddCurrencyReferencePaths(paths, handbook);
        ScanTraderSources(sources, paths, cancellationToken);
        ScanQuestSources(sources, baseline, cancellationToken);
        var craftCount = ScanCraftSources(sources, paths, cancellationToken);

        var graph = EffectiveAcquisitionGraph.Resolve(paths);
        stopwatch.Stop();

        var byChannel = sources.GroupBy(x => x.Channel).ToDictionary(g => g.Key, g => g.Count());
        var coverage = Enum.GetValues<AcquisitionChannel>().Select(channel => channel switch
        {
            AcquisitionChannel.Flea => new ChannelObservationCoverage
            {
                Channel = channel,
                State = "ReferenceOnly",
                ObservedSourceCount = 0,
                Diagnostic = $"TemplateTable.Prices contains {templates.Prices.Count(x => x.Value > 0)} positive final price references, but flea eligibility/ownership is not inferred as an acquisition source.",
            },
            AcquisitionChannel.WorldLoot => new ChannelObservationCoverage
            {
                Channel = channel,
                State = "UnknownNoMaintainedAdapter",
                ObservedSourceCount = 0,
                Diagnostic = "World-loot availability remains Unknown until a maintained final-location/loot adapter is registered; zero sources is not asserted.",
            },
            AcquisitionChannel.Other => new ChannelObservationCoverage
            {
                Channel = channel,
                State = "ExplicitAdapters",
                ObservedSourceCount = byChannel.GetValueOrDefault(channel),
                Diagnostic = "External adapter evidence is merged separately and retains adapter provenance.",
            },
            AcquisitionChannel.Craft => new ChannelObservationCoverage
            {
                Channel = channel,
                State = "ObservedFinalDb",
                ObservedSourceCount = byChannel.GetValueOrDefault(channel),
                Diagnostic = $"Observed {craftCount} final hideout production recipes using bounded startup-only accessors.",
            },
            _ => new ChannelObservationCoverage
            {
                Channel = channel,
                State = "ObservedFinalDb",
                ObservedSourceCount = byChannel.GetValueOrDefault(channel),
            },
        }).OrderBy(x => x.Channel).ToArray();

        return new FinalDbSourceObservation
        {
            Sources = sources.OrderBy(x => x.ItemTemplateId, StringComparer.Ordinal).ThenBy(x => x.Channel).ThenBy(x => x.SourceId, StringComparer.Ordinal).ToArray(),
            CostPaths = paths.OrderBy(x => x.ItemTemplateId, StringComparer.Ordinal).ThenBy(x => x.PathId, StringComparer.Ordinal).ToArray(),
            ChannelCoverage = coverage,
            AcquisitionGraph = graph,
            StartupMilliseconds = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
        };
    }

    private void ScanTraderSources(List<AcquisitionSourceEvidence> sources, List<AcquisitionCostPath> paths, CancellationToken cancellationToken)
    {
        foreach (var (traderIdRaw, trader) in traders.OrderBy(x => x.Key.ToString(), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var traderId = traderIdRaw.ToString();
            var assort = trader.Assort;
            foreach (var root in assort.Items.Where(x => string.Equals(x.ParentId, "hideout", StringComparison.OrdinalIgnoreCase)))
            {
                var templateId = root.Template.ToString();
                if (string.IsNullOrWhiteSpace(templateId)) continue;
                var offerId = root.Id.ToString();
                assort.BarterScheme.TryGetValue(root.Id, out var alternatives);
                assort.LoyalLevelItems.TryGetValue(root.Id, out var loyaltyRaw);
                var loyalty = ConvertToPositiveInt(loyaltyRaw);
                var allCurrency = alternatives is { Count: > 0 } && alternatives.All(scheme => scheme is { Count: > 0 } && scheme.All(r => CurrencyTemplates.Contains(r.Template.ToString())));
                var channel = allCurrency ? AcquisitionChannel.TraderPurchase : AcquisitionChannel.TraderBarter;
                sources.Add(new AcquisitionSourceEvidence
                {
                    ItemTemplateId = templateId,
                    SourceId = $"trader:{traderId}:{offerId}",
                    Channel = channel,
                    Renewable = true,
                    EarliestProgressionLevel = loyalty,
                    ProvenanceClass = "FinalDbObserved",
                });

                if (alternatives is null) continue;
                var alternativeIndex = 0;
                foreach (var scheme in alternatives)
                {
                    if (scheme is null || scheme.Count == 0) { alternativeIndex++; continue; }
                    var dependencies = new List<AcquisitionCostDependency>();
                    var valid = true;
                    foreach (var requirement in scheme)
                    {
                        var dependencyId = requirement.Template.ToString();
                        var count = ReadPositiveDouble(requirement, "Count") ?? 1d;
                        if (string.IsNullOrWhiteSpace(dependencyId) || count <= 0 || !double.IsFinite(count)) { valid = false; break; }
                        dependencies.Add(new AcquisitionCostDependency(dependencyId, count));
                    }
                    if (valid)
                    {
                        paths.Add(new AcquisitionCostPath
                        {
                            ItemTemplateId = templateId,
                            PathId = $"trader:{traderId}:{offerId}:alt:{alternativeIndex}",
                            Channel = channel,
                            Dependencies = dependencies,
                            EarliestProgressionLevel = loyalty,
                        });
                    }
                    alternativeIndex++;
                }
            }
        }
    }

    private void ScanQuestSources(List<AcquisitionSourceEvidence> sources, VanillaBaselineSnapshot baseline, CancellationToken cancellationToken)
    {
        foreach (var (questIdRaw, quest) in templates.Quests.OrderBy(x => x.Key.ToString(), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var questId = questIdRaw.ToString();
            if (quest.Rewards is null) continue;
            var requiredLevel = ExtractRequiredLevel(quest.Conditions.AvailableForStart);
            var provenance = baseline.QuestIds.Contains(questId) ? "PristineObserved" : "ModAdded";
            var channel = quest.Restartable ? AcquisitionChannel.RepeatableQuestReward : AcquisitionChannel.QuestReward;
            var distinctTemplates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rewardGroup in quest.Rewards)
            {
                if (!string.Equals(rewardGroup.Key, "Success", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var reward in rewardGroup.Value)
                {
                    if (reward.Type != RewardType.Item || reward.Items is null) continue;
                    foreach (var item in reward.Items)
                    {
                        var templateId = item.Template.ToString();
                        if (!string.IsNullOrWhiteSpace(templateId)) distinctTemplates.Add(templateId);
                    }
                }
            }
            foreach (var templateId in distinctTemplates)
            {
                sources.Add(new AcquisitionSourceEvidence
                {
                    ItemTemplateId = templateId,
                    SourceId = $"quest:{questId}",
                    Channel = channel,
                    Renewable = quest.Restartable,
                    EarliestProgressionLevel = requiredLevel,
                    ProvenanceClass = provenance,
                });
            }
        }
    }

    private int ScanCraftSources(List<AcquisitionSourceEvidence> sources, List<AcquisitionCostPath> paths, CancellationToken cancellationToken)
    {
        var recipes = hideout.Production.Recipes ?? [];
        var count = 0;
        foreach (var recipe in recipes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;
            var outputId = ReadStringLike(recipe, "EndProduct");
            var recipeId = ReadStringLike(recipe, "Id") ?? ReadStringLike(recipe, "_id") ?? $"index-{count}";
            if (string.IsNullOrWhiteSpace(outputId)) continue;

            var requiredLevel = 1;
            var dependencies = new List<AcquisitionCostDependency>();
            foreach (var requirement in recipe.Requirements ?? [])
            {
                var type = ReadStringLike(requirement, "Type");
                if (string.Equals(type, "Area", StringComparison.OrdinalIgnoreCase))
                {
                    requiredLevel = Math.Max(requiredLevel, ConvertToPositiveInt(ReadObject(requirement, "RequiredLevel")) ?? 1);
                    continue;
                }
                if (string.Equals(type, "Tool", StringComparison.OrdinalIgnoreCase)) continue;
                var dependencyId = ReadStringLike(requirement, "TemplateId");
                if (string.IsNullOrWhiteSpace(dependencyId)) continue;
                var dependencyCount = ReadPositiveDouble(requirement, "Count") ?? 1d;
                dependencies.Add(new AcquisitionCostDependency(dependencyId, dependencyCount));
            }

            sources.Add(new AcquisitionSourceEvidence
            {
                ItemTemplateId = outputId,
                SourceId = $"craft:{recipeId}",
                Channel = AcquisitionChannel.Craft,
                Renewable = true,
                EarliestProgressionLevel = requiredLevel,
                ProvenanceClass = "FinalDbObserved",
            });
            paths.Add(new AcquisitionCostPath
            {
                ItemTemplateId = outputId,
                PathId = $"craft:{recipeId}",
                Channel = AcquisitionChannel.Craft,
                Dependencies = dependencies,
                EarliestProgressionLevel = requiredLevel,
                ProductionTimeSeconds = ReadPositiveDouble(recipe, "ProductionTime"),
            });
        }
        return count;
    }

    private static void AddCurrencyReferencePaths(List<AcquisitionCostPath> paths, IReadOnlyDictionary<string, double> handbook)
    {
        foreach (var currencyId in CurrencyTemplates.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!handbook.TryGetValue(currencyId, out var price) || !double.IsFinite(price) || price <= 0) continue;
            paths.Add(new AcquisitionCostPath
            {
                ItemTemplateId = currencyId,
                PathId = $"currency-reference:{currencyId}",
                Channel = AcquisitionChannel.TraderPurchase,
                FixedReferenceCost = price,
                Dependencies = Array.Empty<AcquisitionCostDependency>(),
                EarliestProgressionLevel = 1,
            });
        }
    }

    private static int ExtractRequiredLevel(IEnumerable<QuestCondition>? conditions)
    {
        if (conditions is null) return 1;
        return conditions.Where(x => string.Equals(x.ConditionType, "Level", StringComparison.OrdinalIgnoreCase) && x.Value.HasValue)
            .Select(x => Math.Max(1, (int)Math.Ceiling(x.Value!.Value))).DefaultIfEmpty(1).Max();
    }

    private static object? ReadObject(object instance, string property) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(instance);

    private static string? ReadStringLike(object instance, string property)
    {
        var value = ReadObject(instance, property);
        var text = value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static double? ReadPositiveDouble(object instance, string property)
    {
        var value = ReadObject(instance, property);
        if (value is null) return null;
        try
        {
            var number = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            return double.IsFinite(number) && number > 0 ? number : null;
        }
        catch { return null; }
    }

    private static int? ConvertToPositiveInt(object? value)
    {
        if (value is null) return null;
        try
        {
            var number = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return number >= 1 ? number : null;
        }
        catch { return null; }
    }
}
