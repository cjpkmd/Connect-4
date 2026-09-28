using System.Globalization;
using Connect4.Engine.Endgame;

namespace Connect4.Engine.Book;

/// <summary>A book position: the moves that lead to it and its exact score for the side to move (C++ convention).</summary>
public readonly record struct BookLine(string Moves, int Score);

/// <summary>
/// The opening book text format, as the files in "Test positions/": one "&lt;moves&gt; &lt;score&gt;" per line
/// (the empty board is " 1"). Lines starting with '#' and empty lines are ignored.
/// </summary>
public static class BookFile
{
    public static List<BookLine> Read(TextReader reader) => [.. ReadWithPositions(reader).Select(entry => entry.Line)];

    internal static IEnumerable<(BookLine Line, Position Position)> ReadWithPositions(TextReader reader)
    {
        int number = 0;
        while (reader.ReadLine() is { } text)
        {
            number++;
            if (text.Length > 0 && text[0] != '#')
            {
                yield return ParseWithPosition(text, number);
            }
        }
    }

    /// <exception cref="FormatException">The moves cannot be played or the score is not a valid solver score.</exception>
    public static BookLine Parse(string text, int lineNumber = 0) => ParseWithPosition(text, lineNumber).Line;

    private static (BookLine Line, Position Position) ParseWithPosition(string text, int lineNumber)
    {
        string[] parts = text.Split(' ');
        if (parts.Length != 2
            || !int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int score)
            || score is < EndgameSolver.MinScore or > EndgameSolver.MaxScore)
        {
            throw new FormatException($"Line {lineNumber}: expected \"<moves> <score>\", found \"{text}\".");
        }

        Position position;
        try
        {
            position = Position.FromMoves(parts[0]);
        }
        catch (FormatException e)
        {
            throw new FormatException($"Line {lineNumber}: {e.Message}", e);
        }

        return (new BookLine(parts[0], score), position);
    }

    /// <param name="comment">Written first, each line prefixed with "# ".</param>
    public static void Write(TextWriter writer, IEnumerable<BookLine> lines, string? comment = null)
    {
        if (comment is not null)
        {
            foreach (string line in comment.Split('\n'))
            {
                writer.Write("# ");
                writer.Write(line);
                writer.Write('\n');
            }
        }

        foreach (BookLine line in lines)
        {
            writer.Write(Format(line));
            writer.Write('\n');
        }
    }

    public static string Format(BookLine line) =>
        line.Moves + " " + line.Score.ToString(CultureInfo.InvariantCulture);
}
