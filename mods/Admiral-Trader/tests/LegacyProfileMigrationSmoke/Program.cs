using AdmiralTrader.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;

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
            Quests =
            [
                new QuestStatus
                {
                    QId = new MongoId("668aacd1dee3de3ce276fdef"),
                    Status = QuestStatusEnum.Started,
                    StartTime = 123,
                    StatusTimers = [],
                    CompletedConditions = ["672e31c3262af62a8eb157cd"]
                },
                new QuestStatus
                {
                    QId = new MongoId("673f06ffd971eef67d8cc504"),
                    Status = QuestStatusEnum.Success,
                    StartTime = 50,
                    StatusTimers = [],
                    CompletedConditions = []
                },
                new QuestStatus
                {
                    QId = new MongoId("668aace8ff74aecfbcfbe9e6"),
                    Status = QuestStatusEnum.AvailableForFinish,
                    StartTime = 80,
                    StatusTimers = new() { [QuestStatusEnum.AvailableForFinish] = 99 },
                    CompletedConditions = ["668aacd1dee3de3ce276fdfa"]
                }
            ],
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
            Messages =
            [
                new Message
                {
                    Id = painterMessage,
                    UserId = painterId,
                    HasRewards = true,
                    RewardCollected = false,
                    Items = new MessageItems
                    {
                        Stash = new MongoId("668000000000000000000002"),
                        Data = [new Item { Id = new MongoId("668000000000000000000003"), Template = new MongoId("5449016a4bdc2d6f028b456f"), Upd = new Upd { StackObjectsCount = 21000 } }]
                    }
                }
            ],
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
    throw new Exception("legacy dialogue or its unclaimed attachment was not preserved under Admiral");
Message migratedMessage = dialogue.Messages!.Single();
if (migratedMessage.RewardCollected != false
    || migratedMessage.Items?.Data?.Single().Upd?.StackObjectsCount != 21000)
    throw new Exception("unclaimed legacy message attachment changed during migration");
if (profile.CharacterData.PmcData.Quests!.Count != 3
    || profile.CharacterData.PmcData.Quests[0].Status != QuestStatusEnum.Started
    || profile.CharacterData.PmcData.Quests[0].CompletedConditions!.Single() != "672e31c3262af62a8eb157cd"
    || profile.CharacterData.PmcData.Quests[1].Status != QuestStatusEnum.Success
    || profile.CharacterData.PmcData.Quests[2].Status != QuestStatusEnum.AvailableForFinish
    || profile.CharacterData.PmcData.Quests[2].StatusTimers[QuestStatusEnum.AvailableForFinish] != 99
    || profile.CharacterData.PmcData.Quests[2].CompletedConditions!.Single() != "668aacd1dee3de3ce276fdfa")
    throw new Exception("persistent external quest status or objective progress changed during migration");

SptProfile failedPainterReward = new()
{
    CharacterData = new Characters
    {
        PmcData = new PmcData
        {
            Quests =
            [
                new QuestStatus
                {
                    QId = new MongoId("668aacd1dee3de3ce276fdef"),
                    StartTime = 150,
                    Status = QuestStatusEnum.Success,
                    StatusTimers = new() { [QuestStatusEnum.Success] = 220 },
                    CompletedConditions = []
                }
            ],
            TradersInfo = new() { [admiralId] = new TraderInfo { Standing = 0.592 } },
            Info = new SPTarkov.Server.Core.Models.Eft.Common.Tables.Info { Experience = 1212906 }
        }
    },
    SptData = new Spt
    {
        Migrations = new() { ["admiral-trader-legacy-consolidation-v1"] = 200 }
    }
};
List<Item>? repairedItems = null;
if (!LegacyTraderConsolidation.ApplyPainterTapedUpRewardRepair(failedPainterReward, items => repairedItems = items, 300))
    throw new Exception("eligible failed Painter reward was not repaired");
if (LegacyTraderConsolidation.ApplyPainterTapedUpRewardRepair(failedPainterReward, _ => throw new Exception("duplicate delivery"), 301))
    throw new Exception("Painter reward repair was not idempotent");
if (repairedItems is null || repairedItems.Count != 1
    || repairedItems[0].Template.ToString() != "5449016a4bdc2d6f028b456f"
    || repairedItems[0].Upd?.StackObjectsCount != 21000)
    throw new Exception("Painter reward repair did not deliver exactly 21000 RUB");
if (failedPainterReward.CharacterData.PmcData.TradersInfo![admiralId].Standing != 0.612)
    throw new Exception("Painter reward repair did not restore exactly 0.02 Admiral standing");
if (failedPainterReward.CharacterData.PmcData.Info!.Experience != 1212906)
    throw new Exception("Painter reward repair repeated XP");
if (failedPainterReward.SptData.Migrations!["admiral-trader-painter-taped-up-reward-repair-v1"] != 300)
    throw new Exception("Painter reward repair marker is missing");

SptProfile historicalPainterCompletion = new()
{
    CharacterData = new Characters
    {
        PmcData = new PmcData
        {
            Quests =
            [
                new QuestStatus
                {
                    QId = new MongoId("668aacd1dee3de3ce276fdef"),
                    StartTime = 100,
                    Status = QuestStatusEnum.Success,
                    StatusTimers = new() { [QuestStatusEnum.Success] = 190 },
                    CompletedConditions = []
                }
            ],
            TradersInfo = new() { [admiralId] = new TraderInfo { Standing = 0.5 } }
        }
    },
    SptData = new Spt { Migrations = new() { ["admiral-trader-legacy-consolidation-v1"] = 200 } }
};
if (LegacyTraderConsolidation.ApplyPainterTapedUpRewardRepair(historicalPainterCompletion, _ => throw new Exception("historical delivery"), 300))
    throw new Exception("historical Painter completion was incorrectly compensated");

Console.WriteLine("Legacy profile migration PASS: state preserved; failed Painter reward repaired exactly once without repeated XP.");
