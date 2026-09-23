using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class WalletLogisticsRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string[] money = LootPriorityPlan.Build(LootItemKind.Money, true);
        if (money.Length == 0 || money[0] != LootPriorityPlan.Wallet)
            throw new InvalidOperationException("Money auto-deposit must try exact Admiral wallet containers first.");
        if (Array.IndexOf(money, LootPriorityPlan.Secure) < 0
            || Array.IndexOf(money, LootPriorityPlan.Backpack) < 0)
            throw new InvalidOperationException("Money auto-deposit must retain vanilla fallback containers.");

        string[] other = LootPriorityPlan.Build(LootItemKind.Other, true);
        if (Array.IndexOf(other, LootPriorityPlan.Wallet) >= 0)
            throw new InvalidOperationException("Non-money loot must not be routed into wallet containers.");

        if (!WearableItemDescriptorRegistry.HasCapability(RuntimeIdentity.WristWalletItemId, AccessoryCapability.LootPriority)
            || !WearableItemDescriptorRegistry.HasCapability(RuntimeIdentity.EmergencyHeadBandItemId, AccessoryCapability.LootPriority))
            throw new InvalidOperationException("Owned wallet products must expose money auto-deposit capability.");
    }
}
