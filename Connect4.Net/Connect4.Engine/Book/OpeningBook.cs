using System.Numerics;

namespace Connect4.Engine.Book;

/// <summary>Exact scores (C++ convention, side to move) of the opening positions, looked up by <see cref="Position.CanonicalKey"/>.</summary>
public sealed class OpeningBook
{
    private const int ScoreBits = 8;
    private const int ScoreOffset = 64;
    private const string ResourceName = "Connect4.Engine.Book.OpeningBook.txt";

    private static readonly Lazy<OpeningBook> s_default = new(LoadDefault);

    // CanonicalKey << ScoreBits | (score + ScoreOffset), sorted.
    private readonly ulong[] _entries;

    private OpeningBook(ulong[] entries, int depth)
    {
        _entries = entries;
        Depth = depth;
    }

    /// <summary>The book built into the engine (Book/OpeningBook.txt), loaded the first time it is used.</summary>
    public static OpeningBook Default => s_default.Value;

    /// <summary>The largest number of discs of a book position; −1 for an empty book.</summary>
    public int Depth { get; }

    public int Count => _entries.Length;

    /// <exception cref="FormatException">A line is invalid, or a position has two different scores.</exception>
    public static OpeningBook Load(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return Create(BookFile.ReadWithPositions(reader));
    }

    /// <exception cref="FormatException">A position has two different scores.</exception>
    public static OpeningBook FromLines(IEnumerable<BookLine> lines) =>
        Create(lines.Select(line => (line, Position.FromMoves(line.Moves))));

    private static OpeningBook Create(IEnumerable<(BookLine Line, Position Position)> lines)
    {
        var entries = new List<ulong>();
        int depth = -1;
        foreach ((BookLine line, Position position) in lines)
        {
            entries.Add(position.CanonicalKey << ScoreBits | (byte)(line.Score + ScoreOffset));
            depth = Math.Max(depth, position.Moves);
        }

        entries.Sort();
        var unique = new List<ulong>(entries.Count);
        foreach (ulong entry in entries)
        {
            if (unique.Count > 0 && unique[^1] >> ScoreBits == entry >> ScoreBits)
            {
                if (unique[^1] != entry)
                {
                    throw new FormatException("The book has two different scores for the same position.");
                }

                continue;
            }

            unique.Add(entry);
        }

        return new OpeningBook([.. unique], depth);
    }

    private static OpeningBook LoadDefault()
    {
        using Stream stream = typeof(OpeningBook).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The resource {ResourceName} is missing.");
        return Load(stream);
    }

    public bool TryGetScore(Position position, out int score)
    {
        if (position.Moves > Depth)
        {
            score = 0;
            return false;
        }

        return TryGet(position.CanonicalKey, out score);
    }

    /// <summary>A best move from the book, picked at random among moves with the same score.</summary>
    /// <param name="score">The score of <paramref name="position"/>, as <see cref="TryGetScore"/>.</param>
    /// <returns>False if the position has <see cref="Depth"/> discs or more, or a child is not in the book.</returns>
    public bool TryGetMove(Position position, Random random, out int column, out int score)
    {
        column = -1;
        if (!TryGetBackedUpScore(position, out score, out int bestColumns))
        {
            return false;
        }

        int pick = random.Next(BitOperations.PopCount((uint)bestColumns));
        for (column = 0; ; column++)
        {
            if ((bestColumns & 1 << column) != 0 && pick-- == 0)
            {
                return true;
            }
        }
    }

    internal bool TryGetBackedUpScore(Position position, out int score, out int bestColumns)
    {
        if (position.Moves >= Depth)
        {
            score = 0;
            bestColumns = 0;
            return false;
        }

        return BookBuilder.TryBackUp(position, TryGet, out score, out bestColumns);
    }

    private bool TryGet(ulong key, out int score)
    {
        int low = 0;
        int high = _entries.Length - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            ulong found = _entries[middle] >> ScoreBits;
            if (found == key)
            {
                score = (int)(_entries[middle] & ((1UL << ScoreBits) - 1)) - ScoreOffset;
                return true;
            }

            if (found < key)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        score = 0;
        return false;
    }
}
