using Connect4.Engine;

namespace Connect4.Engine.Tests;

/// <summary>
/// Counts the move sequences of a given length from the empty board. A winning move ends the game,
/// so it is counted only when it is the last move of the sequence.
/// </summary>
public class PerftTests
{
    [Theory]
    [InlineData(1, 7L)]
    [InlineData(2, 49L)]
    [InlineData(3, 343L)]
    [InlineData(4, 2_401L)]
    [InlineData(5, 16_807L)]
    [InlineData(6, 117_649L)]
    [InlineData(7, 823_536L)] // 7^7 minus the 7 sequences that fill one column in the first 6 moves
    public void Perft_KnownValues(int depth, long expected)
    {
        Assert.Equal(expected, Perft(Position.Empty, depth));
    }

    [Theory]
    [InlineData(8)]
    public void Perft_MatchesTheReferenceBoard(int depth)
    {
        Assert.Equal(ReferencePerft(new ReferenceBoard(), depth), Perft(Position.Empty, depth));
    }

    private static long Perft(Position position, int depth)
    {
        if (depth == 0)
        {
            return 1;
        }

        long count = 0;
        for (int column = 0; column < Position.Width; column++)
        {
            if (!position.CanPlay(column))
            {
                continue;
            }

            if (position.IsWinningMove(column))
            {
                count += depth == 1 ? 1 : 0;
                continue;
            }

            count += Perft(position.Play(column), depth - 1);
        }

        return count;
    }

    private static long ReferencePerft(ReferenceBoard board, int depth)
    {
        if (depth == 0)
        {
            return 1;
        }

        long count = 0;
        for (int column = 0; column < Position.Width; column++)
        {
            if (!board.CanPlay(column))
            {
                continue;
            }

            if (board.IsWinningMove(column))
            {
                count += depth == 1 ? 1 : 0;
                continue;
            }

            board.Play(column);
            count += ReferencePerft(board, depth - 1);
            board.Undo(column);
        }

        return count;
    }
}
