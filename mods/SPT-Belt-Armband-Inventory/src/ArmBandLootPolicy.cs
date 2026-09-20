using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTBeltArmbandInventory;

public enum ArmBandGridOrientation { Vertical, Horizontal }

public static class ArmBandLootPolicy
{
    public static bool Includes(ArmBandVisualPool selected, ArmBandVisualPool item) => selected switch
    {
        ArmBandVisualPool.ExistingRaid => item == ArmBandVisualPool.ExistingRaid,
        ArmBandVisualPool.Standard => item is ArmBandVisualPool.ExistingRaid or ArmBandVisualPool.Standard,
        ArmBandVisualPool.All => true,
        _ => false
    };

    public static KeyValuePair<string, double>[] Split(
        double sourceWeight,
        IEnumerable<ArmBandVariantDescriptor> variants,
        IReadOnlyDictionary<ArmBandRole, int> roleWeights)
    {
        if (!double.IsFinite(sourceWeight) || sourceWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWeight));

        ArmBandVariantDescriptor[] enabled = variants
            .Where(variant => roleWeights.TryGetValue(variant.Role, out int weight) && weight > 0)
            .ToArray();
        long total = enabled.Sum(variant => (long)roleWeights[variant.Role]);
        if (enabled.Length == 0 || total <= 0)
            return [];

        var result = new KeyValuePair<string, double>[enabled.Length];
        double assigned = 0;
        for (int index = 0; index < enabled.Length; index++)
        {
            double weight = index == enabled.Length - 1
                ? sourceWeight - assigned
                : sourceWeight * roleWeights[enabled[index].Role] / total;
            assigned += weight;
            result[index] = KeyValuePair.Create(enabled[index].TemplateId, weight);
        }
        return result;
    }
}
