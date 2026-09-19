namespace Admiral.SecondLife;

public static class PaidHealingPolicy
{
    public static int CalculateCost(
        float missingHealth,
        float healthPointPrice,
        float loyaltyHealPriceCoefficient,
        bool fastHealIsFree,
        float charismaHealingDiscount,
        int charismaLevel,
        int traderLoyaltyLevel,
        float healthRestoreTraderDiscount)
    {
        if (missingHealth <= 0f) return 0;
        if (fastHealIsFree) return 0;
        float loyalty = Math.Clamp(loyaltyHealPriceCoefficient / 100f, 0.05f, 10f);
        int level = Math.Min(50, Math.Max(0, charismaLevel));
        double discount = 1d - charismaHealingDiscount * level * Math.Max(0, traderLoyaltyLevel - 1) / 3d * healthRestoreTraderDiscount;
        return Math.Max(0, (int)Math.Ceiling(missingHealth * healthPointPrice * loyalty * discount));
    }

    public static IReadOnlyList<int>? PlanDebits(int cost, IReadOnlyList<int> stableStackCounts)
    {
        if (cost < 0) return null;
        var result = new int[stableStackCounts.Count];
        int remaining = cost;
        for (int index = 0; index < stableStackCounts.Count && remaining > 0; index++)
        {
            if (stableStackCounts[index] < 0) return null;
            result[index] = Math.Min(stableStackCounts[index], remaining);
            remaining -= result[index];
        }
        return remaining == 0 ? result : null;
    }
}
