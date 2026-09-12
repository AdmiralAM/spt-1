using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using SPTBeltArmbandInventory.Server;

namespace SPTBeltArmbandInventory.Tests;

internal static class ProfileCleanupRegression
{
    internal static void Run()
    {
        AssertManifestContractParity();

        const string vanillaTpl = "5448bf274bdc2dfc2f8b456a";
        var availableTemplatesWithoutMod = new HashSet<string>(StringComparer.Ordinal)
        {
            vanillaTpl
        };

        JsonObject profile = new()
        {
            ["characters"] = new JsonObject
            {
                ["pmc"] = new JsonObject
                {
                    ["Inventory"] = new JsonObject
                    {
                        ["items"] = new JsonArray
                        {
                            Item("vanilla-stash", vanillaTpl),
                            Item("bahb-equipped", RuntimeIdentity.DedicatedMagazineBeltItemId, "equipment-root", RuntimeIdentity.DedicatedBeltWireSlotId),
                            Item("belt-child", vanillaTpl, "bahb-equipped", "Grid")
                        }
                    }
                }
            },
            ["mail"] = new JsonArray
            {
                new JsonObject
                {
                    ["messageId"] = "mail-1",
                    ["items"] = new JsonArray
                    {
                        Item("bahb-mail", RuntimeIdentity.CandidateItemId),
                        Item("mail-child", vanillaTpl, "bahb-mail", "Grid")
                    }
                }
            },
            ["insurance"] = new JsonArray
            {
                new JsonObject
                {
                    ["itemId"] = "bahb-equipped",
                    ["traderId"] = "54cb50c76803fa8b248b4571"
                }
            },
            ["weaponBuilds"] = new JsonArray
            {
                new JsonObject
                {
                    ["_id"] = "build-ref",
                    ["itemId"] = "bahb-mail"
                }
            }
        };

        string[] modTemplatesBefore = FindTemplateIds(profile)
            .Where(PersistentIdentityManifest.IsOwnedTemplate)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert(modTemplatesBefore.Length == 2, "synthetic invalid profile contains B&A&HB templates");
        Assert(modTemplatesBefore.All(x => !availableTemplatesWithoutMod.Contains(x)),
            "B&A&HB templates are absent from simulated items DB before recovery");

        ProfileCleanupPolicy.CleanupResult result = ProfileCleanupPolicy.Clean(profile);

        Assert(result.RemovedItems == 2, "cleanup removes both direct B&A&HB items");
        Assert(result.RemovedReferences == 4, "cleanup removes descendants plus insurance/build references");
        Assert(result.Locations.Any(x => x.Contains("Inventory", StringComparison.Ordinal)), "cleanup reports inventory location");
        Assert(result.Locations.Any(x => x.Contains("mail", StringComparison.OrdinalIgnoreCase)), "cleanup reports mail location");
        Assert(result.Locations.Any(x => x.Contains("insurance", StringComparison.OrdinalIgnoreCase)), "cleanup reports insurance location");
        Assert(result.Locations.Any(x => x.Contains("weaponBuilds", StringComparison.Ordinal)), "cleanup reports build location");
        Assert(!FindTemplateIds(profile).Any(PersistentIdentityManifest.IsOwnedTemplate),
            "no B&A&HB template remains after recovery");
        Assert(FindObjectIds(profile).Contains("vanilla-stash", StringComparer.Ordinal),
            "unrelated vanilla stash item survives cleanup");
        Assert(!FindObjectIds(profile).Contains("belt-child", StringComparer.Ordinal),
            "descendant of removed wearable is removed");

        ProfileCleanupPolicy.CleanupResult second = ProfileCleanupPolicy.Clean(profile);
        Assert(second.RemovedItems == 0 && second.RemovedReferences == 0,
            "cleanup is idempotent after recovery");
    }

    private static void AssertManifestContractParity()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "persistent-identities.json");
        JsonObject contract = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        Assert(ReadArray(contract, "templateIds").SequenceEqual(PersistentIdentityManifest.TemplateIds),
            "machine-readable template IDs match server manifest");
        Assert(ReadArray(contract, "parentIds").SequenceEqual(PersistentIdentityManifest.ParentIds),
            "machine-readable parent IDs match server manifest");
        Assert(ReadArray(contract, "slotIds").SequenceEqual(PersistentIdentityManifest.SlotIds),
            "machine-readable slot IDs match server manifest");
        Assert(ReadArray(contract, "slotMongoIds").SequenceEqual(PersistentIdentityManifest.SlotMongoIds),
            "machine-readable slot Mongo IDs match server manifest");
        Assert(ReadArray(contract, "gridIds").SequenceEqual(PersistentIdentityManifest.GridIds),
            "machine-readable grid IDs match server manifest");
        Assert(ReadArray(contract, "assortIds").SequenceEqual(PersistentIdentityManifest.AssortIds),
            "machine-readable assort IDs match server manifest");
        Assert(ReadArray(contract, "slotSemanticIds").SequenceEqual(new[]
        {
            RuntimeIdentity.DedicatedBeltSlotName,
            RuntimeIdentity.DedicatedHeadBandSlotName
        }), "machine-readable semantic slot IDs match runtime identity");
    }

    private static string[] ReadArray(JsonObject contract, string name) =>
        contract[name]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();

    private static JsonObject Item(string id, string tpl, string? parentId = null, string? slotId = null)
    {
        JsonObject item = new()
        {
            ["_id"] = id,
            ["_tpl"] = tpl
        };
        if (parentId is not null) item["parentId"] = parentId;
        if (slotId is not null) item["slotId"] = slotId;
        return item;
    }

    private static IEnumerable<string> FindTemplateIds(JsonNode node) =>
        FindStringProperties(node, "_tpl");

    private static IEnumerable<string> FindObjectIds(JsonNode node) =>
        FindStringProperties(node, "_id");

    private static IEnumerable<string> FindStringProperties(JsonNode? node, string property)
    {
        if (node is JsonObject obj)
        {
            if (obj[property] is JsonValue value && value.TryGetValue<string>(out string? text) && text is not null)
                yield return text;
            foreach ((_, JsonNode? child) in obj)
                foreach (string found in FindStringProperties(child, property))
                    yield return found;
            yield break;
        }

        if (node is JsonArray array)
            foreach (JsonNode? child in array)
                foreach (string found in FindStringProperties(child, property))
                    yield return found;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Profile cleanup regression failed: " + message);
    }
}
