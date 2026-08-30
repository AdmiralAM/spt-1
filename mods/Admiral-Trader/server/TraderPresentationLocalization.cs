using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace AdmiralTrader.Server;

public static class TraderPresentationCopy
{
    public const string DescriptionEn = "Former naval logistics officer turned independent procurement broker. Handles expedition access, specialist field logistics and capability contracts for operators who prove they can use them.";
    public const string DescriptionRu = "Бывший офицер флотской логистики, ставший независимым снабженцем. Организует доступ, специализированное полевое обеспечение и контракты на особые возможности для тех, кто доказал, что умеет ими пользоваться.";
}

[Injectable(TypePriority = OnLoadOrder.Preload + 3), UsedImplicitly]
public sealed class AdmiralTraderPresentationLocalization(
    TradersTable tradersTable,
    LocaleTable localesTable) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        MongoId traderId = new(RuntimeIdentity.TraderId);
        if (!tradersTable.ContainsKey(traderId))
            return Task.CompletedTask;

        foreach (var (localeCode, localeKvP) in localesTable.Global)
        {
            localeKvP.AddTransformer(lazyLoadedLocaleData =>
            {
                if (lazyLoadedLocaleData is null)
                    return lazyLoadedLocaleData;

                bool isRussian = localeCode.Equals("ru", StringComparison.OrdinalIgnoreCase);
                lazyLoadedLocaleData[$"{RuntimeIdentity.TraderId} Description"] =
                    isRussian ? TraderPresentationCopy.DescriptionRu : TraderPresentationCopy.DescriptionEn;
                return lazyLoadedLocaleData;
            });
        }

        return Task.CompletedTask;
    }
}
