using Connect4.Engine;
using Connect4.Engine.Endgame;

namespace Connect4.Engine.Tests;

/// <summary>Exact scores of Pascal Pons' test sets. The slow sets run only with <c>dotnet test --filter Category=Slow</c>.</summary>
public class EndgameSolverTests
{
    // 2^20 entries (5 MB) are plenty for the normal sets and quick to allocate.
    private const int SmallLogSize = 20;

    [Theory]
    [InlineData("Test_L3_R1", 1000)]
    [InlineData("Test_L2_R1", 1000)]
    [InlineData("Test_L2_R2", 100)]
    public void Solve_TestSet_GivesTheExactScore(string file, int count)
    {
        AssertExactScores(new EndgameSolver(SmallLogSize), file, count);
    }

    [Theory]
    [Trait("Category", "Slow")]
    [InlineData("Test_L2_R2")]
    [InlineData("Test_L1_R1")]
    [InlineData("Test_L1_R2")]
    [InlineData("Test_L1_R3")] // about 40 minutes
    public void Solve_SlowTestSet_GivesTheExactScore(string file)
    {
        AssertExactScores(new EndgameSolver(), file, 1000);
    }

    [Theory]
    [InlineData("Test_L3_R1")]
    [InlineData("Test_L2_R1")]
    public void WeakSolve_GivesTheSignOfTheScore(string file)
    {
        var solver = new EndgameSolver(SmallLogSize);
        foreach (TestPosition test in TestPosition.Load(file).Take(200))
        {
            Assert.Equal(Math.Sign(test.Score), Math.Sign(solver.Solve(Position.FromMoves(test.Moves), weak: true)));
        }
    }

    [Fact]
    public void Analyze_BestColumnHasThePositionScore()
    {
        var solver = new EndgameSolver(SmallLogSize);
        foreach (TestPosition test in TestPosition.Load("Test_L3_R1").Take(100))
        {
            Position position = Position.FromMoves(test.Moves);
            int[] scores = solver.Analyze(position);

            Assert.Equal(test.Score, scores.Max());
            for (int column = 0; column < Position.Width; column++)
            {
                Assert.Equal(!position.CanPlay(column), scores[column] == EndgameSolver.InvalidMove);
            }
        }
    }

    [Fact]
    public void Solve_ImmediateWin_ScoresTheFastestWin()
    {
        Assert.Equal(EndgameSolver.MaxScore, new EndgameSolver(SmallLogSize).Solve(Position.FromMoves("121212")));
    }

    [Fact]
    public void Solve_CancelledToken_ThrowsAndTheSolverStaysUsable()
    {
        var solver = new EndgameSolver(SmallLogSize);

        Assert.ThrowsAny<OperationCanceledException>(
            () => solver.Solve(Position.Empty, cancellation: new CancellationToken(canceled: true)));

        TestPosition test = TestPosition.Load("Test_L2_R1")[0];
        Assert.Equal(test.Score, solver.Solve(Position.FromMoves(test.Moves)));
    }

    [Fact]
    public void Solve_TimeLimitReached_Throws()
    {
        var solver = new EndgameSolver(SmallLogSize);

        Assert.ThrowsAny<OperationCanceledException>(() => solver.Solve(Position.Empty, timeLimit: TimeSpan.FromMilliseconds(20)));
    }

    [Fact]
    public void Reset_ClearsTheNodeCount()
    {
        var solver = new EndgameSolver(SmallLogSize);
        solver.Solve(Position.FromMoves(TestPosition.Load("Test_L2_R1")[0].Moves));
        Assert.True(solver.NodeCount > 0);

        solver.Reset();

        Assert.Equal(0, solver.NodeCount);
    }

    private static void AssertExactScores(EndgameSolver solver, string file, int count)
    {
        foreach (TestPosition test in TestPosition.Load(file).Take(count))
        {
            int score = solver.Solve(Position.FromMoves(test.Moves));
            Assert.True(test.Score == score, $"{test.Moves}: expected {test.Score}, got {score}.");
        }
    }
}
