using System;
using System.Collections;

namespace AdmiralCompatibilitySuite;

internal enum SlotAccessSyncResult
{
    Ineligible,
    AlreadyPresent,
    Added
}

internal static class SlotAccessSynchronizer
{
    internal static SlotAccessSyncResult EnsureFollower(IList slots, object leader, object follower)
    {
        if (slots == null)
        {
            throw new ArgumentNullException(nameof(slots));
        }

        if (!Contains(slots, leader))
        {
            return SlotAccessSyncResult.Ineligible;
        }

        if (Contains(slots, follower))
        {
            return SlotAccessSyncResult.AlreadyPresent;
        }

        slots.Add(follower);
        return SlotAccessSyncResult.Added;
    }

    private static bool Contains(IList slots, object value)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            if (Equals(slots[index], value))
            {
                return true;
            }
        }

        return false;
    }
}
