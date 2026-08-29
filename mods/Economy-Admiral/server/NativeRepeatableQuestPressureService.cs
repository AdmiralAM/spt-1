using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace SPTEconomy;

[Injectable]
public sealed class NativeRepeatableQuestPressureService(
    QuestConfig questConfig,
    NativeRepeatableQuestBaselineService baselineService,
    ISptLogger<NativeRepeatableQuestPressureService> logger)
{
    public NativeRepeatablePressureResult Apply(EconomyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var enabled = config.EnableQuestEconomyCluster && config.EnableRestartableQuestPressure;
        if (!enabled)
            return Empty(enabled: false);

        var baseline = baselineService.GetSnapshot();
        var caps = PlayableQuestRewardCaps.Resolve(config);
        var proposals = new List<NativeRepeatableMutation>();
        var blocked = 0;

        for (var repeatableIndex = 0; repeatableIndex < questConfig.RepeatableQuests.Count; repeatableIndex++)
        {
            var repeatable = questConfig.RepeatableQuests[repeatableIndex];
            var key = NativeRepeatableQuestBaselineService.Key(repeatable, repeatableIndex);
            if (!baseline.TryGetValue(key, out var pristine))
            {
                blocked++;
                continue;
            }

            if (config.EnableQuestXpPressure)
            {
                blocked += PlanDimension(
                    proposals,
                    key,
                    repeatable.Name,
                    "Experience",
                    pristine.Experience,
                    repeatable.RewardScaling.Experience,
                    caps.RestartableXpMultiple,
                    config.Mode == EconomyMode.Enforce);
            }

            if (config.EnableQuestStandingPressure)
            {
                blocked += PlanDimension(
                    proposals,
                    key,
                    repeatable.Name,
                    "TraderStanding",
                    pristine.Reputation,
                    repeatable.RewardScaling.Reputation,
                    NativeRepeatableQuestPressureCore.ResolveStandingMultiple(caps),
                    config.Mode == EconomyMode.Enforce);
            }
        }

        var result = new NativeRepeatablePressureResult
        {
            Enabled = true,
            PlannedMutationCount = proposals.Count,
            MutationCount = proposals.Count(entry => entry.Applied),
            BlockedDimensionCount = blocked,
            Mutations = proposals,
        };

        if (config.Mode == EconomyMode.Enforce)
            logger.Warning($"[Economy Admiral] native repeatable pressure applied: planned={result.PlannedMutationCount}, mutations={result.MutationCount}, blockedDimensions={result.BlockedDimensionCount}");
        else
            logger.Info($"[Economy Admiral] native repeatable pressure preview: planned={result.PlannedMutationCount}, mutations=0, blockedDimensions={result.BlockedDimensionCount}");

        return result;
    }

    private static int PlanDimension(
        List<NativeRepeatableMutation> proposals,
        string key,
        string name,
        string dimension,
        IReadOnlyList<double> pristine,
        IList<double> current,
        double multiple,
        bool apply)
    {
        if (!NativeRepeatableQuestPressureCore.Compatible(pristine, current))
            return 1;

        for (var index = 0; index < current.Count; index++)
        {
            var before = current[index];
            var target = NativeRepeatableQuestPressureCore.Cap(before, pristine[index], multiple);
            if (!NativeRepeatableQuestPressureCore.NeedsMutation(before, target))
                continue;

            if (apply)
                current[index] = target;

            proposals.Add(new NativeRepeatableMutation(
                key,
                name,
                dimension,
                index,
                before,
                target,
                apply));
        }

        return 0;
    }

    private static NativeRepeatablePressureResult Empty(bool enabled) => new()
    {
        Enabled = enabled,
        PlannedMutationCount = 0,
        MutationCount = 0,
        BlockedDimensionCount = 0,
        Mutations = Array.Empty<NativeRepeatableMutation>(),
    };
}
