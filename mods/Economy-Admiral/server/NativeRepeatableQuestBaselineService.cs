using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace SPTEconomy;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Watermark + 2)]
public sealed class NativeRepeatableQuestBaselineService(
    QuestConfig questConfig,
    EconomyRuntimeConfigService runtimeConfigService,
    ISptLogger<NativeRepeatableQuestBaselineService> logger) : IOnLoad
{
    private IReadOnlyDictionary<string, NativeRepeatableRewardBaseline>? snapshot;

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        if (config.Mode == EconomyMode.Off)
            return;

        snapshot = questConfig.RepeatableQuests
            .Select((repeatable, index) => Capture(repeatable, index))
            .ToDictionary(row => row.Key, StringComparer.Ordinal);

        logger.Info($"[Economy Admiral] pristine native repeatable reward scaling captured: groups={snapshot.Count}, priority={OnLoadOrder.Watermark + 2}");
    }

    public IReadOnlyDictionary<string, NativeRepeatableRewardBaseline> GetSnapshot() => snapshot
        ?? throw new InvalidOperationException("Economy Admiral pristine native repeatable reward scaling was not captured.");

    public static string Key(RepeatableQuestConfig repeatable, int index) =>
        $"{index}:{repeatable.Name}:{repeatable.Side}";

    private static NativeRepeatableRewardBaseline Capture(RepeatableQuestConfig repeatable, int index)
    {
        var scaling = repeatable.RewardScaling;
        return new NativeRepeatableRewardBaseline(
            Key(repeatable, index),
            repeatable.Name,
            scaling.Experience.ToArray(),
            scaling.Roubles.ToArray(),
            scaling.GpCoins.ToArray(),
            scaling.Items.ToArray(),
            scaling.Reputation.ToArray());
    }
}
