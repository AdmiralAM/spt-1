using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;

namespace AdmiralTrader.Server;

public static class RuntimeIdentity
{
    public const string ModGuid = "com.admiralam.spt.admiraltrader";
    public const string TraderId = "d5c27bb3169f8dfbc13f6b69";
    public const string TraderName = "Admiral";
    public const string TraderNameRu = "Адмирал";
}

[Injectable(TypePriority = OnLoadOrder.Preload + 1), UsedImplicitly]
public sealed class AdmiralTraderRuntimeFoundation(
    ModHelper modHelper,
    ISptLogger<AdmiralTraderRuntimeFoundation> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        string manifestPath = Path.Combine(modPath, "manifests", "campaign-manifest.json");
        ValidateCampaignManifest(manifestPath);

        logger.Success($"Admiral Trader runtime foundation loaded; trader id {RuntimeIdentity.TraderId}; registration intentionally deferred");
        return Task.CompletedTask;
    }

    internal static void ValidateCampaignManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Admiral Trader campaign manifest is missing", manifestPath);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;
        JsonElement product = root.GetProperty("product");

        string? modName = product.GetProperty("modName").GetString();
        string? modGuid = product.GetProperty("modGuid").GetString();
        string? traderId = product.GetProperty("traderId").GetString();
        string? traderName = product.GetProperty("traderWorkingName").GetString();
        string? traderNameRu = product.GetProperty("traderWorkingNameRu").GetString();

        if (!string.Equals(modName, "Admiral Trader", StringComparison.Ordinal))
            throw new InvalidDataException($"Unexpected Admiral Trader product name: {modName}");
        if (!string.Equals(modGuid, RuntimeIdentity.ModGuid, StringComparison.Ordinal))
            throw new InvalidDataException($"Manifest modGuid mismatch: {modGuid}");
        if (!string.Equals(traderId, RuntimeIdentity.TraderId, StringComparison.Ordinal))
            throw new InvalidDataException($"Manifest traderId mismatch: {traderId}");
        if (!string.Equals(traderName, RuntimeIdentity.TraderName, StringComparison.Ordinal))
            throw new InvalidDataException($"Manifest trader name mismatch: {traderName}");
        if (!string.Equals(traderNameRu, RuntimeIdentity.TraderNameRu, StringComparison.Ordinal))
            throw new InvalidDataException($"Manifest Russian trader name mismatch: {traderNameRu}");

        if (traderId is null || traderId.Length != 24 || !traderId.All(Uri.IsHexDigit))
            throw new InvalidDataException($"Trader id is not a 24-hex MongoId-compatible value: {traderId}");
    }
}
