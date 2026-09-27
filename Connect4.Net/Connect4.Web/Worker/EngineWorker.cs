using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Connect4.Engine;

namespace Connect4.Web.Worker;

/// <summary>The engine inside the Web Worker (see wwwroot/js/engine-worker.js).</summary>
[SupportedOSPlatform("browser")]
public static partial class EngineWorker
{
    private static ComputerPlayer? s_computer;

    private static ComputerPlayer Computer =>
        s_computer ?? throw new InvalidOperationException("The engine worker has not been initialised.");

    /// <returns>True when smaller hash tables had to be used.</returns>
    [JSExport]
    public static bool Init(int hashLogSize, int endgameLogSize)
    {
        s_computer = new ComputerPlayer(new SearchEngine(hashLogSize, endgameLogSize));
        return s_computer.UsesFallbackTables;
    }

    [JSExport]
    public static string ChooseMove(string request)
    {
        (Game game, SearchLimits limits) = EngineProtocol.ReadRequest(request);
        return EngineProtocol.WriteResult(Computer.ChooseMove(game, limits, new ProgressPoster()));
    }

    [JSExport]
    public static void NewGame() => Computer.NewGame();

    [JSImport("postProgress", "engine-worker")]
    private static partial void PostProgress(string json);

    // Posting every report would flood the page; new depths, new best moves and the solver always go through.
    private sealed class ProgressPoster : IProgress<SearchInfo>
    {
        private const long IntervalMs = 100;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastPostMs = -IntervalMs;
        private int _lastDepth = -1;
        private int? _lastBest;
        private bool _lastSolving;

        public void Report(SearchInfo info)
        {
            long now = _clock.ElapsedMilliseconds;
            if (info.Depth == _lastDepth && info.BestColumn == _lastBest && info.Solving == _lastSolving
                && now - _lastPostMs < IntervalMs)
            {
                return;
            }

            _lastDepth = info.Depth;
            _lastBest = info.BestColumn;
            _lastSolving = info.Solving;
            _lastPostMs = now;
            PostProgress(EngineProtocol.WriteProgress(info));
        }
    }
}
