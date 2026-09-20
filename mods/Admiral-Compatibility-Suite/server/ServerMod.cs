using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace AdmiralCompatibilitySuite.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.compatibility-suite.server";
    public string Name { get; init; } = "Admiral Compatibility Suite Server";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(0, 1, 0);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.Preload - 1)]
public sealed class ExternalCompatibilityClaims(
    TemplateTable templateTable,
    ISptLogger<ExternalCompatibilityClaims> logger) : IOnLoad
{
    private const int ContractVersion = 1;
    private static readonly object OwnerToken = new();
    private static readonly MongoId PackNStrapParent = new("680fd1dae5044e670a092e16");
    private static readonly MongoId[] TgcBelts =
    [
        new("672e2e75a16c1d2034c384cf"), new("672e2e750ea81b3b93b943ac"),
        new("672e2e75a26efb53bc703d45"), new("672e2e75bfad327651a1a19b"),
        new("672e2e751ff683e9432cb5af")
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Type? api = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("SPTBeltArmbandInventory.ExternalCompatibilityApi", false))
            .SingleOrDefault(type => type != null);
        if (api is null) return Task.CompletedTask;

        bool tgcPresent = TgcBelts.All(templateTable.Items.ContainsKey);
        bool packPresent = templateTable.Items.ContainsKey(PackNStrapParent);
        if (tgcPresent) Claim(api, "TryClaimTgc300", "TGC 3.0.0");
        if (packPresent) Claim(api, "TryClaimPackNStrap211", "Pack 'n' Strap 2.1.1");
        return Task.CompletedTask;
    }

    private void Claim(Type api, string methodName, string product)
    {
        MethodInfo? method = api.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method?.Invoke(null, [ContractVersion, OwnerToken]) is true)
            logger.Success($"Admiral Compatibility Suite claimed Belt {product} compatibility ownership.");
        else
            logger.Warning($"Admiral Compatibility Suite could not claim Belt {product} compatibility; integration remains disabled.");
    }
}
