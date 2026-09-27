using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace Connect4.Engine;

/// <summary>
/// Immutable 7×6 Connect 4 position, ported from connect4-master <c>Position.hpp</c>.
/// Bitboards are relative to the side to move. Each column uses <see cref="Height"/> + 1 bits,
/// bottom to top; the extra bit on top is always empty:
/// <code>
/// .  .  .  .  .  .  .
/// 5 12 19 26 33 40 47
/// 4 11 18 25 32 39 46
/// 3 10 17 24 31 38 45
/// 2  9 16 23 30 37 44
/// 1  8 15 22 29 36 43
/// 0  7 14 21 28 35 42
/// </code>
/// Like the C++ class, the search functions assume that the position contains no four in a row.
/// </summary>
public readonly record struct Position
{
    public const int Width = 7;
    public const int Height = 6;
    public const int CellCount = Width * Height;

    /// <summary>One bit at the bottom cell of each column.</summary>
    internal const ulong BottomMask =
        1UL | 1UL << 7 | 1UL << 14 | 1UL << 21 | 1UL << 28 | 1UL << 35 | 1UL << 42;

    /// <summary>All 42 cells of the board.</summary>
    internal const ulong BoardMask = BottomMask * ((1UL << Height) - 1);

    private Position(ulong current, ulong mask, int moves)
    {
        Current = current;
        Mask = mask;
        Moves = moves;
    }

    public static Position Empty => default;

    /// <summary>Discs of the side to move (C++: current_position).</summary>
    internal ulong Current { get; }

    /// <summary>All discs (C++: mask).</summary>
    internal ulong Mask { get; }

    /// <summary>Number of moves played from the empty board (C++: nbMoves).</summary>
    public int Moves { get; }

    public int EmptyCells => CellCount - Moves;

    public bool IsFull => Moves == CellCount;

    public Player ToMove => (Moves & 1) == 0 ? Player.Red : Player.Yellow;

    /// <summary>Unique key of the position on 49 bits (C++: key).</summary>
    public ulong Key => Current + Mask;

    /// <summary>The disc in a cell; column and row are 0-based, row 0 at the bottom.</summary>
    public Player? this[int column, int row]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(column);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Width);
            ArgumentOutOfRangeException.ThrowIfNegative(row);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Height);

            ulong cell = CellBit(column, row);
            if ((Mask & cell) == 0)
            {
                return null;
            }

            return (Current & cell) != 0 ? ToMove : ToMove.Opponent();
        }
    }

    public ulong Discs(Player player) => player == ToMove ? Current : Current ^ Mask;

    /// <summary>Plays the columns of a 1-based move sequence, e.g. "4453" (C++: play(seq)).</summary>
    /// <exception cref="FormatException">A character is not a column 1–7, a column is full, or a move makes four in a row.</exception>
    public static Position FromMoves(string moves)
    {
        Position position = Empty;
        for (int i = 0; i < moves.Length; i++)
        {
            int column = moves[i] - '1';
            int number = i + 1;
            if (column is < 0 or >= Width)
            {
                throw new FormatException($"Move {number}: '{moves[i]}' is not a column 1-{Width}.");
            }

            if (!position.CanPlay(column))
            {
                throw new FormatException($"Move {number}: column {column + 1} is full.");
            }

            if (position.IsWinningMove(column))
            {
                throw new FormatException($"Move {number}: column {column + 1} makes four in a row.");
            }

            position = position.PlayMove(position.MoveInColumn(column));
        }

        return position;
    }

    public bool CanPlay(int column) => (Mask & TopMaskColumn(column)) == 0;

    /// <exception cref="InvalidOperationException">The column is full.</exception>
    public Position Play(int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Width);
        if (!CanPlay(column))
        {
            throw new InvalidOperationException($"Column {column + 1} is full.");
        }

        return PlayMove(MoveInColumn(column));
    }

    /// <summary>Plays a move given as a single bit of <see cref="Possible"/>.</summary>
    internal Position PlayMove(ulong move) => new(Current ^ Mask, Mask | move, Moves + 1);

    /// <summary>The cell a disc dropped in the column lands on; 0 if the column is full.</summary>
    internal ulong MoveInColumn(int column) => (Mask + BottomMaskColumn(column)) & ColumnMask(column);

    /// <summary>The cells the side to move can play, one per non-full column.</summary>
    internal ulong Possible => (Mask + BottomMask) & BoardMask;

    public bool CanWinNext => (WinningPosition() & Possible) != 0;

    public bool IsWinningMove(int column) => (WinningPosition() & Possible & ColumnMask(column)) != 0;

    /// <summary>All empty cells where a disc of <paramref name="player"/> would complete a four.</summary>
    public ulong WinningCells(Player player) => ComputeWinningPosition(Discs(player), Mask);

    /// <summary>
    /// The possible moves that do not let the opponent win on the next move; 0 if every move loses
    /// (C++: possibleNonLosingMoves). Only valid when the side to move cannot win at once.
    /// </summary>
    internal ulong NonLosingMoves()
    {
        Debug.Assert(!CanWinNext);
        ulong possible = Possible;
        ulong opponentWin = OpponentWinningPosition();
        ulong forced = possible & opponentWin;
        if (forced != 0)
        {
            if ((forced & (forced - 1)) != 0)
            {
                return 0;
            }

            possible = forced;
        }

        return possible & ~(opponentWin >> 1);
    }

    /// <summary>Number of winning cells the side to move has after the move (C++: moveScore).</summary>
    internal int MoveScore(ulong move) => BitOperations.PopCount(ComputeWinningPosition(Current | move, Mask));

    internal ulong WinningPosition() => ComputeWinningPosition(Current, Mask);

    internal ulong OpponentWinningPosition() => ComputeWinningPosition(Current ^ Mask, Mask);

    /// <summary>Empty cells that complete a four for the discs in <paramref name="position"/> (C++: compute_winning_position).</summary>
    internal static ulong ComputeWinningPosition(ulong position, ulong mask)
    {
        // vertical
        ulong r = (position << 1) & (position << 2) & (position << 3);

        // horizontal
        ulong p = (position << (Height + 1)) & (position << 2 * (Height + 1));
        r |= p & (position << 3 * (Height + 1));
        r |= p & (position >> (Height + 1));
        p = (position >> (Height + 1)) & (position >> 2 * (Height + 1));
        r |= p & (position << (Height + 1));
        r |= p & (position >> 3 * (Height + 1));

        // diagonal 1
        p = (position << Height) & (position << 2 * Height);
        r |= p & (position << 3 * Height);
        r |= p & (position >> Height);
        p = (position >> Height) & (position >> 2 * Height);
        r |= p & (position << Height);
        r |= p & (position >> 3 * Height);

        // diagonal 2
        p = (position << (Height + 2)) & (position << 2 * (Height + 2));
        r |= p & (position << 3 * (Height + 2));
        r |= p & (position >> (Height + 2));
        p = (position >> (Height + 2)) & (position >> 2 * (Height + 2));
        r |= p & (position << (Height + 2));
        r |= p & (position >> 3 * (Height + 2));

        return r & (BoardMask ^ mask);
    }

    /// <summary>All discs in <paramref name="discs"/> that are part of a four in a row; 0 if there is none.</summary>
    public static ulong FindFours(ulong discs)
    {
        ulong fours = 0;
        foreach (int shift in (ReadOnlySpan<int>)[1, Height + 1, Height, Height + 2])
        {
            ulong start = discs & (discs >> shift) & (discs >> 2 * shift) & (discs >> 3 * shift);
            fours |= start | (start << shift) | (start << 2 * shift) | (start << 3 * shift);
        }

        return fours;
    }

    public static ulong CellBit(int column, int row) => 1UL << (column * (Height + 1) + row);

    /// <summary>The 0-based column of a cell bit.</summary>
    public static int ColumnOf(ulong cell) => BitOperations.TrailingZeroCount(cell) / (Height + 1);

    internal static ulong ColumnMask(int column) => ((1UL << Height) - 1) << column * (Height + 1);

    private static ulong TopMaskColumn(int column) => 1UL << (Height - 1 + column * (Height + 1));

    private static ulong BottomMaskColumn(int column) => 1UL << column * (Height + 1);

    /// <summary>Six lines, top row first: 'R' = Red, 'Y' = Yellow, '.' = empty.</summary>
    public override string ToString()
    {
        var text = new StringBuilder(Height * (Width + 1));
        for (int row = Height - 1; row >= 0; row--)
        {
            for (int column = 0; column < Width; column++)
            {
                text.Append(this[column, row] switch
                {
                    Player.Red => 'R',
                    Player.Yellow => 'Y',
                    _ => '.',
                });
            }

            if (row > 0)
            {
                text.Append('\n');
            }
        }

        return text.ToString();
    }
}
