using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class AuditBundlePreviewSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var audit = new EconomyConfig
        {
            Mode = EconomyMode.Audit,
            EnablePlayableEconomyBundle = true,
        };

        if (!audit.EnableItemRewardStackNormalization
            || !audit.EnableQuestXpPressure
            || !audit.EnableQuestStandingPressure
            || !audit.EnableRestartableQuestPressure
            || !audit.EnableTraderPurchasePressure
            || !audit.EnableTraderSellPressure
            || !audit.EnableFleaPurchasePressure
            || !audit.EnableFleaListingFeePressure
            || !audit.EnableLooseLootPressure
            || !audit.EnableStaticLootPressure)
        {
            throw new InvalidOperationException("Audit + Full Preset Bundle must expose the same enabled mechanism profile that Enforce would preview.");
        }

        var questOff = audit with { EnableQuestEconomyCluster = false };
        if (questOff.EnableItemRewardStackNormalization
            || questOff.EnableQuestXpPressure
            || questOff.EnableQuestStandingPressure
            || questOff.EnableRestartableQuestPressure)
            throw new InvalidOperationException("Quest Economy OFF must remain a hard gate in Audit preview.");
        if (!questOff.EnableTraderPurchasePressure || !questOff.EnableFleaPurchasePressure || !questOff.EnableLooseLootPressure)
            throw new InvalidOperationException("Disabling Quest Economy must not suppress other Audit preview clusters.");

        var selectiveAudit = audit with
        {
            EnablePlayableEconomyBundle = false,
            EnableQuestXpPressure = true,
        };
        if (!selectiveAudit.EnableQuestXpPressure || selectiveAudit.EnableQuestStandingPressure || selectiveAudit.EnableTraderPurchasePressure)
            throw new InvalidOperationException("Audit with Full Preset Bundle OFF must honor only explicitly configured granular mechanisms.");

        var off = audit with { Mode = EconomyMode.Off };
        if (off.EnableQuestXpPressure || off.EnableTraderPurchasePressure || off.EnableFleaPurchasePressure || off.EnableLooseLootPressure)
            throw new InvalidOperationException("Off mode must not activate Full Preset Bundle mechanisms.");

        Console.WriteLine("PASS Audit full-bundle preview matches Enforce mechanism selection without enabling Off mode");
    }
}
