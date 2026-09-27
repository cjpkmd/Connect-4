using System.Diagnostics;
using Connect4.Engine.Endgame;
using Connect4.Engine.Evaluation;
using Connect4.Engine.Search;

namespace Connect4.Engine;

/// <summary>
/// Iterative deepening alpha-beta search (principal variation search) with a hash table and the
/// connect4-master move generation and ordering, stopped by a depth or time limit (Stello design).
/// Positions at the horizon are scored by the <see cref="Evaluator"/>. Near the end of the game the exact
/// <see cref="EndgameSolver"/> is tried. An instance runs one search at a time.
/// </summary>
public sealed class SearchEngine
{
    /// <summary>Table size used when the requested one cannot be allocated (e.g. in a browser on a phone).</summary>
    public const int FallbackLogSize = 20;

    private const int Infinity = Scores.Win + 1;

    // When the solver will run in fixed-depth mode, this shallow search only gives a move for Move Now.
    private const int FallbackDepth = 8;

    private readonly TranspositionTable _table;
    private readonly int _endgameLogSize;
    private readonly Evaluator _evaluator;
    private readonly Random _random;
    private readonly Stopwatch _clock = new();
    private EndgameSolver? _endgame;

    private long _nodes;
    private long _hardLimitMs;
    private CancellationToken _cancel;
    private CancellationToken _moveNow;
    private IProgress<SearchInfo>? _progress;

    /// <param name="hashLogSize">The search hash table has 2^hashLogSize entries of 8 bytes (24 = 128 MiB).</param>
    /// <param name="endgameLogSize">Size of the endgame solver's table (24 ≈ 84 MB); allocated the first time the solver runs.</param>
    /// <param name="random">Picks among equally good moves; seed it for repeatable games.</param>
    /// <remarks>A table that cannot be allocated gets 2^<see cref="FallbackLogSize"/> entries instead.</remarks>
    public SearchEngine(
        int hashLogSize = TranspositionTable.DefaultLogSize,
        int endgameLogSize = EndgameTable.DefaultLogSize,
        Evaluator? evaluator = null,
        Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(endgameLogSize, EndgameTable.MinLogSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(endgameLogSize, EndgameTable.MaxLogSize);

        _table = AllocateWithFallback(
            () => new TranspositionTable(hashLogSize),
            () => new TranspositionTable(Math.Min(hashLogSize, FallbackLogSize)));
        _endgameLogSize = endgameLogSize;
        _evaluator = evaluator ?? Evaluator.Default;
        _random = random ?? new Random();
    }

    /// <summary>True when a table got the fallback size because the requested size could not be allocated.</summary>
    public bool UsesFallbackTables { get; private set; }

    /// <summary>Clears the search hash table (at New Game). The endgame solver's table is kept: its results never go stale.</summary>
    public void ClearHash() => _table.Clear();

    /// <summary>Finds the best move for the side to move. The position must not contain a four.</summary>
    /// <param name="cancellationToken">Stops the search and throws <see cref="OperationCanceledException"/>.</param>
    /// <param name="moveNowToken">Stops the search and returns the best move found so far.</param>
    public SearchResult Search(
        Position position,
        SearchLimits limits,
        IProgress<SearchInfo>? progress = null,
        CancellationToken cancellationToken = default,
        CancellationToken moveNowToken = default)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (position.IsFull)
        {
            throw new ArgumentException("The board is full.", nameof(position));
        }

        cancellationToken.ThrowIfCancellationRequested();
        _clock.Restart();
        _nodes = 0;
        _cancel = cancellationToken;
        _moveNow = moveNowToken;
        _progress = progress;
        _table.NewSearch();

        int[] winning = [.. Enumerable.Range(0, Position.Width).Where(c => position.CanPlay(c) && position.IsWinningMove(c))];
        if (winning.Length > 0)
        {
            return Result(position, new Best(winning, Scores.WinAfter(position.Moves + 1), ScoreKind.Exact, 1, Final: true, Solved: true));
        }

        ulong possible = position.NonLosingMoves();
        if (possible == 0)
        {
            int column = MoveOrdering.ColumnOrder.First(position.CanPlay);
            return Result(position, new Best([column], -Scores.WinAfter(position.Moves + 2), ScoreKind.Exact, 1, Final: true, Solved: true));
        }

        int[] root = OrderedColumns(position, possible);
        if (root.Length == 1 && limits.Mode != TimeControlMode.Solve)
        {
            return Result(position, new Best(root, 0, ScoreKind.None, 0, Final: true, Solved: false));
        }

        int emptyCells = position.EmptyCells;
        TimeBudget budget = TimeControl.For(limits, emptyCells);
        _hardLimitMs = budget.HardMs;
        bool timed = TimeControl.IsTimed(limits);
        bool useSolver = limits.Mode switch
        {
            TimeControlMode.Solve => true,
            TimeControlMode.FixedDepth => emptyCells <= limits.Depth,
            _ => emptyCells <= limits.EndgameThreshold,
        };
        int maxDepth = limits.Mode switch
        {
            TimeControlMode.FixedDepth => useSolver ? Math.Min(limits.Depth, FallbackDepth) : limits.Depth,
            TimeControlMode.Solve => 1,
            _ => SearchLimits.MaxDepth,
        };

        var best = new Best([root[0]], 0, ScoreKind.Heuristic, 0, Final: false, Solved: false);
        try
        {
            for (int depth = 1; ; depth++)
            {
                best = SearchRoot(position, depth, root);
                if (best.Final || depth >= maxDepth || (timed && _clock.ElapsedMilliseconds > budget.SoftMs))
                {
                    break;
                }
            }
        }
        catch (SearchAbortedException)
        {
            // Time is up or Move Now: keep the result of the last completed iteration.
        }

        if (useSolver && !best.Final && !_moveNow.IsCancellationRequested && _clock.ElapsedMilliseconds < _hardLimitMs)
        {
            best = SolveRoot(position, best, timed);
        }

        return Result(position, best);
    }

