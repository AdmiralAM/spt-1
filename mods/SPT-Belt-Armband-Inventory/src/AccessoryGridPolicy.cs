namespace SPTBeltArmbandInventory
{
    // Pure geometry rules. They describe the declared grid, not a Unity prefab.
    // The runtime must render the declared dimensions without adding filler cells.
    internal static class AccessoryGridPolicy
    {
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
    }
}
