namespace SPTBeltArmbandInventory
{
    // Pure geometry rules. They describe the declared grid, not a Unity prefab.
    // The runtime must render the declared dimensions without filler cells or
    // artificial minimum window dimensions. The only non-grid space is the
    // same native GridWindow chrome used by ordinary EFT containers.
    internal static class AccessoryGridPolicy
    {
        internal const float CellPixels = 63f;
        internal const float WindowHorizontalChrome = 24f;
        internal const float WindowVerticalChrome = 34f;

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

        internal static float ExactWindowWidth(int columns, float measuredGridWidth)
        {
            if (columns <= 0) return 0f;
            float gridWidth = measuredGridWidth > 0f ? measuredGridWidth : columns * CellPixels;
            return gridWidth + WindowHorizontalChrome;
        }

        internal static float ExactWindowWidth(int columns)
        {
            return ExactWindowWidth(columns, 0f);
        }

        internal static float ExactWindowHeight(int rows, float measuredGridHeight)
        {
            if (rows <= 0) return 0f;
            float gridHeight = measuredGridHeight > 0f ? measuredGridHeight : rows * CellPixels;
            return gridHeight + WindowVerticalChrome;
        }

        internal static float ExactWindowHeight(int rows)
        {
            return ExactWindowHeight(rows, 0f);
        }
    }
}
