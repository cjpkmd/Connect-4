using System.Diagnostics;
using Connect4.Engine;
using Connect4.Engine.Book;

namespace Connect4.Tools;

internal static class BookCommands
{
    private const int DefaultTable = 24;
    private const string DepthHeader = "# depth ";

    // Red's score of the empty board and of each first move (Pascal Pons' solver).
    private const int KnownEmptyBoardScore = 1;
    private static readonly int[] KnownFirstMoveScores = [-2, -1, 0, 1, 0, -1, -2];

    private static int DefaultWorkers => Math.Max(1, Environment.ProcessorCount / 2);

    public static int Generate(Options options)
    {
        options.AllowOnly("depth", "workers", "table", "out");
        int depth = options.GetInt("depth", 6, 1, BookBuilder.MaxDepth);
        int workers = options.GetInt("workers", DefaultWorkers, 1, 256);
        int table = options.GetInt("table", DefaultTable, 17, 27);
        string output = options.GetString("out", "OpeningBook.txt");
        string partialPath = output + ".partial";

        var clock = Stopwatch.StartNew();
        IReadOnlyList<string>[] levels = BookBuilder.Enumerate(depth);
        IReadOnlyList<string> leaves = levels[depth];
        Console.WriteLine($"Depth {depth}: {levels.Sum(level => level.Count):N0} positions, {leaves.Count:N0} leaves to solve.");

        Dictionary<string, int> scores = ReadPartial(partialPath, depth, leaves);
        List<string> remaining = leaves.Where(leaf => !scores.ContainsKey(leaf)).ToList();
        if (scores.Count > 0)
        {
            Console.WriteLine($"Continuing from {partialPath}: {scores.Count:N0} leaves already solved.");
        }

        Console.WriteLine($"Solving {remaining.Count:N0} leaves with {workers} workers (table 2^{table}). Ctrl+C stops; run again to continue.");
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler stop = (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += stop;
        try
        {
            using var writer = new StreamWriter(partialPath, append: true) { AutoFlush = true, NewLine = "\n" };
            var gate = new object();
            var progress = new Progress(remaining.Count);
            ParallelSolver.SolveAll(
                remaining,
                workers,
                table,
                (moves, score) =>
                {
                    lock (gate)
                    {
                        writer.WriteLine(BookFile.Format(new BookLine(moves, score)));
                        scores[moves] = score;
                        progress.Solved++;
                    }
                },
                () =>
                {
                    lock (gate)
                    {
                        progress.Print();
                    }
                },
                cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= stop;
            Console.WriteLine();
        }

        if (cancellation.IsCancellationRequested)
        {
            Console.WriteLine($"Stopped: {scores.Count:N0} of {leaves.Count:N0} leaves solved and saved in {partialPath}.");
            return 2;
        }

        List<BookLine> lines = BookBuilder.BackUp(levels, scores);
        string version = typeof(BookCommands).Assembly.GetName().Version?.ToString(2) ?? "?";
        string comment =
            $"Connect 4 opening book: depth {depth}, {lines.Count} positions, one line \"<moves> <score>\" per position.\n" +
            "Exact strong scores for the side to move (Pascal Pons' convention: 22 - the winner's disc number, 0 = draw).\n" +
            $"Generated {DateTime.Now:yyyy-MM-dd} by Connect4.Tools {version} in {Format(clock.Elapsed)}.";
        using (var writer = new StreamWriter(output) { NewLine = "\n" })
        {
            BookFile.Write(writer, lines, comment);
        }

        File.Delete(partialPath);
        Console.WriteLine($"Wrote {lines.Count:N0} positions to {output} in {Format(clock.Elapsed)}.");
        PrintFirstMoves(OpeningBook.FromLines(lines));
        return 0;
    }

    public static int Verify(Options options)
    {
        options.AllowOnly("book", "sample", "min-ply", "seed", "workers", "table");
        string path = options.GetString("book", "OpeningBook.txt");
        int sample = options.GetInt("sample", 200, 0, int.MaxValue);
        int minPly = options.GetInt("min-ply", 3, 0, BookBuilder.MaxDepth);
        int seed = options.GetInt("seed", Environment.TickCount & int.MaxValue, 0, int.MaxValue);
        int workers = options.GetInt("workers", DefaultWorkers, 1, 256);
        int table = options.GetInt("table", DefaultTable, 17, 27);

        List<BookLine> lines;
        using (var reader = new StreamReader(path))
        {
            lines = BookFile.Read(reader);
        }

        OpeningBook book = OpeningBook.FromLines(lines);
        Console.WriteLine($"{path}: {lines.Count:N0} positions, depth {book.Depth}.");
        int failures = 0;

        if (book.Depth >= 1)
        {
            int[] redScores = PrintFirstMoves(book);
            bool rootOk = book.TryGetScore(Position.Empty, out int root) && root == KnownEmptyBoardScore;
            bool movesOk = redScores.SequenceEqual(KnownFirstMoveScores);
            Console.WriteLine(rootOk && movesOk ? "Known first-move scores: OK" : "Known first-move scores: WRONG");
            failures += rootOk && movesOk ? 0 : 1;
        }

        List<BookLine> inconsistent = BookBuilder.CheckBackUp(lines);
        Console.WriteLine($"Back-up of the shallower positions: {(inconsistent.Count == 0 ? "OK" : $"{inconsistent.Count} WRONG")}");
        foreach (BookLine line in inconsistent.Take(10))
        {
            Console.WriteLine($"  \"{line.Moves}\" stored {line.Score}");
        }

        failures += inconsistent.Count;

        var expected = lines.Where(line => line.Moves.Length >= minPly).ToDictionary(line => line.Moves, line => line.Score);
        var random = new Random(seed);
        List<string> chosen = expected.Keys.OrderBy(_ => random.Next()).Take(sample).ToList();
        Console.WriteLine($"Solving {chosen.Count} random positions with at least {minPly} discs again (seed {seed}).");
        var gate = new object();
        var progress = new Progress(chosen.Count);
        var wrong = new List<string>();
        ParallelSolver.SolveAll(
            chosen,
            workers,
            table,
            (moves, score) =>
            {
                lock (gate)
                {
                    progress.Solved++;
                    if (score != expected[moves])
                    {
                        wrong.Add($"  \"{moves}\" stored {expected[moves]}, solved {score}");
                    }
                }
            },
            () =>
            {
                lock (gate)
                {
                    progress.Print();
                }
            },
            CancellationToken.None);
        Console.WriteLine();
        Console.WriteLine($"Solved again: {(wrong.Count == 0 ? "OK" : $"{wrong.Count} WRONG")}");
        wrong.ForEach(Console.WriteLine);
        failures += wrong.Count;

        Console.WriteLine(failures == 0 ? "The book is correct." : $"{failures} problems found.");
        return failures == 0 ? 0 : 1;
    }

    /// <exception cref="FormatException">The partial file was made for another depth.</exception>
    private static Dictionary<string, int> ReadPartial(string path, int depth, IReadOnlyList<string> leaves)
    {
        var scores = new Dictionary<string, int>();
        if (!File.Exists(path))
        {
            File.WriteAllLines(path, [DepthHeader + depth]);
            return scores;
        }

        string[] text = File.ReadAllLines(path);
        if (text.Length == 0 || text[0] != DepthHeader + depth)
        {
            throw new FormatException($"{path} was not made for depth {depth}. Delete it, or use the depth it was made for.");
        }

        var leafSet = leaves.ToHashSet();
        foreach (string line in text.Skip(1))
        {
            BookLine parsed;
            try
            {
                parsed = BookFile.Parse(line);
            }
            catch (FormatException)
            {
                // The last line may be cut off by a stop.
                continue;
            }

            if (leafSet.Contains(parsed.Moves))
            {
                scores[parsed.Moves] = parsed.Score;
            }
        }

        // Rewrite without a cut-off line, so new results start on a line of their own.
        File.WriteAllLines(path, [DepthHeader + depth, .. scores.Select(pair => BookFile.Format(new BookLine(pair.Key, pair.Value)))]);
        return scores;
    }

    /// <returns>Red's score of each first move, as in <see cref="KnownFirstMoveScores"/>.</returns>
    private static int[] PrintFirstMoves(OpeningBook book)
    {
        int[] redScores = new int[Position.Width];
        for (int column = 0; column < Position.Width; column++)
        {
            redScores[column] = book.TryGetScore(Position.Empty.Play(column), out int yellow) ? -yellow : int.MinValue;
        }

        book.TryGetScore(Position.Empty, out int root);
        Console.WriteLine($"Empty board: {root}; Red's first moves, columns 1-7: {string.Join(' ', redScores)}");
        return redScores;
    }

    private static string Format(TimeSpan time) =>
        time.TotalHours >= 1 ? $@"{(int)time.TotalHours}:{time:mm\:ss}" : time.ToString(@"m\:ss");

    private sealed class Progress(int total)
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public int Solved { get; set; }

        public void Print()
        {
            double rate = Solved / Math.Max(_clock.Elapsed.TotalSeconds, 0.001);
            string left = Solved == 0 ? "?" : Format(TimeSpan.FromSeconds((total - Solved) / rate));
            Console.Write($"\r{Solved:N0} / {total:N0} ({100.0 * Solved / Math.Max(total, 1):F1} %), {rate:F2} positions/s, {Format(_clock.Elapsed)} used, {left} left   ");
        }
    }
}
