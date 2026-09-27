using Connect4.Engine;
using Connect4.Engine.Book;

namespace Connect4.Engine.Tests;

public class OpeningBookTests
{
    // Any score that is the same for a position and its mirror image.
    private static int FakeSolve(Position position) => (int)(position.CanonicalKey % 37) - 18;

    [Fact]
    public void Mirror_ReversesTheColumns()
    {
        Position position = Position.FromMoves("11234");

        Assert.Equal(Position.FromMoves("77654"), position.Mirror());
        Assert.Equal(position, position.Mirror().Mirror());
        Assert.Equal(Position.FromMoves("4444"), Position.FromMoves("4444").Mirror());
    }

    [Fact]
    public void CanonicalKey_IsTheSameForMirrorImagesOnly()
    {
        Assert.Equal(Position.FromMoves("1123").CanonicalKey, Position.FromMoves("7765").CanonicalKey);
        Assert.NotEqual(Position.FromMoves("12").CanonicalKey, Position.FromMoves("13").CanonicalKey);
        Assert.Equal(Position.FromMoves("12").Key, Position.FromMoves("12").CanonicalKey);
    }

    [Fact]
    public void Enumerate_CountsUniquePositionsPerPly()
    {
        IReadOnlyList<string>[] levels = BookBuilder.Enumerate(6);
        int[] expected = [1, 4, 25, 121, 568, 2144, 8231];

        Assert.Equal(expected, levels.Select(level => level.Count));
    }

    [Fact]
    public void Enumerate_LevelsAreSortedWithEveryPositionOnce()
    {
        IReadOnlyList<string>[] levels = BookBuilder.Enumerate(5);

        Assert.All(levels, level =>
        {
            Assert.Equal(level.Order(StringComparer.Ordinal), level);
            Assert.Equal(level.Count, level.Select(moves => Position.FromMoves(moves).CanonicalKey).Distinct().Count());
        });
    }

    [Fact]
    public void Enumerate_LeavesOutGamesThatHaveEnded()
    {
        IReadOnlyList<string>[] levels = BookBuilder.Enumerate(7);

        Assert.Contains("121212", levels[6]);
        Assert.DoesNotContain("1212121", levels[7]);
        Assert.DoesNotContain("7676767", levels[7]);
    }

    [Fact]
    public void BackUp_WithFakeSolver_IsTheNegamaxOfTheLeaves()
    {
        const int depth = 3;
        List<BookLine> lines = BookBuilder.Build(depth, FakeSolve);

        Assert.Equal(1 + 4 + 25 + 121, lines.Count);
        Assert.Equal("", lines[0].Moves);
        Assert.All(lines, line => Assert.Equal(Negamax(Position.FromMoves(line.Moves), depth), line.Score));
    }

    [Fact]
    public void BackUp_WinningMove_ScoresTheFastestWin()
    {
        OpeningBook book = OpeningBook.FromLines(BookBuilder.Build(7, FakeSolve));

        Assert.True(book.TryGetScore(Position.FromMoves("121212"), out int score));
        Assert.Equal(18, score);
        Assert.True(book.TryGetScore(Position.FromMoves("767676"), out score));
        Assert.Equal(18, score);
    }

    [Fact]
    public void BackUp_MissingLeaf_Throws()
    {
        IReadOnlyList<string>[] levels = BookBuilder.Enumerate(2);
        var scores = levels[2].Skip(1).ToDictionary(moves => moves, _ => 0);

        Assert.Throws<ArgumentException>(() => BookBuilder.BackUp(levels, scores));
    }

    [Fact]
    public void BookFile_WriteAndRead_RoundTrip()
    {
        List<BookLine> lines = [new("", 1), new("4", -1), new("44", 1), new("4453", -2)];
        var writer = new StringWriter();

        BookFile.Write(writer, lines, "Test book\nsecond line");
        string text = writer.ToString();

        Assert.StartsWith("# Test book\n# second line\n 1\n4 -1\n", text);
        Assert.Equal(lines, BookFile.Read(new StringReader(text)));
    }

    [Theory]
    [InlineData("44")]
    [InlineData("44x 1")]
    [InlineData("44 19")]
    [InlineData("44 one")]
    [InlineData("1111111 0")]
    [InlineData("1212121 0")]
    public void BookFile_InvalidLine_Throws(string line)
    {
        Assert.Throws<FormatException>(() => BookFile.Parse(line));
    }

