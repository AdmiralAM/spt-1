namespace Admiral.SecondLife;

internal static class DeterministicSelection
{
    internal static int Index(int seed, IReadOnlyList<string> stableKeys)
    {
        if (stableKeys.Count == 0)
        {
            throw new ArgumentException("At least one stable key is required.", nameof(stableKeys));
        }

        uint hash = unchecked((uint)seed) ^ 2166136261u;
        foreach (string key in stableKeys)
        {
            foreach (char character in key)
            {
                hash ^= character;
                hash *= 16777619u;
            }
        }

        hash ^= hash >> 16;
        hash *= 0x7feb352du;
        hash ^= hash >> 15;
        return (int)(hash % (uint)stableKeys.Count);
    }
}
