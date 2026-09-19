using AdmiralTrader.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;

MongoId admiralId = new(RuntimeIdentity.TraderId);
MongoId painterId = new(LegacyTraderConsolidation.PainterTraderId);
MongoId artemId = new(LegacyTraderConsolidation.ArtemTraderId);
MongoId painterOffer = new("668ff5bde41a0cce3b142465");
MongoId artemOffer = new("674000000000000000000001");
MongoId painterMessage = new("668000000000000000000001");

SptProfile profile = new()
{
    CharacterData = new Characters
    {
        PmcData = new PmcData
        {
            TradersInfo = new Dictionary<MongoId, TraderInfo>
            {
                [admiralId] = new() { Standing = 0.2, SalesSum = 12000, LoyaltyLevel = 2, Unlocked = true },
                [painterId] = new() { Standing = 0.55, SalesSum = 85000, LoyaltyLevel = 3, Unlocked = true },
                [artemId] = new() { Standing = 0.35, SalesSum = 42000, LoyaltyLevel = 2, Unlocked = true }
            }
        },
        ScavData = new PmcData
        {
            TradersInfo = new Dictionary<MongoId, TraderInfo>
            {
                [painterId] = new() { Standing = 0.1, SalesSum = 5000, Unlocked = true }
            }
        }
    },
    TraderPurchases = new Dictionary<MongoId, Dictionary<MongoId, TraderPurchaseData>?>
    {
        [painterId] = new() { [painterOffer] = new() { PurchaseCount = 2, PurchaseTimestamp = 100 } },
        [artemId] = new() { [artemOffer] = new() { PurchaseCount = 1, PurchaseTimestamp = 200 } }
    },
    DialogueRecords = new Dictionary<MongoId, Dialogue>
    {
        [painterId] = new()
        {
            Id = painterId,
            AttachmentsNew = 1,
            New = 1,
            Messages = [new Message { Id = painterMessage, UserId = painterId }],
            Users = [new UserDialogInfo { Id = painterId }]
        }
    },
    SptData = new Spt()
};

MongoId[] legacyIds = [painterId, artemId];
if (!LegacyTraderConsolidation.MigrateProfile(profile, legacyIds)) throw new Exception("first migration did not run");
if (LegacyTraderConsolidation.MigrateProfile(profile, legacyIds)) throw new Exception("migration is not idempotent");
if (LegacyTraderConsolidation.MigrateProfile(new SptProfile { SptData = new Spt() }, legacyIds))
    throw new Exception("clean profile was modified by legacy migration");

Dictionary<MongoId, TraderInfo> pmc = profile.CharacterData!.PmcData!.TradersInfo!;
if (pmc.ContainsKey(painterId) || pmc.ContainsKey(artemId)) throw new Exception("legacy trader relations remain");
if (!pmc.TryGetValue(admiralId, out TraderInfo? admiral) || admiral.Standing != 0.55 || admiral.SalesSum != 85000 || admiral.LoyaltyLevel != 3)
    throw new Exception("strongest standing/sales/loyalty progress was not preserved");
if (profile.CharacterData.ScavData!.TradersInfo!.ContainsKey(painterId)
    || !profile.CharacterData.ScavData.TradersInfo.ContainsKey(admiralId))
    throw new Exception("scav trader relation was not migrated");
if (profile.TraderPurchases!.ContainsKey(painterId) || profile.TraderPurchases.ContainsKey(artemId))
    throw new Exception("legacy purchase ledgers remain");
if (!profile.TraderPurchases[admiralId]!.ContainsKey(painterOffer)
    || !profile.TraderPurchases[admiralId]!.ContainsKey(artemOffer))
    throw new Exception("purchase history was not preserved");
if (profile.DialogueRecords!.ContainsKey(painterId)
    || !profile.DialogueRecords.TryGetValue(admiralId, out Dialogue? dialogue)
    || dialogue.Messages!.Single().UserId != admiralId
    || dialogue.Users!.Single().Id != admiralId)
    throw new Exception("legacy dialogue was not re-owned by Admiral");

Console.WriteLine("Legacy profile migration PASS: relations, standing, sales, purchases and dialogue preserved; second pass is idempotent.");
