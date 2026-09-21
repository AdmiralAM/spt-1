using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace TGC;

public sealed class ModItem
{
    [JsonPropertyName("itemTplToClone")] public MongoId ItemTplToClone { get; set; }
    [JsonPropertyName("PutInArmband")] public bool? PutInArmband { get; set; }
    [JsonPropertyName("putInSecureContainer")] public bool? PutInSecureContainer { get; set; }
    [JsonPropertyName("addToThisItemsFilters")] public Dictionary<string, List<MongoId>>? AddToThisItemsFilters { get; set; }
    [JsonPropertyName("addToExistingItemFilters")] public Dictionary<string, List<MongoId>>? AddToExistingItemFilters { get; set; }
}

public sealed class ModPresets
{
    [JsonPropertyName("ItemPresets")] public required Dictionary<MongoId, Preset> Items { get; set; }
}

public sealed class ModClothing
{
    [JsonPropertyName("clone")] public MongoId Clone { get; set; }
    [JsonPropertyName("customization")] public required CustomizationWrapper Customization { get; set; }
    [JsonPropertyName("locales")] public required Dictionary<string, LocaleDetails> Locales { get; set; }
}

public sealed class CustomizationWrapper
{
    [JsonPropertyName("_props")] public required CustomizationProperties Properties { get; set; }
}