    private Best SearchRoot(Position position, int depth, int[] root)
    {
        int bestScore = -Infinity;
        var ties = new List<int>(root.Length);

        foreach (int column in root)
        {
            Position child = position.Play(column);
            int score;
            if (ties.Count == 0)
            {
                score = -Negamax(child, depth - 1, -Infinity, Infinity);
            }
            else
            {
                // A window around the best score tells worse, equal and better apart; only a better move needs its exact score.
                score = -Negamax(child, depth - 1, -(bestScore + 1), -(bestScore - 1));
                if (score > bestScore)
                {
                    score = -Negamax(child, depth - 1, -Infinity, -bestScore);
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                ties.Clear();
                ties.Add(column);
            }
            else if (score == bestScore)
            {
                ties.Add(column);
            }

            Report(depth, column, ties[0], bestScore, Kind(bestScore), position, solving: false);
        }

        // The best moves are searched first in the next iteration.
        int[] rest = [.. root.Except(ties)];
        ties.CopyTo(root);
        rest.CopyTo(root, ties.Count);

        // A decided score is the fastest win (slowest loss) once the search is at least as deep as the win.
        bool final = depth >= position.EmptyCells
            || (Scores.IsDecided(bestScore) && depth >= Scores.WinningMoveNumber(bestScore) - position.Moves);
        return new Best([.. ties], bestScore, final ? ScoreKind.Exact : Kind(bestScore), depth, final, Solved: false);
    }

    /// <summary>Negamax alpha-beta; fail-soft, with bounds stored in the hash table.</summary>
    private int Negamax(Position position, int depth, int alpha, int beta)
    {
        if ((++_nodes & 1023) == 0)
        {
            CheckAbort();
        }

        if (position.CanWinNext)
        {
            return Scores.WinAfter(position.Moves + 1);
        }

        ulong possible = position.NonLosingMoves();
        if (possible == 0)
        {
            return -Scores.WinAfter(position.Moves + 2);
        }

        // The opponent cannot win with the last disc after a non-losing move.
        if (position.Moves >= Position.CellCount - 2)
        {
            return 0;
        }

        // We cannot win before move Moves + 3, and the opponent cannot win before move Moves + 4.
        int max = Scores.WinAfter(position.Moves + 3);
        if (beta > max)
        {
            beta = max;
            if (alpha >= beta)
            {
                return beta;
            }
        }

        int min = -Scores.WinAfter(position.Moves + 4);
        if (alpha < min)
        {
            alpha = min;
            if (alpha >= beta)
            {
                return alpha;
            }
        }

        // A single move that does not lose (usually a forced block) is searched without using up depth.
        bool forced = (possible & (possible - 1)) == 0;
        if (depth <= 0 && !forced)
        {
            return _evaluator.Evaluate(position);
        }

        ulong key = position.Key;
        int hashColumn = -1;
        if (_table.TryGet(key, out TableEntry entry))
        {
            hashColumn = entry.Column;
            if (entry.Depth >= depth && entry.Bound switch
                {
                    Bound.Exact => true,
                    Bound.Lower => entry.Value >= beta,
                    Bound.Upper => entry.Value <= alpha,
                    _ => false,
                })
            {
                return entry.Value;
            }
        }

        int childDepth = forced ? depth : depth - 1;
        int alphaOriginal = alpha;
        int best = -Infinity;
        ulong bestMove = 0;

        var moves = new MoveSorter();
        MoveOrdering.Add(ref moves, position, possible, hashColumn);
        for (ulong move = moves.GetNext(); move != 0; move = moves.GetNext())
        {
            Position child = position.PlayMove(move);
            int score;
            if (bestMove == 0)
            {
                score = -Negamax(child, childDepth, -beta, -alpha);
            }
            else
            {
                score = -Negamax(child, childDepth, -alpha - 1, -alpha);
                if (score > alpha && score < beta)
                {
                    score = -Negamax(child, childDepth, -beta, -alpha);
                }
            }

            if (score > best)
            {
                best = score;
                bestMove = move;
                if (score > alpha)
                {
                    alpha = score;
                    if (alpha >= beta)
                    {
                        break;
                    }
                }
            }
        }

        Bound bound = best >= beta ? Bound.Lower : best > alphaOriginal ? Bound.Exact : Bound.Upper;
        _table.Store(key, depth, bound, best, Position.ColumnOf(bestMove));
        return best;
    }

    private Best SolveRoot(Position position, Best heuristic, bool timed)
    {
        _endgame ??= AllocateWithFallback(
            () => new EndgameSolver(_endgameLogSize),
            () => new EndgameSolver(Math.Min(_endgameLogSize, FallbackLogSize)));
        Report(position.EmptyCells, heuristic.Columns[0], heuristic.Columns[0], heuristic.Score, heuristic.Kind, position, solving: true);

        using var source = CancellationTokenSource.CreateLinkedTokenSource(_cancel, _moveNow);
        if (timed)
        {
            source.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(0, _hardLimitMs - _clock.ElapsedMilliseconds)));
        }

