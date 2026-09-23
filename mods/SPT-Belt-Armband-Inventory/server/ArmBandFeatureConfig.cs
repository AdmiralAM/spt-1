using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTBeltArmbandInventory.Server;

public sealed class BeltFeatureConfig
{
    public ArmBandRolesConfig ArmBandRoles { get; set; } = new();
    public WalletConfig Wallet { get; set; } = new();
    public EmbeddedGridsConfig EmbeddedGrids { get; set; } = new();
}

public sealed class ArmBandRolesConfig
{
    public bool Enabled { get; set; } = true;
    public ArmBandVisualPool RaidVisualPool { get; set; } = ArmBandVisualPool.Standard;
    public ArmBandGridOrientation GridOrientation { get; set; } = ArmBandGridOrientation.Vertical;
    public ArmBandRoleWeights Weights { get; set; } = new();
    public List<ArmBandRole> EnabledRoles { get; set; } = Enum.GetValues<ArmBandRole>().ToList();
    public List<string> ProtectionTemplateAllowlist { get; set; } = [];
}

public sealed class ArmBandRoleWeights
{
    public int Medical { get; set; } = 25;
    public int Ammo { get; set; } = 25;
    public int Magazine { get; set; } = 20;
    public int Technical { get; set; } = 20;
    public int Currency { get; set; } = 10;
    public int Get(ArmBandRole role) => role switch
    {
        ArmBandRole.Medical => Medical, ArmBandRole.Ammo => Ammo, ArmBandRole.Magazine => Magazine,
        ArmBandRole.Technical => Technical, ArmBandRole.Currency => Currency,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
}

public sealed class WalletConfig
{
    public List<string> PaymentPriority { get; set; } = [];
    public List<string> AutoDepositPriority { get; set; } = [];
}

public sealed class EmbeddedGridsConfig { public bool Enabled { get; set; } = true; }

[Injectable]
public sealed class ArmBandFeatureConfig
{
    public BeltFeatureConfig Value { get; }

    public ArmBandFeatureConfig(ModHelper modHelper)
    {
        string path = Path.Combine(modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "config", "config.json");
        if (!File.Exists(path))
            throw new InvalidOperationException($"B&A&HB server configuration is missing: {path}");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter() }
        };
        Value = JsonSerializer.Deserialize<BeltFeatureConfig>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException("B&A&HB server configuration is empty.");
        Validate(Value);
    }

    public static void Validate(BeltFeatureConfig config)
    {
        ArmBandRolesConfig arm = config.ArmBandRoles ?? throw new InvalidOperationException("ArmBandRoles config is required.");
        if (!Enum.IsDefined(arm.RaidVisualPool) || !Enum.IsDefined(arm.GridOrientation))
            throw new InvalidOperationException("B&A&HB ArmBand enum configuration is invalid.");
        if (arm.EnabledRoles is null || arm.EnabledRoles.Count != arm.EnabledRoles.Distinct().Count()
            || arm.EnabledRoles.Any(role => !Enum.IsDefined(role)))
            throw new InvalidOperationException("B&A&HB EnabledRoles contains duplicates or unknown roles.");
        if (Enum.GetValues<ArmBandRole>().Any(role => arm.Weights.Get(role) < 0))
            throw new InvalidOperationException("B&A&HB ArmBand role weights must be non-negative.");
        if (arm.Enabled && !arm.EnabledRoles.Any(role => arm.Weights.Get(role) > 0))
            throw new InvalidOperationException("B&A&HB requires at least one enabled ArmBand role with positive weight.");

        HashSet<string> owned = ArmBandVariantCatalog.All.Select(item => item.TemplateId).ToHashSet(StringComparer.Ordinal);
        if (arm.ProtectionTemplateAllowlist is null
            || arm.ProtectionTemplateAllowlist.Count != arm.ProtectionTemplateAllowlist.Distinct(StringComparer.Ordinal).Count()
            || arm.ProtectionTemplateAllowlist.Any(id => !owned.Contains(id)))
            throw new InvalidOperationException("B&A&HB protection allowlist must contain unique exact functional variant IDs only.");
    }

    public IReadOnlyDictionary<ArmBandRole, int> EffectiveWeights() =>
        Value.ArmBandRoles.EnabledRoles.ToDictionary(role => role, role => Value.ArmBandRoles.Weights.Get(role));
}
