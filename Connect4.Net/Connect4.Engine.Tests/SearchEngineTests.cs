using System.Diagnostics;
using Connect4.Engine;
using Connect4.Engine.Endgame;

namespace Connect4.Engine.Tests;

public class SearchEngineTests
{
    private static SearchEngine NewEngine(int seed = 1) =>
        new(hashLogSize: 16, endgameLogSize: EndgameTable.MinLogSize, random: new Random(seed));

    [Fact]
    public void WinningMove_IsPlayedAtOnce()
    {
        SearchResult result = NewEngine().Search(Position.FromMoves("121212"), SearchLimits.FixedDepth(8));

        Assert.Equal(0, result.Column);
        Assert.Equal(ScoreKind.Exact, result.Kind);
        Assert.Equal(Scores.Win - 7, result.Score);
    }

    [Fact]
    public void SingleNonLosingMove_BlocksWithoutSearching()
    {
        SearchResult result = NewEngine().Search(Position.FromMoves("12121"), SearchLimits.FixedDepth(8));

        Assert.Equal(0, result.Column);
        Assert.Equal(ScoreKind.None, result.Kind);
    }

    [Fact]
    public void DoubleThreat_IsALossOnTheNextMove()
    {
        SearchResult result = NewEngine().Search(Position.FromMoves("22334"), SearchLimits.FixedDepth(8));

        Assert.Equal(ScoreKind.Exact, result.Kind);
        Assert.Equal(-(Scores.Win - 7), result.Score);
    }

    [Fact]
    public void FullBoard_Throws()
    {
        Position full = FindDraw().Position;

        Assert.Throws<ArgumentException>(() => NewEngine().Search(full, SearchLimits.FixedDepth(1)));
    }

    [Fact]
    public void HeuristicSearch_ToTheEnd_GivesTheExactScore()
    {
        // With at most 8 empty cells the fixed-depth search reaches the end without the solver.
        var engine = NewEngine();
        foreach (TestPosition test in TestPosition.Load("Test_L3_R1").Where(t => t.Moves.Length >= 34).Take(100))
        {
            Position position = Position.FromMoves(test.Moves);
            SearchResult result = engine.Search(position, SearchLimits.FixedDepth(8));

            if (result.Kind != ScoreKind.None)
            {
                Assert.Equal(ScoreKind.Exact, result.Kind);
                Assert.Equal(Scores.FromSolver(test.Score, position), result.Score);
            }

            AssertOptimal(position, result.Column, test.Score);
        }
    }

    [Theory]
    [InlineData("Test_L3_R1")]
    [InlineData("Test_L2_R1")]
    public void FixedDepth_AtLeastTheEmptyCells_UsesTheSolverAndPlaysPerfectly(string file)
    {
        var engine = NewEngine();
        foreach (TestPosition test in TestPosition.Load(file).Take(50))
        {
            Position position = Position.FromMoves(test.Moves);
            SearchResult result = engine.Search(position, SearchLimits.FixedDepth(SearchLimits.MaxDepth));

            if (result.Kind != ScoreKind.None)
            {
                Assert.Equal(ScoreKind.Exact, result.Kind);
                Assert.Equal(Scores.FromSolver(test.Score, position), result.Score);
            }

            AssertOptimal(position, result.Column, test.Score);
        }
    }

    [Fact]
    public void Solve_GivesTheExactScore()
    {
        var engine = NewEngine();
        foreach (TestPosition test in TestPosition.Load("Test_L2_R1").Skip(50).Take(50))
        {
            Position position = Position.FromMoves(test.Moves);
            SearchResult result = engine.Search(position, SearchLimits.Solve);

            Assert.Equal(ScoreKind.Exact, result.Kind);
            Assert.Equal(Scores.FromSolver(test.Score, position), result.Score);
        }
    }

