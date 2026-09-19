using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTFoldablesExtended.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.foldables-extended.server";
    public string Name { get; init; } = "Admiral Foldables Extended Server";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; } = ["ozen-m (Foldables API and behavior)"];
    public SemanticVersioning.Version Version { get; init; } = new(0, 1, 0);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new()
    {
        ["com.ozen.foldables"] = new(">=1.1.1")
    };
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostLoad + 100100)]
public sealed class FoldablesExtendedRegistration(
    TemplateTable templateTable,
    ItemHelper itemHelper,
    ISptLogger<FoldablesExtendedRegistration> logger) : IOnLoad
{
    private static readonly MongoId ArmorBase = new("5448e54d4bdc2dcc718b4568");
    private static readonly MongoId VestBase = new("5448e5284bdc2dcb718b4567");
    private static readonly MongoId HeadwearBase = new("5a341c4086f77401f2541505");
    private static readonly MongoId FaceCoverBase = new("5a341c4686f77469e155819e");
    private static readonly MongoId PosterBase = new("6759673c76e93d8eb20b2080");

    private static readonly HashSet<string> PosterTemplates =
    [
        "664a5775f3d3570fba06be64",
        "664b69c5a082271bc46c4e11",
        "664b69e8e1238e506d3630af",
        "664b69f3a082271bc46c4e13"
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int armor = 0;
        int armoredRigs = 0;
        int headwear = 0;
        int posters = 0;

        foreach (TemplateItem template in templateTable.Items.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TemplateItemProperties? properties = template.Properties;
            if (properties?.Width is not int width || properties.Height is not int height || width <= 0 || height <= 0)
            {
                continue;
            }

            string id = template.Id.ToString();
            if (PosterTemplates.Contains(id) || itemHelper.IsOfBaseclass(template.Id, PosterBase))
            {
                Apply(template, FoldedGeometry.ForPoster(width, height), 1d);
                posters++;
                continue;
            }

            if (itemHelper.IsOfBaseclass(template.Id, ArmorBase))
            {
                Apply(template, FoldedGeometry.ForArmor(width, height), FoldingTime(width, height));
                armor++;
                continue;
            }

            if (itemHelper.IsOfBaseclass(template.Id, VestBase) && properties.Slots?.Any() == true)
            {
                Apply(template, FoldedGeometry.ForArmor(width, height), FoldingTime(width, height));
                armoredRigs++;
                continue;
            }

            if (itemHelper.IsOfBaseclass(template.Id, FaceCoverBase)
                && width * height > 1)
            {
                Apply(template, FoldedGeometry.ForFaceCover(width, height), 1d);
                headwear++;
                continue;
            }

            if (itemHelper.IsOfBaseclass(template.Id, HeadwearBase)
                && width * height == 4)
            {
                Apply(template, FoldedGeometry.ForHelmet(width, height), 1d);
                headwear++;
            }
        }

        if (posters < PosterTemplates.Count)
        {
            throw new InvalidDataException($"Foldables Extended expected at least {PosterTemplates.Count} poster templates but resolved {posters}; no partial poster contract is allowed.");
        }

        logger.Success($"Foldables Extended registered {armor} body armors, {armoredRigs} armored rigs, {headwear} four-cell headwear items and {posters} poster packs.");
        return Task.CompletedTask;
    }

    private static void Apply(TemplateItem template, FoldedSize folded, double foldingTime)
    {
        TemplateItemProperties properties = template.Properties
            ?? throw new InvalidDataException($"Template {template.Id} has no properties.");
        int width = properties.Width.GetValueOrDefault();
        int height = properties.Height.GetValueOrDefault();
        if (folded.Width <= 0 || folded.Height <= 0 || folded.Width * folded.Height >= width * height)
        {
            throw new InvalidDataException($"Folded geometry {folded.Width}x{folded.Height} does not reduce template {template.Id} ({width}x{height}).");
        }

        properties.Foldable = true;
        properties.SizeReduceRight = width - folded.Width;
        properties.ExtensionData ??= [];
        properties.ExtensionData["SizeReduceDown"] = height - folded.Height;
        properties.ExtensionData["FoldingTime"] = foldingTime;
    }

    private static double FoldingTime(int width, int height) => Math.Clamp(Math.Round(width * height / 4d, 2), 1d, 5d);
}
