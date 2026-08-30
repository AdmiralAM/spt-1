using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class AdmiralTraderRelationshipManifestSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string traderBase = """
        {"loyaltyLevels":[
          {"minLevel":1,"minSalesSum":0,"minStanding":0},
          {"minLevel":15,"minSalesSum":0,"minStanding":0.1},
          {"minLevel":25,"minSalesSum":0,"minStanding":0.3},
          {"minLevel":35,"minSalesSum":0,"minStanding":0.55}
        ]}
        """;
        const string disabled = """
        {"schemaVersion":1,"stockClass":"Relationship","authority":{"salesSumGateAllowed":false,"questGateAllowed":false,"capabilityAuthority":false,"finiteStockRequired":true},"materialization":{"enabled":false},"offers":[]}
        """;
        const string enabled = """
        {"schemaVersion":1,"stockClass":"Relationship","authority":{"salesSumGateAllowed":false,"questGateAllowed":false,"capabilityAuthority":false,"finiteStockRequired":true},"materialization":{"enabled":true},"offers":[{"offerId":"rel1","tpl":"tpl-rel","loyaltyLevel":3,"stockPerReset":4,"buyRestriction":2,"questGate":null}]}
        """;

        Require(AdmiralTraderRelationshipManifest.Parse(null, true, traderBase).Count == 0, "missing optional manifest must preserve current frozen contract");
        Require(AdmiralTraderRelationshipManifest.Parse(disabled, true, traderBase).Count == 0, "disabled manifest must not fabricate offers");
        var parsed = AdmiralTraderRelationshipManifest.Parse(enabled, true, traderBase);
        Require(parsed.Count == 1, "enabled manifest must classify one explicit offer");
        var offer = parsed[0];
        Require(offer.OfferId == "rel1" && offer.ItemTemplateId == "tpl-rel", "Relationship identity mismatch");
        Require(offer.LoyaltyLevel == 3 && offer.RequiredStanding == 0.3 && offer.MinimumPlayerLevel == 25, "Relationship loyalty gate mismatch");
        Require(offer.StockPerReset == 4 && offer.BuyRestrictionPerReset == 2, "Relationship bounded supply mismatch");

        MustFail("policy disabled", () => AdmiralTraderRelationshipManifest.Parse(enabled, false, traderBase));
        MustFail("sales gate", () => AdmiralTraderRelationshipManifest.Parse(enabled.Replace("\"salesSumGateAllowed\":false", "\"salesSumGateAllowed\":true"), true, traderBase));
        MustFail("quest gate", () => AdmiralTraderRelationshipManifest.Parse(enabled.Replace("\"questGate\":null", "\"questGate\":\"q1\""), true, traderBase));
        MustFail("unbounded logical buy", () => AdmiralTraderRelationshipManifest.Parse(enabled.Replace("\"buyRestriction\":2", "\"buyRestriction\":5"), true, traderBase));
        MustFail("sales-sum drift", () => AdmiralTraderRelationshipManifest.Parse(enabled, true, traderBase.Replace("\"minSalesSum\":0,\"minStanding\":0.3", "\"minSalesSum\":1000,\"minStanding\":0.3")));
        Console.WriteLine("Economy Admiral Admiral Trader Relationship manifest smoke PASS");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void MustFail(string name, Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException($"Relationship manifest smoke expected '{name}' to fail.");
    }
}