    [Fact]
    public void Search_FindsAWinSeveralMovesAhead()
    {
        // The side to move wins with its next-but-one disc; it has Moves / 2 discs now.
        var engine = NewEngine();
        TestPosition[] quickWins = [.. TestPosition.Load("Test_L3_R1")
            .Where(t => t.Score == EndgameSolver.MaxScore + 4 - (t.Moves.Length / 2 + 2) && HasChoice(t))
            .Take(20)];
        Assert.NotEmpty(quickWins);

        foreach (TestPosition test in quickWins)
        {
            Position position = Position.FromMoves(test.Moves);
            SearchResult result = engine.Search(position, SearchLimits.FixedDepth(4) with { EndgameThreshold = 0 });

            Assert.Equal(Scores.FromSolver(test.Score, position), result.Score);
            AssertOptimal(position, result.Column, test.Score);
        }
    }

    [Fact]
    public void FixedDepth_IsRepeatableWithTheSameSeed()
    {
        Position position = Position.FromMoves("4453");

        int[] first = [.. Enumerable.Range(0, 5).Select(_ => NewEngine(7).Search(position, SearchLimits.FixedDepth(9)).Column)];
        int[] second = [.. Enumerable.Range(0, 5).Select(_ => NewEngine(7).Search(position, SearchLimits.FixedDepth(9)).Column)];

        Assert.Equal(first, second);
    }

    [Fact]
    public void EquallyGoodMoves_ArePickedAtRandom()
    {
        // The full centre column makes the position symmetric, so the best move has an equal mirror move.
        Position position = Position.FromMoves("444444");

        var results = Enumerable.Range(1, 30)
            .Select(seed => NewEngine(seed).Search(position, SearchLimits.FixedDepth(6)))
            .ToArray();

        Assert.Single(results.Select(r => r.Score).Distinct());
        int column = results[0].Column;
        Assert.Contains(results, r => r.Column == column);
        Assert.Contains(results, r => r.Column == Position.Width - 1 - column);
    }

    [Fact]
    public void TimePerMove_StopsInTime()
    {
        var stopwatch = Stopwatch.StartNew();

        SearchResult result = NewEngine().Search(Position.Empty, SearchLimits.TimePerMove(TimeSpan.FromMilliseconds(300)));

        Assert.True(stopwatch.ElapsedMilliseconds < 1500, $"{stopwatch.ElapsedMilliseconds} ms");
        Assert.True(result.Depth > 1);
        Assert.Equal(ScoreKind.Heuristic, result.Kind);
    }

