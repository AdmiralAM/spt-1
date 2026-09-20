using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Pure cleanup engine used by uninstall/recovery tooling. It intentionally does
/// not depend on registered item templates: ownership is decided exclusively by
/// PersistentIdentityManifest, so it still works after B&A&HB templates are absent.
/// </summary>
public static class ProfileCleanupPolicy
{
    public sealed record CleanupResult(int RemovedItems, int RemovedReferences, IReadOnlyList<string> Locations);

    public static CleanupResult Clean(JsonNode? profile)
    {
        if (profile is null) return new CleanupResult(0, 0, Array.Empty<string>());

        // Instance IDs are expected to be unique in healthy profiles, but recovery
        // must fail closed if a malformed/foreign payload violates that invariant.
        // An ambiguous ID may still identify an owned template for direct removal,
        // but it must never become authority for deleting parentId/itemId references.
        Dictionary<string, int> instanceIdCounts = CollectInstanceIdCounts(profile);
        var removedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        var locations = new HashSet<string>(StringComparer.Ordinal);
        int removedItems = 0;
        int removedReferences = 0;

        // Pass 1: remove every serialized item whose _tpl belongs to this mod and
        // remember only cardinality-proven unique instance ids for cascading. This
        // catches stash/equipment, mail rewards, insurance payloads and build item
        // trees without allowing duplicate-id corruption to cross ownership bounds.
        Walk(profile, "$", (array, index, node, path) =>
        {
            if (node is not JsonObject obj) return false;
            string? tpl = ReadString(obj, "_tpl");
            if (!PersistentIdentityManifest.IsOwnedTemplate(tpl)) return false;

            string? id = ReadString(obj, "_id");
            if (IsUniqueInstanceId(id, instanceIdCounts)) removedInstanceIds.Add(id!);
            array.RemoveAt(index);
            removedItems++;
            locations.Add(path);
            return true;
        });

        // Pass 2: remove descendants and direct references to removed instances.
        // Only parentId is a containment edge and may promote the removed object's
        // own unique _id into further cascade authority. itemId is a service/build
        // reference edge: remove that exact reference, but never let an arbitrary
        // reference record become a new ownership root for deleting foreign state.
        bool changed;
        do
        {
            changed = false;
            Walk(profile, "$", (array, index, node, path) =>
            {
                if (node is not JsonObject obj) return false;
                string? parentId = ReadString(obj, "parentId");
                string? itemId = ReadString(obj, "itemId");
                string? id = ReadString(obj, "_id");
                bool matchedParent = !string.IsNullOrEmpty(parentId) && removedInstanceIds.Contains(parentId);
                bool matchedItemReference = !string.IsNullOrEmpty(itemId) && removedInstanceIds.Contains(itemId);
                if (matchedParent || matchedItemReference)
                {
                    if (matchedParent && IsUniqueInstanceId(id, instanceIdCounts))
                        removedInstanceIds.Add(id!);
                    array.RemoveAt(index);
                    removedReferences++;
                    locations.Add(path);
                    changed = true;
                    return true;
                }
                return false;
            });
        } while (changed);

        return new CleanupResult(removedItems, removedReferences, locations.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static Dictionary<string, int> CollectInstanceIdCounts(JsonNode profile)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        CountInstanceIds(profile, counts);
        return counts;
    }

    private static void CountInstanceIds(JsonNode node, Dictionary<string, int> counts)
    {
        if (node is JsonObject obj)
        {
            string? id = ReadString(obj, "_id");
            if (!string.IsNullOrEmpty(id))
                counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;

            foreach (var property in obj)
                if (property.Value is not null) CountInstanceIds(property.Value, counts);
            return;
        }

        if (node is not JsonArray array) return;
        foreach (JsonNode? child in array)
            if (child is not null) CountInstanceIds(child, counts);
    }

    private static bool IsUniqueInstanceId(string? id, IReadOnlyDictionary<string, int> counts)
    {
        return !string.IsNullOrEmpty(id) && counts.TryGetValue(id, out int count) && count == 1;
    }

    private delegate bool ArrayVisitor(JsonArray owner, int index, JsonNode? node, string path);

    private static void Walk(JsonNode node, string path, ArrayVisitor visitor)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
                if (property.Value is not null) Walk(property.Value, path + "." + property.Key, visitor);
            return;
        }

        if (node is not JsonArray array) return;
        for (int i = array.Count - 1; i >= 0; i--)
        {
            JsonNode? child = array[i];
            string childPath = path + "[" + i + "]";
            if (visitor(array, i, child, childPath)) continue;
            if (child is not null) Walk(child, childPath, visitor);
        }
    }

    private static string? ReadString(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out JsonNode? value) || value is null) return null;
        try { return value.GetValue<string>(); }
        catch { return null; }
    }
}