    [Fact]
    public void OpeningBook_LooksUpMirrorImagesUpToItsDepth()
    {
        OpeningBook book = OpeningBook.FromLines(BookBuilder.Build(2, FakeSolve));

        Assert.Equal(2, book.Depth);
        Assert.Equal(1 + 4 + 25, book.Count);
        Assert.True(book.TryGetScore(Position.FromMoves("12"), out int score));
        Assert.True(book.TryGetScore(Position.FromMoves("76"), out int mirrored));
        Assert.Equal(score, mirrored);
        Assert.False(book.TryGetScore(Position.FromMoves("123"), out _));
    }

    [Fact]
    public void TryGetMove_PlaysABestMove()
    {
        const int depth = 3;
        OpeningBook book = OpeningBook.FromLines(BookBuilder.Build(depth, FakeSolve));
        var random = new Random(1);

        foreach (string moves in (string[])["", "4", "43", "11", "21"])
        {
            Position position = Position.FromMoves(moves);
            Assert.True(book.TryGetMove(position, random, out int column, out int score));
            Assert.Equal(Negamax(position, depth), score);
            Assert.Equal(score, -Negamax(position.Play(column), depth));
        }
    }

    [Fact]
    public void TryGetMove_EqualMoves_ArePickedAtRandomWithASeed()
    {
        OpeningBook book = OpeningBook.FromLines(BookBuilder.Build(2, _ => 0));

        var picked = Enumerable.Range(0, 100)
            .Select(seed => book.TryGetMove(Position.Empty, new Random(seed), out int column, out _) ? column : -1)
            .ToList();

        Assert.Equal(Enumerable.Range(0, Position.Width), picked.Distinct().Order());
        book.TryGetMove(Position.Empty, new Random(5), out int first, out _);
        book.TryGetMove(Position.Empty, new Random(5), out int second, out _);
        Assert.Equal(first, second);
    }

    [Fact]
    public void TryGetMove_AtTheBookDepth_ReturnsFalse()
    {
        OpeningBook book = OpeningBook.FromLines(BookBuilder.Build(2, FakeSolve));

        Assert.True(book.TryGetMove(Position.FromMoves("4"), new Random(1), out _, out _));
        Assert.False(book.TryGetMove(Position.FromMoves("44"), new Random(1), out int column, out _));
        Assert.Equal(-1, column);
    }

    [Fact]
    public void FromLines_MirrorLines_MustAgree()
    {
        OpeningBook book = OpeningBook.FromLines([new("1", 2), new("7", 2)]);
        Assert.Equal(1, book.Count);

        Assert.Throws<FormatException>(() => OpeningBook.FromLines([new("1", 2), new("7", 1)]));
    }

    [Fact]
    public void Load_ReadsTheTextFormat()
    {
        var stream = new MemoryStream("# comment\n 1\n4 -1\n"u8.ToArray());

        OpeningBook book = OpeningBook.Load(stream);

        Assert.Equal(1, book.Depth);
        Assert.True(book.TryGetScore(Position.FromMoves("4"), out int score));
        Assert.Equal(-1, score);
    }

    [Fact]
    public void CheckBackUp_FindsAWrongScore()
    {
        List<BookLine> lines = BookBuilder.Build(2, FakeSolve);
        Assert.Empty(BookBuilder.CheckBackUp(lines));

        int index = lines.FindIndex(line => line.Moves == "");
        lines[index] = lines[index] with { Score = lines[index].Score + 1 };

        BookLine wrong = Assert.Single(BookBuilder.CheckBackUp(lines));
        Assert.Equal("", wrong.Moves);
    }

    private static int Negamax(Position position, int depth)
    {
        if (position.Moves == depth)
        {
            return FakeSolve(position);
        }

        int best = int.MinValue;
        for (int column = 0; column < Position.Width; column++)
        {
            if (position.CanPlay(column))
            {
                best = Math.Max(best, position.IsWinningMove(column)
                    ? (Position.CellCount + 1 - position.Moves) / 2
                    : -Negamax(position.Play(column), depth));
            }
        }

        return best;
    }
}
