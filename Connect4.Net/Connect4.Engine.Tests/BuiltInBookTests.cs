using Connect4.Engine;
using Connect4.Engine.Book;
using Connect4.Engine.Endgame;

namespace Connect4.Engine.Tests;

/// <summary>The book built into the engine (Book/OpeningBook.txt, made by Connect4.Tools) and its use in the search.</summary>
public class BuiltInBookTests
{
    private static OpeningBook Book => OpeningBook.Default;

    public static TheoryData<string> OpeningTestSets => ["Test_L1_R1", "Test_L1_R2", "Test_L1_R3"];

    [Fact]
    public void Default_HasEveryPositionOfTheFirstSixPlies()
    {
        Assert.Equal(6, Book.Depth);
        Assert.Equal(11_094, Book.Count);
    }

    [Fact]
    public void Default_HasTheKnownFirstMoveScores()
    {
        int[] expected = [-2, -1, 0, 1, 0, -1, -2];

        Assert.True(Book.TryGetScore(Position.Empty, out int root));
        Assert.Equal(1, root);
        Assert.Equal(expected, Enumerable.Range(0, Position.Width).Select(column =>
            Book.TryGetScore(Position.Empty.Play(column), out int score) ? -score : int.MinValue));
    }

    [Fact]
    public void Default_ShallowerScoresAreTheBackUpOfTheirChildren()
    {
        Assert.Empty(BookBuilder.CheckBackUp(ReadLines()));
    }

    [Theory]
    [MemberData(nameof(OpeningTestSets))]
    public void Default_AgreesWithThePositionsOfTheTestSets(string file)
    {
        TestPosition[] tests = TestPosition.Load(file).Where(test => test.Moves.Length <= Book.Depth).ToArray();

        Assert.NotEmpty(tests);
        Assert.All(tests, test =>
        {
            Assert.True(Book.TryGetScore(Position.FromMoves(test.Moves), out int score), test.Moves);
            Assert.Equal(test.Score, score);
        });
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void Default_SampleOfLeaves_SolvesToTheSameScore()
    {
        var solver = new EndgameSolver();
        var random = new Random(1);
        List<BookLine> leaves = ReadLines().Where(line => line.Moves.Length == Book.Depth).ToList();

        foreach (BookLine leaf in leaves.OrderBy(_ => random.Next()).Take(50))
        {
            Assert.Equal(leaf.Score, solver.Solve(Position.FromMoves(leaf.Moves)));
        }
    }

    [Fact]
    public void Search_EmptyBoard_PlaysTheCentreFromTheBookAtOnce()
    {
        SearchResult result = NewEngine().Search(Position.Empty, SearchLimits.TimePerMove(TimeSpan.FromSeconds(5)));

        Assert.True(result.FromBook);
        Assert.Equal(3, result.Column);
        Assert.Equal(ScoreKind.Exact, result.Kind);
        Assert.Equal(Scores.Win - 41, result.Score);
        Assert.Equal(0, result.Nodes);
        Assert.True(result.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Search_EqualBookMoves_ArePickedAtRandom()
    {
        // A position where the book has more than one best move.
        Position position = BookBuilder.Enumerate(Book.Depth - 1).SelectMany(level => level)
            .Select(Position.FromMoves)
            .First(p => Book.TryGetBackedUpScore(p, out _, out int best) && (best & (best - 1)) != 0);
        Book.TryGetBackedUpScore(position, out int score, out int bestColumns);

        var columns = Enumerable.Range(0, 30)
            .Select(seed => NewEngine(seed).Search(position, SearchLimits.FixedDepth(8)))
            .Select(result =>
            {
                Assert.True(result.FromBook);
                Assert.Equal(Scores.FromSolver(score, position), result.Score);
                return result.Column;
            })
            .Distinct()
            .ToList();

        Assert.True(columns.Count > 1);
        Assert.All(columns, column => Assert.NotEqual(0, bestColumns & 1 << column));
    }

    [Fact]
    public void Search_UseBookOff_Searches()
    {
        SearchResult result = NewEngine().Search(Position.Empty, SearchLimits.FixedDepth(4) with { UseBook = false });

        Assert.False(result.FromBook);
        Assert.Equal(ScoreKind.Heuristic, result.Kind);
    }

    [Fact]
    public void Search_AtTheBookDepth_Searches()
    {
        SearchResult result = NewEngine().Search(Position.FromMoves("444444"), SearchLimits.FixedDepth(4));

        Assert.False(result.FromBook);
        Assert.Equal(4, result.Depth);
    }

    private static SearchEngine NewEngine(int seed = 1) =>
        new(hashLogSize: 16, endgameLogSize: EndgameTable.MinLogSize, random: new Random(seed));

    private static List<BookLine> ReadLines()
    {
        using Stream stream = typeof(OpeningBook).Assembly.GetManifestResourceStream("Connect4.Engine.Book.OpeningBook.txt")!;
        using var reader = new StreamReader(stream);
        return BookFile.Read(reader);
    }
}
