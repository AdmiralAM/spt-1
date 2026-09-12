using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class WristWalletDescriptorRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (!WearableItemDescriptorRegistry.TryGet(RuntimeIdentity.WristWalletItemId, out WearableItemDescriptor wallet))
            throw new InvalidOperationException("Wrist Wallet descriptor must be registered.");
        if (wallet.Category != AccessoryCategory.ArmBand)
            throw new InvalidOperationException("Wrist Wallet must reuse the proven ArmBand host category.");
        if (wallet.GridColumns != 1 || wallet.GridRows != 1)
            throw new InvalidOperationException("Wrist Wallet must remain an exact 1x1 wearable container.");
        if (!wallet.Has(AccessoryCapability.PaymentSource))
            throw new InvalidOperationException("Wrist Wallet must expose payment-source capability.");
        if (wallet.Has(AccessoryCapability.FastAccess))
            throw new InvalidOperationException("Wrist Wallet must not inherit magazine fast-access semantics.");
        if (WearableItemDescriptorRegistry.HasCapability(RuntimeIdentity.CandidateItemId, AccessoryCapability.PaymentSource))
            throw new InvalidOperationException("Magazine RC must remain excluded from payment-source semantics.");
        if (!PaymentSlotPolicy.ShouldIncludeWearable(RuntimeIdentity.WristWalletItemId, true))
            throw new InvalidOperationException("Container Wrist Wallet must participate in payment-source enumeration.");
        if (PaymentSlotPolicy.ShouldIncludeWearable(RuntimeIdentity.WristWalletItemId, false))
            throw new InvalidOperationException("Non-container Wrist Wallet shape must fail closed.");
        if (PaymentSlotPolicy.ShouldIncludeWearable(RuntimeIdentity.CandidateItemId, true))
            throw new InvalidOperationException("Magazine RC must never be promoted to a payment source.");
        if (Math.Abs(AccessoryGridPolicy.ExactWindowWidth(1) - 73f) > 0.01f
            || Math.Abs(AccessoryGridPolicy.ExactWindowHeight(1) - 95f) > 0.01f)
            throw new InvalidOperationException("1x1 wearable window must use runtime-calibrated cell extent plus native chrome.");
    }
}
