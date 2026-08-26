namespace SPTBeltArmbandInventory
{
    // Pure geometry rules. They describe the declared grid, not a Unity prefab.
    // The runtime must render the declared dimensions without adding filler cells.
    internal static class AccessoryGridPolicy
    {
        internal const float CellPixels = 63f;
        internal const float WindowHorizontalPadding = 24f;
        internal const float WindowVerticalPadding = 34f;
        internal const float MinimumWindowWidth = 96f;
        internal const float MinimumWindowHeight = 136f;

        internal static bool IsValid(int columns, int rows)
        {
            return columns > 0 && rows > 0;
        }

        internal static int CellCount(int columns, int rows)
        {
            return IsValid(columns, rows) ? columns * rows : 0;
        }

        internal static bool IsExactShape(int columns, int rows, int expectedColumns, int expectedRows)
        {
            return IsValid(columns, rows)
                && IsValid(expectedColumns, expectedRows)
                && columns == expectedColumns
                && rows == expectedRows;
        }

        internal static bool FitsDeclaredCapacity(int columns, int rows, int itemCount)
        {
            return IsValid(columns, rows) && itemCount >= 0 && itemCount <= CellCount(columns, rows);
        }

        internal static bool IsRuntimeCandidateTemplate(string templateId)
        {
            return string.Equals(templateId, RuntimeIdentity.CandidateItemId, System.StringComparison.Ordinal);
        }

        internal static float CompactWindowWidth(int columns, float measuredGridWidth)
        {
            float gridWidth = measuredGridWidth > 0f ? measuredGridWidth : columns * CellPixels;
            float width = gridWidth + WindowHorizontalPadding;
            return width < MinimumWindowWidth ? MinimumWindowWidth : width;
        }

        internal static float CompactWindowWidth(int columns)
        {
            return CompactWindowWidth(columns, 0f);
        }

        internal static float CompactWindowHeight(int rows, float measuredGridHeight)
        {
            float gridHeight = measuredGridHeight > 0f ? measuredGridHeight : rows * CellPixels;
            float height = gridHeight + WindowVerticalPadding;
            return height < MinimumWindowHeight ? MinimumWindowHeight : height;
        }

        internal static float CompactWindowHeight(int rows)
        {
            return CompactWindowHeight(rows, 0f);
        }
    }
}
