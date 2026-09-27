namespace Connect4.Engine.Search;

/// <summary>Move order from connect4-master: the most winning cells after the move first, then centre columns first.</summary>
internal static class MoveOrdering
{
    /// <summary>Centre columns first: 3, 2, 4, 1, 5, 0, 6 (the C++ comment says 3, 4, 2, ... but its formula gives this).</summary>
    public static readonly int[] ColumnOrder =
        [.. Enumerable.Range(0, Position.Width).Select(i => Position.Width / 2 + (1 - 2 * (i % 2)) * (i + 1) / 2)];

    /// <summary>Adds the moves in <paramref name="possible"/>; the column from the hash table, if any, comes first.</summary>
    public static void Add(ref MoveSorter sorter, in Position position, ulong possible, int hashColumn = -1)
    {
        for (int i = Position.Width - 1; i >= 0; i--)
        {
            int column = ColumnOrder[i];
            ulong move = possible & Position.ColumnMask(column);
            if (move != 0)
            {
                sorter.Add(move, column == hashColumn ? int.MaxValue : position.MoveScore(move));
            }
        }
    }
}
