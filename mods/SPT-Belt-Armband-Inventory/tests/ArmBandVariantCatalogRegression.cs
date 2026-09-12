using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using SPTBeltArmbandInventory;

internal static class ArmBandVariantCatalogRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        ArmBandVariantDescriptor[] variants = ArmBandVariantCatalog.All;
        if (variants.Length != 130)
            throw new InvalidOperationException("ArmBand catalog must contain 26 visuals x 5 roles.");

        var templates = new HashSet<string>(StringComparer.Ordinal);
        var grids = new HashSet<string>(StringComparer.Ordinal);
        foreach (IGrouping<string, ArmBandVariantDescriptor> visual in variants.GroupBy(x => x.SourceTemplateId, StringComparer.Ordinal))
        {
            if (visual.Count() != 5 || visual.Select(x => x.Role).Distinct().Count() != 5)
                throw new InvalidOperationException("Every ArmBand visual must publish exactly one immutable variant per role.");
        }

        foreach (ArmBandVariantDescriptor variant in variants)
        {
            if (!templates.Add(variant.TemplateId) || !grids.Add(variant.GridId))
                throw new InvalidOperationException("ArmBand variant persistent identity was reused.");
            if (variant.TemplateId == variant.GridId)
                throw new InvalidOperationException("ArmBand template and grid identities must be separate.");
        }

        JsonObject contract = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "persistent-identities.json")))!.AsObject();
        if (contract["schemaVersion"]!.GetValue<int>() != 2)
            throw new InvalidOperationException("ArmBand variants require persistent manifest schema 2.");

        JsonArray serialized = contract["armBandVariants"]!.AsArray();
        if (serialized.Count != variants.Length)
            throw new InvalidOperationException("Compiled and recovery ArmBand variant counts differ.");

        for (int i = 0; i < variants.Length; i++)
        {
            JsonObject item = serialized[i]!.AsObject();
            ArmBandVariantDescriptor variant = variants[i];
            if (item["sourceTemplateId"]!.GetValue<string>() != variant.SourceTemplateId
                || item["visualKey"]!.GetValue<string>() != variant.VisualKey
                || item["visualPool"]!.GetValue<string>() != variant.VisualPool.ToString()
                || item["role"]!.GetValue<string>() != variant.Role.ToString()
                || item["templateId"]!.GetValue<string>() != variant.TemplateId
                || item["gridId"]!.GetValue<string>() != variant.GridId)
                throw new InvalidOperationException("Compiled and recovery ArmBand variant catalog order/content differ.");
        }
    }
}
