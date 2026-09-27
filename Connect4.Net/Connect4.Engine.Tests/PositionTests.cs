using Connect4.Engine;

namespace Connect4.Engine.Tests;

public class PositionTests
{
    [Fact]
    public void EmptyPosition_HasNoDiscsAndRedToMove()
    {
        Position position = Position.Empty;

        Assert.Equal(0, position.Moves);
        Assert.Equal(Position.CellCount, position.EmptyCells);
        Assert.Equal(Player.Red, position.ToMove);
        Assert.False(position.IsFull);
        Assert.All(Enumerable.Range(0, Position.Width), column => Assert.True(position.CanPlay(column)));
        Assert.Equal(0UL, position.Discs(Player.Red) | position.Discs(Player.Yellow));
    }

    [Fact]
    public void Play_DropsDiscsToTheBottomAndAlternatesPlayers()
    {
        Position position = Position.Empty.Play(3).Play(3).Play(4);

        Assert.Equal(Player.Red, position[3, 0]);
        Assert.Equal(Player.Yellow, position[3, 1]);
        Assert.Equal(Player.Red, position[4, 0]);
        Assert.Null(position[3, 2]);
        Assert.Equal(Player.Yellow, position.ToMove);
        Assert.Equal(
            ".......\n.......\n.......\n.......\n...Y...\n...RR..",
            position.ToString());
    }

    [Fact]
    public void Play_FullColumn_Throws()
    {
        Position position = Position.FromMoves("111111");

        Assert.False(position.CanPlay(0));
        Assert.Throws<InvalidOperationException>(() => position.Play(0));
    }

    [Fact]
    public void Play_ColumnOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Position.Empty.Play(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => Position.Empty.Play(-1));
    }

    [Theory]
    [InlineData("121212", 0)] // vertical
    [InlineData("112233", 3)] // horizontal
    [InlineData("1223343474", 3)] // diagonal /
    [InlineData("7665545414", 3)] // diagonal \
    public void IsWinningMove_FindsFourInEveryDirection(string moves, int column)
    {
        Position position = Position.FromMoves(moves);

        Assert.True(position.CanWinNext);
        Assert.True(position.IsWinningMove(column));
        Assert.Equal(Position.ColumnOf(position.WinningCells(position.ToMove) & position.Possible), column);
    }

    [Fact]
    public void NonLosingMoves_SingleThreat_MustBlock()
    {
        Position position = Position.FromMoves("12121");

        Assert.Equal(Position.CellBit(0, 3), position.NonLosingMoves());
    }

    [Fact]
    public void NonLosingMoves_DoubleThreat_EveryMoveLoses()
    {
        Position position = Position.FromMoves("22334");

        Assert.False(position.CanWinNext);
        Assert.Equal(0UL, position.NonLosingMoves());
    }

    [Fact]
    public void Key_IsTheSameForTranspositionsAndDiffersOtherwise()
    {
        Assert.Equal(Position.FromMoves("1234").Key, Position.FromMoves("3214").Key);
        Assert.NotEqual(Position.FromMoves("1234").Key, Position.FromMoves("1243").Key);
        Assert.NotEqual(Position.Empty.Key, Position.FromMoves("1").Key);
    }

    [Fact]
    public void FindFours_ReturnsTheDiscsOfTheLine()
    {
        ulong line = Position.CellBit(2, 0) | Position.CellBit(3, 1) | Position.CellBit(4, 2) | Position.CellBit(5, 3);
        ulong discs = line | Position.CellBit(0, 0) | Position.CellBit(1, 0);

        Assert.Equal(line, Position.FindFours(discs));
        Assert.Equal(0UL, Position.FindFours(discs & ~Position.CellBit(5, 3)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("8")]
    [InlineData("12a")]
    [InlineData("1111111")] // column full
    [InlineData("1212121")] // last move makes four
    public void FromMoves_InvalidSequence_Throws(string moves)
    {
        Assert.Throws<FormatException>(() => Position.FromMoves(moves));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RandomGames_MatchTheReferenceBoard(int seed)
    {
        var random = new Random(seed);
        for (int game = 0; game < 500; game++)
        {
            Position position = Position.Empty;
            var reference = new ReferenceBoard();

            while (true)
            {
                AssertSame(reference, position);

                int[] moves = Enumerable.Range(0, Position.Width)
                    .Where(column => position.CanPlay(column) && !position.IsWinningMove(column))
                    .ToArray();
                if (moves.Length == 0)
                {
                    break;
                }

                int move = moves[random.Next(moves.Length)];
                position = position.Play(move);
                reference.Play(move);
            }
        }
    }

    private static void AssertSame(ReferenceBoard reference, Position position)
    {
        Assert.Equal(reference.Moves, position.Moves);
        Assert.Equal(reference.ToMove, position.ToMove);
        for (int column = 0; column < Position.Width; column++)
        {
            Assert.Equal(reference.CanPlay(column), position.CanPlay(column));
            Assert.Equal(reference.IsWinningMove(column), position.CanPlay(column) && position.IsWinningMove(column));
            for (int row = 0; row < Position.Height; row++)
            {
                Assert.Equal(reference[column, row], position[column, row]);
            }
        }

        Assert.Equal(reference.WinningCells(Player.Red), position.WinningCells(Player.Red));
        Assert.Equal(reference.WinningCells(Player.Yellow), position.WinningCells(Player.Yellow));
        Assert.Equal(Enumerable.Range(0, Position.Width).Any(reference.IsWinningMove), position.CanWinNext);
        if (!position.CanWinNext)
        {
            Assert.Equal(reference.NonLosingMoves(), position.NonLosingMoves());
        }
    }
}