    [Fact]
    public void MoveNow_ReturnsTheBestMoveSoFar()
    {
        using var moveNow = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var stopwatch = Stopwatch.StartNew();

        SearchResult result = NewEngine().Search(
            Position.Empty, SearchLimits.TimePerMove(TimeSpan.FromSeconds(30)), moveNowToken: moveNow.Token);

        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"{stopwatch.ElapsedMilliseconds} ms");
        Assert.InRange(result.Column, 0, Position.Width - 1);
    }

    [Fact]
    public void CancelledToken_Throws()
    {
        Assert.ThrowsAny<OperationCanceledException>(() => NewEngine().Search(
            Position.Empty, SearchLimits.FixedDepth(8), cancellationToken: new CancellationToken(canceled: true)));
    }

    [Theory]
    [InlineData(42, true)]
    [InlineData(0, false)]
    public void TimeModes_UseTheSolverOnlyWithinTheThreshold(int threshold, bool solves)
    {
        // A draw early in the game: the heuristic search cannot prove it in the time, so only the solver can.
        TestPosition test = TestPosition.Load("Test_L1_R2").First(t => t.Score == 0 && HasChoice(t));
        Position position = Position.FromMoves(test.Moves);
        var progress = new ListProgress();

        SearchResult result = NewEngine().Search(
            position,
            SearchLimits.TimePerMove(TimeSpan.FromMilliseconds(300)) with { EndgameThreshold = threshold },
            progress);

        Assert.Equal(solves, progress.Items.Any(info => info.Solving));
        if (!solves)
        {
            Assert.Equal(ScoreKind.Heuristic, result.Kind);
        }
    }

    [Fact]
    public void Progress_ReportsEveryIteration()
    {
        var progress = new ListProgress();

        NewEngine().Search(Position.Empty, SearchLimits.FixedDepth(6), progress);

        Assert.Equal([1, 2, 3, 4, 5, 6], progress.Items.Select(info => info.Depth).Distinct());
        Assert.All(progress.Items, info => Assert.False(info.Solving));
    }

    [Fact]
    public void PrincipalVariation_StartsWithTheMoveAndCanBePlayed()
    {
        Position position = Position.FromMoves("4453");

        SearchResult result = NewEngine().Search(position, SearchLimits.FixedDepth(10));

        Assert.Equal(result.Column, result.PrincipalVariation[0]);
        Position current = position;
        foreach (int column in result.PrincipalVariation)
        {
            Assert.True(current.CanPlay(column));
            if (current.IsWinningMove(column))
            {
                break;
            }

            current = current.Play(column);
        }
    }

    [Theory]
    [InlineData(Player.Red)]
    [InlineData(Player.Yellow)]
    public void Strength_BeatsARandomPlayer(Player engineColour)
    {
        var random = new Random(5);
        var engine = NewEngine();
        for (int game = 0; game < 10; game++)
        {
            Player? winner = PlayGame(engine, engineColour, p => RandomMove(p, random, greedy: false));
            Assert.Equal(engineColour, winner);
        }
    }

    [Theory]
    [InlineData(Player.Red)]
    [InlineData(Player.Yellow)]
    public void Strength_BeatsAGreedyPlayer(Player engineColour)
    {
        var random = new Random(6);
        var engine = NewEngine();
        int wins = 0;
        for (int game = 0; game < 10; game++)
        {
            Player? winner = PlayGame(engine, engineColour, p => RandomMove(p, random, greedy: true));
            Assert.NotEqual(engineColour.Opponent(), winner);
            wins += winner == engineColour ? 1 : 0;
        }

        Assert.True(wins >= 9, $"{wins} of 10");
    }

    /// <summary>The side to move cannot win at once and has more than one move that does not lose at once.</summary>
    private static bool HasChoice(TestPosition test)
    {
        Position position = Position.FromMoves(test.Moves);
        return !position.CanWinNext && ulong.PopCount(position.NonLosingMoves()) > 1;
    }

    /// <summary>The move keeps the exact score: it wins at once, or the solver gives the child the negated score.</summary>
    private static void AssertOptimal(Position position, int column, int score)
    {
        if (position.IsWinningMove(column))
        {
            Assert.Equal((Position.CellCount + 1 - position.Moves) / 2, score);
            return;
        }

        int child = new EndgameSolver(EndgameTable.MinLogSize).Solve(position.Play(column));
        Assert.Equal(score, -child);
    }

    private static Player? PlayGame(SearchEngine engine, Player engineColour, Func<Position, int> opponent)
    {
        var game = new Game();
        engine.ClearHash();
        while (!game.IsGameOver)
        {
            int column = game.ToMove == engineColour
                ? engine.Search(game.Position, SearchLimits.FixedDepth(6)).Column
                : opponent(game.Position);
            game.Play(column);
        }

        return game.Winner;
    }

    /// <summary>A random move; a greedy player also wins at once and blocks an immediate threat when it can.</summary>
    private static int RandomMove(Position position, Random random, bool greedy)
    {
        int[] playable = [.. Enumerable.Range(0, Position.Width).Where(position.CanPlay)];
        if (greedy)
        {
            int[] wins = [.. playable.Where(position.IsWinningMove)];
            if (wins.Length > 0)
            {
                return wins[0];
            }

            ulong threats = position.WinningCells(position.ToMove.Opponent()) & position.Possible;
            if (threats != 0)
            {
                return Position.ColumnOf(threats & (0 - threats));
            }
        }

        return playable[random.Next(playable.Length)];
    }

    private static Game FindDraw()
    {
        var random = new Random(1);
        while (true)
        {
            var game = new Game();
            while (!game.IsGameOver)
            {
                int[] columns = [.. Enumerable.Range(0, Position.Width).Where(game.CanPlay)];
                game.Play(columns[random.Next(columns.Length)]);
            }

            if (game.IsDraw)
            {
                return game;
            }
        }
    }

    private sealed class ListProgress : IProgress<SearchInfo>
    {
        public List<SearchInfo> Items { get; } = [];

        public void Report(SearchInfo value) => Items.Add(value);
    }
}
