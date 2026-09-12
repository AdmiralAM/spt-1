using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class HeadBandUtilityPolicyRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (RuntimeIdentity.EmergencyHeadBandGridColumns != 1 || RuntimeIdentity.EmergencyHeadBandGridRows != 2)
            throw new InvalidOperationException("HeadBand utility geometry must remain 1x2.");

        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            HeadBandUtilityPolicy.Rouble,
            HeadBandUtilityPolicy.Dollar,
            HeadBandUtilityPolicy.Euro,
            HeadBandUtilityPolicy.ApolloSoyuz,
            HeadBandUtilityPolicy.Malboro,
            HeadBandUtilityPolicy.Wilston,
            HeadBandUtilityPolicy.Strike,
            HeadBandUtilityPolicy.VanillaWallet,
            HeadBandUtilityPolicy.WzWallet
        };
        if (HeadBandUtilityPolicy.AcceptedTemplateIds.Count != expected.Count)
            throw new InvalidOperationException("HeadBand whitelist contains duplicates or unexpected entries.");
        for (int i = 0; i < HeadBandUtilityPolicy.AcceptedTemplateIds.Count; i++)
            if (!expected.Remove(HeadBandUtilityPolicy.AcceptedTemplateIds[i]))
                throw new InvalidOperationException("HeadBand whitelist contains an unexpected template ID.");
        if (expected.Count != 0)
            throw new InvalidOperationException("HeadBand whitelist lost an intended utility item.");

        if (!HeadBandUtilityPolicy.IsAccepted("5783c43d2459774bbe137486")
            || !HeadBandUtilityPolicy.IsAccepted("60b0f6c058e0b0481a09ad11"))
            throw new InvalidOperationException("HeadBand must accept both Simple Wallet and WZ Wallet.");
        if (HeadBandUtilityPolicy.IsAccepted("5734758f24597738025ee253"))
            throw new InvalidOperationException("Golden neck chain must never be accepted as Apollo cigarettes.");
        if (HeadBandUtilityPolicy.IsAccepted("544fb3f34bdc2d03748b456a"))
            throw new InvalidOperationException("Broad medical storage must not leak back into HeadBand utility policy.");

        string payload = WearableProtectionContract.Encode(true, false, true);
        if (!string.Equals(payload, "{\"armBandProtected\":true,\"beltProtected\":false,\"headBandProtected\":true}", StringComparison.Ordinal))
            throw new InvalidOperationException("Protection F12 payload contract drifted.");
    }
}
