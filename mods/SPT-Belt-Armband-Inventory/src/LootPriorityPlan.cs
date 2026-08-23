using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal enum LootItemKind
    {
        Other,
        Magazine,
        Ammo,
        Money,
        Throwable
    }

    internal static class LootPriorityPlan
    {
        internal const string Vest = "Vest";
        internal const string Belt = "Belt";
        internal const string Pockets = "Pockets";
        internal const string Backpack = "Backpack";
        internal const string Secure = "Secure";

        internal static string[] Build(LootItemKind kind, bool beltAvailable)
        {
            string[] vanilla;
            switch (kind)
            {
                case LootItemKind.Magazine:
                case LootItemKind.Ammo:
                    vanilla = new[] { Vest, Pockets, Backpack, Secure };
                    break;
                case LootItemKind.Money:
                    vanilla = new[] { Secure, Backpack, Vest, Pockets };
                    break;
                case LootItemKind.Throwable:
                    vanilla = new[] { Pockets, Vest, Backpack, Secure };
                    break;
                default:
                    vanilla = new[] { Backpack, Vest, Pockets, Secure };
                    break;
            }

            if (!beltAvailable) return vanilla;

            var result = new List<string>(5);
            switch (kind)
            {
                case LootItemKind.Ammo:
                    result.Add(Belt);
                    result.AddRange(vanilla);
                    break;
                case LootItemKind.Magazine:
                    result.Add(Vest);
                    result.Add(Belt);
                    result.Add(Pockets);
                    result.Add(Backpack);
                    result.Add(Secure);
                    break;
                case LootItemKind.Money:
                    result.Add(Secure);
                    result.Add(Backpack);
                    result.Add(Vest);
                    result.Add(Belt);
                    result.Add(Pockets);
                    break;
                case LootItemKind.Throwable:
                    result.Add(Pockets);
                    result.Add(Belt);
                    result.Add(Vest);
                    result.Add(Backpack);
                    result.Add(Secure);
                    break;
                default:
                    result.Add(Backpack);
                    result.Add(Vest);
                    result.Add(Belt);
                    result.Add(Pockets);
                    result.Add(Secure);
                    break;
            }
            return result.ToArray();
        }
    }
}
