using System;

namespace SPTBeltArmbandInventory
{
    internal static class RuntimeMutationPolicy
    {
        internal static bool ShouldRestore(object current, object installed)
        {
            return installed != null && ReferenceEquals(current, installed);
        }
    }
}
