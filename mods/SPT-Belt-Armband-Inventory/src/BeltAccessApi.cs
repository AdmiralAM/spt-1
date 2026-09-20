using System;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Stable, reflection-free boundary for optional compatibility adapters that need to
    /// inspect the items currently exposed by the dedicated Belt source.
    /// </summary>
    public static class BeltAccessApi
    {
        public const int ContractVersion = 1;

        static readonly object Gate = new object();
        static Func<object, object[]> enumerate;
        static object owner;

        public static bool IsAvailable
        {
            get { lock (Gate) return enumerate != null && owner != null; }
        }

        /// <summary>
        /// Returns a bounded snapshot of the equipped slot15 Belt root and its contents.
        /// The inventory argument must be the live EFT Inventory instance. False with an
        /// empty result means the Belt runtime could not prove its exact access contract.
        /// </summary>
        public static bool TryEnumerateBeltSources(object inventory, out object[] sources)
        {
            sources = Array.Empty<object>();
            if (inventory == null) return false;

            Func<object, object[]> current;
            lock (Gate)
            {
                if (enumerate == null || owner == null) return false;
                current = enumerate;
            }

            try
            {
                object[] snapshot = current(inventory);
                if (snapshot == null) return false;
                for (int i = 0; i < snapshot.Length; i++) if (snapshot[i] == null) return false;
                sources = (object[])snapshot.Clone();
                return true;
            }
            catch
            {
                sources = Array.Empty<object>();
                return false;
            }
        }

        internal static bool TryPublish(object ownerToken, Func<object, object[]> provider)
        {
            if (ownerToken == null || provider == null) return false;
            lock (Gate)
            {
                if (owner == null && enumerate == null)
                {
                    owner = ownerToken;
                    enumerate = provider;
                    return true;
                }
                return ReferenceEquals(owner, ownerToken) && ReferenceEquals(enumerate, provider);
            }
        }

        internal static void Revoke(object ownerToken)
        {
            if (ownerToken == null) return;
            lock (Gate)
            {
                if (!ReferenceEquals(owner, ownerToken)) return;
                enumerate = null;
                owner = null;
            }
        }

        internal static void ResetForRegression()
        {
            lock (Gate)
            {
                enumerate = null;
                owner = null;
            }
        }
    }
}
