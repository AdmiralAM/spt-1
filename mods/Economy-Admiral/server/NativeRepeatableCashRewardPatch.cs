using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators.RepeatableQuestGeneration;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace SPTEconomy;

[Injectable]
public sealed class NativeRepeatableCashRewardPatch(
    EconomyConfig config,
    NativeRepeatableQuestBaselineService baselineService) : AbstractPatch
{
    private static EconomyConfig runtimeConfig = default!;
    private static NativeRepeatableQuestBaselineService runtimeBaselineService = default!;

    public NativeRepeatableCashRewardPatch : this(config, baselineService)
    {
        runtimeConfig = config;
        runtimeBaselineService = baselineService;
    }

    protected override MethodBase GetTargetMethod() =>
        typeof(RepeatableQuestRewardGenerator).GetMethod(nameof(RepeatableQuestRewardGenerator.GenerateReward))
        ?? throw new MissingMethodException(typeof(RepeatableQuestRewardGenerator).FullName, nameof(RepeatableQuestRewardGenerator.GenerateReward));

    [PatchPostfix]
    public static void Postfix(
        int pmcLevel,
        RepeatableQuestConfig repeatableConfig,
        Dictionary<string, List<Reward>>? __result)
    {
        if (__result is null
            || runtimeConfig.Mode != EconomyMode.Enforce
            || !runtimeConfig.EnableQuestEconomyCluster
            || !runtimeConfig.EnableRestartableQuestPressure
            || !runtimeConfig.EnableItemRewardStackNormalization)
            return;

        var baseline = runtimeBaselineService.GetSnapshot();
        var pristine = baseline.Values.FirstOrDefault(row =>
            string.Equals(row.Name, repeatableConfig.Name, StringComparison.Ordinal));
        if (pristine is null
            || pristine.Roubles.Count == 0
            || pristine.Roubles.Count != repeatableConfig.RewardScaling.Roubles.Count
            || repeatableConfig.RewardScaling.Levels.Count != repeatableConfig.RewardScaling.Roubles.Count)
            return;

        var currentNominal = Interpolate(pmcLevel, repeatableConfig.RewardScaling.Levels, repeatableConfig.RewardScaling.Roubles);
        var pristineNominal = Interpolate(pmcLevel, repeatableConfig.RewardScaling.Levels, pristine.Roubles);
        if (!double.IsFinite(currentNominal) || currentNominal <= 0 || !double.IsFinite(pristineNominal) || pristineNominal < 0)
            return;

        var capMultiple = PlayableQuestRewardCaps.Resolve(runtimeConfig).RestartableItemBudgetMultiple;
        var targetNominal = Math.Min(currentNominal, pristineNominal * capMultiple);
        if (!NativeRepeatableQuestPressureCore.NeedsMutation(currentNominal, targetNominal))
            return;

        if (!__result.TryGetValue("Success", out var successRewards))
            return;

        var ratio = targetNominal / currentNominal;
        foreach (var reward in successRewards)
        {
            if (reward.Type != RewardType.Item || reward.Items is null || reward.Items.Count != 1)
                continue;

            var item = reward.Items[0];
            if (item.Template != Money.ROUBLES && item.Template != Money.EUROS)
                continue;
            if (item.Upd?.StackObjectsCount is not double stack || !double.IsFinite(stack) || stack <= 0)
                continue;

            var target = Math.Max(1d, Math.Floor(stack * ratio));
            if (target >= stack)
                continue;

            item.Upd.StackObjectsCount = target;
            reward.Value = target;
        }
    }

    internal static double Interpolate(int level, IReadOnlyList<double> levels, IReadOnlyList<double> values)
    {
        if (levels.Count == 0 || levels.Count != values.Count)
            throw new ArgumentException("Repeatable reward interpolation requires equal non-empty level/value arrays.");
        if (level <= levels[0])
            return values[0];
        for (var index = 1; index < levels.Count; index++)
        {
            if (level > levels[index])
                continue;
            var span = levels[index] - levels[index - 1];
            if (span <= 0)
                throw new InvalidOperationException("Repeatable reward levels must be strictly increasing.");
            var fraction = (level - levels[index - 1]) / span;
            return values[index - 1] + ((values[index] - values[index - 1]) * fraction);
        }
        return values[^1];
    }
}

[Injectable(TypePriority = OnLoadOrder.Preload + 10)]
public sealed class EconomyRuntimePatchInstaller(IEnumerable<IRuntimePatch> patches) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var patch in patches.Where(patch => patch.IsYourPatch))
            patch.Enable();
        return Task.CompletedTask;
    }
}
