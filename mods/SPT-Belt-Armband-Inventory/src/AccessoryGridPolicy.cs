namespace SPTBeltArmbandInventory
{
    // Pure geometry rules. They describe the declared grid, not a Unity prefab.
    // Runtime evidence from SPT 4.1.3 shows a native cell pitch of 63px and
    // a 32px vertical GridWindow chrome band. Horizontal chrome is deliberately
    // kept to the tight native border allowance rather than the old 128px
    // one-column minimum, so the outer frame follows the declared grid closely.
    internal static class AccessoryGridPolicy
    {
        internal const float CellPixels = 63f;
        internal const float WindowHorizontalChrome = 10f;
        internal const float WindowVerticalChrome = 32f;

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

        // Historical helper retained only so older regression coverage still
        // compiles while the production GridWindow route uses ExactWindow*.
        // Do not use these minimum-clamped helpers for wearable runtime sizing.
        internal static float CompactWindowWidth(int columns, float measuredGridWidth)
        {
            float exact = ExactWindowWidth(columns, measuredGridWidth);
            return exact > 0f && exact < 96f ? 96f : exact;
        }

        internal static float CompactWindowWidth(int columns)
        {
            return CompactWindowWidth(columns, 0f);
        }

        internal static float CompactWindowHeight(int rows, float measuredGridHeight)
        {
            float exact = ExactWindowHeight(rows, measuredGridHeight);
            return exact > 0f && exact < 136f ? 136f : exact;
        }

        internal static float CompactWindowHeight(int rows)
        {
            return CompactWindowHeight(rows, 0f);
        }
    }
}
