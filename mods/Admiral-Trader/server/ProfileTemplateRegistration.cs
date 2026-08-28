using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace AdmiralTrader.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 2), UsedImplicitly]
public sealed class AdmiralProfileTemplateRegistration(
    TemplateTable templateTable,
    ISptLogger<AdmiralProfileTemplateRegistration> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int updatedSides = 0;
        foreach (var profileTemplate in templateTable.Profiles.Values)
        {
            updatedSides += PinStartingStanding(profileTemplate.Usec);
            updatedSides += PinStartingStanding(profileTemplate.Bear);
        }

        if (updatedSides == 0)
            throw new InvalidOperationException("Admiral Trader could not pin starting standing: no profile-side trader templates were available");

        logger.Success($"Pinned Admiral Trader starting standing to 0 across {updatedSides} profile-side templates");
        return Task.CompletedTask;
    }

    private static int PinStartingStanding(SPTarkov.Server.Core.Models.Eft.Common.Tables.TemplateSide? side)
    {
        if (side?.Trader is null)
            return 0;

        side.Trader.InitialStanding ??= new Dictionary<string, double?>();
        side.Trader.InitialStanding[RuntimeIdentity.TraderId] = 0d;
        return 1;
    }
}
