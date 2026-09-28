using Connect4.Engine;
using Connect4.Engine.Endgame;
using Connect4.Engine.Search;

namespace Connect4.Engine.Tests;

public class TimeControlAndScoresTests
{
    [Fact]
    public void TimePerMove_SoftLimitIsTwoThirds()
    {
        TimeBudget budget = TimeControl.For(SearchLimits.TimePerMove(TimeSpan.FromSeconds(3)), 30);

        Assert.Equal(new TimeBudget(2000, 3000), budget);
    }

    [Fact]
    public void TimePerGame_SharesTheTimeOverTheComputersRemainingMoves()
    {
        // 30 empty cells: at most 15 more moves for the computer.
        TimeBudget budget = TimeControl.For(SearchLimits.TimePerGame(TimeSpan.FromSeconds(30)), 30);

        Assert.Equal(new TimeBudget(1333, 2000), budget);
    }

    [Fact]
    public void TimePerGame_ClockUsedUp_StillGivesAMinimum()
    {
        TimeBudget budget = TimeControl.For(SearchLimits.TimePerGame(TimeSpan.FromSeconds(-5)), 30);

        Assert.True(budget.HardMs > 0);
    }

    [Fact]
    public void FixedDepth_HasNoTimeLimit()
    {
        Assert.Equal(TimeBudget.Unlimited, TimeControl.For(SearchLimits.FixedDepth(8), 30));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(43)]
    public void FixedDepth_OutOfRange_Throws(int depth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SearchLimits.FixedDepth(depth));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(43)]
    public void EndgameThreshold_OutOfRange_Throws(int threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SearchLimits.FixedDepth(8) with { EndgameThreshold = threshold });
    }

    [Fact]
    public void EndgameThreshold_DefaultsTo24()
    {
        Assert.Equal(30, SearchLimits.TimePerMove(TimeSpan.FromSeconds(1)).EndgameThreshold);
    }

    [Fact]
    public void FromSolver_ImmediateWin_IsTheNextMove()
    {
        Position position = Position.FromMoves("121212");
        int solver = new EndgameSolver(EndgameTable.MinLogSize).Solve(position);

        Assert.Equal(Scores.Win - 7, Scores.FromSolver(solver, position));
    }

    [Theory]
    [InlineData(0, 18, 7)]   // Red to move, wins with its 4th disc: move 7
    [InlineData(1, 18, 8)]   // Yellow to move, wins with its 4th disc: move 8
    [InlineData(0, -18, 8)]  // Red to move, Yellow wins with its 4th disc
    [InlineData(1, 1, 42)]   // Yellow wins with its 21st disc: the last move
    [InlineData(0, 1, 41)]   // Red wins with its 21st disc
    public void FromSolver_ConvertsToTheWinningMoveNumber(int moves, int solverScore, int moveNumber)
    {
        Position position = moves == 0 ? Position.Empty : Position.FromMoves("4");

        int score = Scores.FromSolver(solverScore, position);

        Assert.Equal(solverScore > 0 ? Scores.Win - moveNumber : -(Scores.Win - moveNumber), score);
        Assert.Equal(moveNumber, Scores.WinningMoveNumber(score));
        Assert.True(Scores.IsDecided(score));
    }

    [Fact]
    public void FromSolver_Draw_IsZero()
    {
        Assert.Equal(0, Scores.FromSolver(0, Position.Empty));
    }
}