        long nodesBefore = _endgame.NodeCount;
        try
        {
            int[] scores = _endgame.Analyze(position, cancellation: source.Token);
            int[] playable = [.. Enumerable.Range(0, Position.Width).Where(c => scores[c] != EndgameSolver.InvalidMove)];
            int best = playable.Max(c => Scores.FromSolver(scores[c], position));
            int[] ties = [.. playable.Where(c => Scores.FromSolver(scores[c], position) == best)];
            return new Best(ties, best, ScoreKind.Exact, position.EmptyCells, Final: true, Solved: true);
        }
        catch (OperationCanceledException) when (!_cancel.IsCancellationRequested)
        {
            return heuristic;
        }
        finally
        {
            _nodes += _endgame.NodeCount - nodesBefore;
        }
    }

    private static int[] OrderedColumns(Position position, ulong possible)
    {
        var sorter = new MoveSorter();
        MoveOrdering.Add(ref sorter, position, possible);
        var columns = new List<int>(Position.Width);
        for (ulong move = sorter.GetNext(); move != 0; move = sorter.GetNext())
        {
            columns.Add(Position.ColumnOf(move));
        }

        return [.. columns];
    }

    /// <summary>The expected line of play from the hash table, starting with <paramref name="column"/>.</summary>
    private List<int> PrincipalVariation(Position position, int column)
    {
        var line = new List<int> { column };
        Position current = position.Play(column);
        while (!current.IsFull)
        {
            int win = Array.FindIndex(MoveOrdering.ColumnOrder, c => current.CanPlay(c) && current.IsWinningMove(c));
            if (win >= 0)
            {
                line.Add(MoveOrdering.ColumnOrder[win]);
                break;
            }

            if (!_table.TryGet(current.Key, out TableEntry entry) || entry.Column < 0 || !current.CanPlay(entry.Column))
            {
                break;
            }

            line.Add(entry.Column);
            current = current.Play(entry.Column);
        }

        return line;
    }

    private static ScoreKind Kind(int score) => Scores.IsDecided(score) ? ScoreKind.Exact : ScoreKind.Heuristic;

    internal T AllocateWithFallback<T>(Func<T> preferred, Func<T> fallback)
    {
        try
        {
            return preferred();
        }
        catch (OutOfMemoryException)
        {
            UsesFallbackTables = true;
            return fallback();
        }
    }

    private void CheckAbort()
    {
        _cancel.ThrowIfCancellationRequested();
        if (_moveNow.IsCancellationRequested || _clock.ElapsedMilliseconds >= _hardLimitMs)
        {
            throw new SearchAbortedException();
        }
    }

    private void Report(int depth, int column, int bestColumn, int score, ScoreKind kind, Position position, bool solving)
    {
        if (_progress is null)
        {
            return;
        }

        _progress.Report(new SearchInfo(
            depth, column, bestColumn, score, kind, _nodes, _clock.Elapsed, PrincipalVariation(position, bestColumn), solving));
    }

    private SearchResult Result(Position position, Best best)
    {
        int column = best.Columns[_random.Next(best.Columns.Length)];
        IReadOnlyList<int> line = best.Solved ? [column] : PrincipalVariation(position, column);
        return new SearchResult(column, best.Score, best.Kind, best.Depth, _nodes, _clock.Elapsed, line);
    }

    /// <param name="Columns">All moves with the best score; one is picked at random.</param>
    /// <param name="Final">Searching deeper cannot change the result.</param>
    /// <param name="Solved">Found without the search hash table (no principal variation).</param>
    private sealed record Best(int[] Columns, int Score, ScoreKind Kind, int Depth, bool Final, bool Solved);

    private sealed class SearchAbortedException : Exception;
}
