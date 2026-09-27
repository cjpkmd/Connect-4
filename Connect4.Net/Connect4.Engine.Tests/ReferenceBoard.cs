using Connect4.Engine;

namespace Connect4.Engine.Tests;

/// <summary>A slow, obvious grid board used to check the bitboard code in <see cref="Position"/>.</summary>
internal sealed class ReferenceBoard
{
    private readonly Player?[,] _cells = new Player?[Position.Width, Position.Height];
    private readonly int[] _heights = new int[Position.Width];

    public int Moves { get; private set; }

    public Player ToMove => Moves % 2 == 0 ? Player.Red : Player.Yellow;

    public bool CanPlay(int column) => _heights[column] < Position.Height;

    public Player? this[int column, int row] => _cells[column, row];

    public void Play(int column)
    {
        _cells[column, _heights[column]] = ToMove;
        _heights[column]++;
        Moves++;
    }

    public void Undo(int column)
    {
        _heights[column]--;
        _cells[column, _heights[column]] = null;
        Moves--;
    }

    public bool IsWinningMove(int column) => CanPlay(column) && MakesFour(column, _heights[column], ToMove);

    /// <summary>Every empty cell (playable or not) where a disc of the player completes a four.</summary>
    public ulong WinningCells(Player player)
    {
        ulong cells = 0;
        for (int column = 0; column < Position.Width; column++)
        {
            for (int row = 0; row < Position.Height; row++)
            {
                if (_cells[column, row] is null && MakesFour(column, row, player))
                {
                    cells |= Position.CellBit(column, row);
                }
            }
        }

        return cells;
    }

    /// <summary>Columns whose move does not let the opponent win on the next move.</summary>
    public ulong NonLosingMoves()
    {
        ulong moves = 0;
        for (int column = 0; column < Position.Width; column++)
        {
            if (!CanPlay(column))
            {
                continue;
            }

            int row = _heights[column];
            Play(column);
            bool opponentWins = Enumerable.Range(0, Position.Width).Any(IsWinningMove);
            Undo(column);

            if (!opponentWins)
            {
                moves |= Position.CellBit(column, row);
            }
        }

        return moves;
    }

    private bool MakesFour(int column, int row, Player player)
    {
        foreach ((int dc, int dr) in (ReadOnlySpan<(int, int)>)[(0, 1), (1, 0), (1, 1), (1, -1)])
        {
            int count = 1 + Count(column, row, dc, dr, player) + Count(column, row, -dc, -dr, player);
            if (count >= 4)
            {
                return true;
            }
        }

        return false;
    }

    private int Count(int column, int row, int dc, int dr, Player player)
    {
        int count = 0;
        for (int c = column + dc, r = row + dr;
             c is >= 0 and < Position.Width && r is >= 0 and < Position.Height && _cells[c, r] == player;
             c += dc, r += dr)
        {
            count++;
        }

        return count;
    }
}
