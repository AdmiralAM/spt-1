using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

internal static class PersistentIdentityUniquenessRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "persistent-identities.json");
        JsonObject contract = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string group in new[] { "templateIds", "parentIds", "slotMongoIds", "gridIds", "assortIds" })
        {
            JsonArray values = contract[group]!.AsArray();
            foreach (JsonNode? node in values)
            {
                string id = node!.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id))
                    throw new InvalidOperationException("Persistent identity contract contains an empty " + group + " entry.");
                if (!seen.Add(id))
                    throw new InvalidOperationException("Persistent identity was reused across B&A&HB ownership groups: " + id);
            }
        }

        JsonArray wireSlots = contract["slotIds"]!.AsArray();
        if (wireSlots.Count != 2 || wireSlots[0]!.GetValue<string>() != "15" || wireSlots[1]!.GetValue<string>() != "16")
            throw new InvalidOperationException("Frozen dedicated wire slots must remain exactly 15 and 16.");

        JsonArray semanticSlots = contract["slotSemanticIds"]!.AsArray();
        if (semanticSlots.Count != 2
            || semanticSlots[0]!.GetValue<string>() != "BAndHBBelt"
            || semanticSlots[1]!.GetValue<string>() != "BAndHBHeadBand")
            throw new InvalidOperationException("Dedicated semantic slot identities drifted.");
    }
}
