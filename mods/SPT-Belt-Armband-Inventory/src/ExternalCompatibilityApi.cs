#nullable disable
using System;

namespace SPTBeltArmbandInventory
{
    /// <summary>Exact, versioned ownership signals for the external compatibility suite.</summary>
    public static class ExternalCompatibilityApi
    {
        public const int ContractVersion = 1;
        static readonly object Gate = new object();
        static object tgcOwner;
        static object packNStrapOwner;

        public static bool IsTgc300Claimed { get { lock (Gate) return tgcOwner != null; } }
        public static bool IsPackNStrap211Claimed { get { lock (Gate) return packNStrapOwner != null; } }

        public static bool TryClaimTgc300(int contractVersion, object ownerToken)
            => TryClaim(contractVersion, ownerToken, ref tgcOwner);

        public static bool TryClaimPackNStrap211(int contractVersion, object ownerToken)
            => TryClaim(contractVersion, ownerToken, ref packNStrapOwner);

        static bool TryClaim(int contractVersion, object ownerToken, ref object currentOwner)
        {
            if (contractVersion != ContractVersion || ownerToken == null) return false;
            lock (Gate)
            {
                if (currentOwner == null) { currentOwner = ownerToken; return true; }
                return ReferenceEquals(currentOwner, ownerToken);
            }
        }

        internal static void ResetForRegression()
        {
            lock (Gate) { tgcOwner = null; packNStrapOwner = null; }
        }
    }
}
