using System.Diagnostics;
using Connect4.Engine;
using Connect4.Engine.Endgame;

namespace Connect4.Tools;

/// <summary>Measures how long the engine's endgame solve (every move, as <c>SearchEngine</c> does) takes by number of empty cells.</summary>
internal static class EndgameCommands
{
    private static readonly string[] TestSets = ["Test_L1_R2", "Test_L1_R3", "Test_L2_R2"];

    // Only for the self-play games, which never reach the solver.
    private const int SmallEndgameTable = 17;

    public static int Measure(Options options)
    {
        options.AllowOnly("from", "to", "games", "per-set", "limit-ms", "cap", "seed", "workers", "table", "tests");
        int from = options.GetInt("from", 24, 1, Position.CellCount);
        int to = options.GetInt("to", 33, from, Position.CellCount);
        int games = options.GetInt("games", 100, 0, 1_000_000);
        int perSet = options.GetInt("per-set", 50, 0, 1_000_000);
        int limitMs = options.GetInt("limit-ms", 1700, 1, int.MaxValue);
        int cap = options.GetInt("cap", 30, 1, 3600);
        int seed = options.GetInt("seed", 1, 0, int.MaxValue);
        int workers = options.GetInt("workers", 1, 1, 256);
        int table = options.GetInt("table", 24, 17, 27);
        string tests = options.GetString("tests", "") is { Length: > 0 } folder ? folder : FindTestFolder();

        Console.WriteLine($"Solving every move from scratch (table 2^{table}, {workers} worker(s), cap {cap} s), {from}-{to} empty cells.");
        var sources = new List<(string Name, List<Position> Positions)>
        {
            ($"Engine games ({games} games, 15 % random moves)", GamePositions(games, from, to, seed)),
            ("Test sets " + string.Join(", ", TestSets), TestPositions(tests, from, to, perSet, seed)),
        };

        foreach ((string name, List<Position> positions) in sources)
        {
            Console.WriteLine();
            Console.WriteLine($"{name}: {positions.Count} positions");
            var times = new Dictionary<int, List<double>>();
            var gate = new object();
            int done = 0;
            var clock = Stopwatch.StartNew();
            ParallelSolver.ForEach(
                positions,
                workers,
                table,
                (solver, position) =>
                {
                    solver.Reset();
                    var watch = Stopwatch.StartNew();
                    double seconds;
                    try
                    {
                        solver.Analyze(position, timeLimit: TimeSpan.FromSeconds(cap));
                        seconds = watch.Elapsed.TotalSeconds;
                    }
                    catch (OperationCanceledException)
                    {
                        seconds = double.PositiveInfinity;
                    }

                    lock (gate)
                    {
                        (times.TryGetValue(position.EmptyCells, out var list) ? list : times[position.EmptyCells] = []).Add(seconds);
                        done++;
                    }
                },
                () =>
                {
                    lock (gate)
                    {
                        Console.Write($"\r{done} / {positions.Count}, {clock.Elapsed:h\\:mm\\:ss}   ");
                    }
                },
                CancellationToken.None);
            Console.WriteLine();
            PrintTable(times, limitMs, cap);
        }

        return 0;
    }

    private static void PrintTable(Dictionary<int, List<double>> times, int limitMs, int cap)
    {
        double limit = limitMs / 1000.0;
        Console.WriteLine($"Empty      n     mean   median      90 %      max   <= {limit:0.0#} s   <= 5 s   > {cap} s");
        int? threshold = null;
        bool ok = true;
        foreach ((int empty, List<double> list) in times.OrderBy(pair => pair.Key))
        {
            list.Sort();
            double Percentile(double p) => list[Math.Min(list.Count - 1, (int)(p * list.Count))];
            double p90 = Percentile(0.9);
            string Format(double s) => double.IsPositiveInfinity(s) ? $"> {cap} s" : $"{s:0.000} s";
            double Share(double s) => 100.0 * list.Count(t => t <= s) / list.Count;
            var finished = list.Where(t => !double.IsPositiveInfinity(t)).ToList();
            string mean = finished.Count == list.Count ? Format(finished.Average()) : "  -";
            Console.WriteLine(
                $"{empty,5} {list.Count,6} {mean,9} {Format(Percentile(0.5)),9} {Format(p90),9} {Format(list[^1]),9} {Share(limit),8:0}% {Share(5),7:0}% {list.Count - finished.Count,6}");
            ok &= p90 <= limit;
            if (ok)
            {
                threshold = empty;
            }
        }

        Console.WriteLine(threshold is { } n
            ? $"Largest threshold where 90 % of the solves at every smaller count finish within {limit:0.0#} s: {n} empty cells."
            : $"Even the fewest empty cells measured take more than {limit:0.0#} s for 10 % of the positions.");
    }

    // Positions from games the engine plays against itself, with some random moves as a human would make; each position once.
    private static List<Position> GamePositions(int games, int from, int to, int seed)
    {
        var random = new Random(seed);
        var engine = new SearchEngine(hashLogSize: 20, endgameLogSize: SmallEndgameTable, random: new Random(seed));
        SearchLimits limits = SearchLimits.FixedDepth(10);
        var seen = new HashSet<ulong>();
        var positions = new List<Position>();
        for (int i = 0; i < games; i++)
        {
            var game = new Game();
            engine.ClearHash();
            while (!game.IsGameOver && game.Position.EmptyCells >= from)
            {
                Position position = game.Position;
                if (position.EmptyCells <= to && !position.CanWinNext && seen.Add(position.CanonicalKey))
                {
                    positions.Add(position);
                }

                int column = random.NextDouble() < 0.15 ? RandomSafeMove(position, random) : -1;
                game.Play(column >= 0 ? column : engine.Search(position, limits).Column);
            }
        }

        return positions;
    }

    private static int RandomSafeMove(Position position, Random random)
    {
        int[] safe = [.. Enumerable.Range(0, Position.Width)
            .Where(c => position.CanPlay(c) && !position.IsWinningMove(c) && !position.Play(c).CanWinNext)];
        return safe.Length > 0 ? safe[random.Next(safe.Length)] : -1;
    }

    // At most perSet positions per number of empty cells from each test set.
    private static List<Position> TestPositions(string folder, int from, int to, int perSet, int seed)
    {
        var random = new Random(seed);
        var positions = new List<Position>();
        foreach (string set in TestSets)
        {
            positions.AddRange(File.ReadLines(Path.Combine(folder, set))
                .Where(line => line.Length > 0)
                .Select(line => Position.FromMoves(line.Split(' ')[0]))
                .Where(position => position.EmptyCells >= from && position.EmptyCells <= to && !position.CanWinNext)
                .GroupBy(position => position.EmptyCells)
                .SelectMany(group => group.OrderBy(_ => random.Next()).Take(perSet)));
        }

        return positions;
    }

    /// <exception cref="IOException">No "Test positions" folder above the current folder.</exception>
    private static string FindTestFolder()
    {
        for (var folder = new DirectoryInfo(Environment.CurrentDirectory); folder is not null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "Test positions");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("No \"Test positions\" folder found; give it with --tests.");
    }
}
