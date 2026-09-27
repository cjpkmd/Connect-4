using System.Diagnostics;
using Connect4.Engine.Search;

namespace Connect4.Engine.Endgame;

/// <summary>
/// Exact Connect 4 solver, a 1:1 port of connect4-master <c>Solver.cpp</c> without the opening book.
/// Scores are from the side to move: 0 = draw; a win with the player's own disc number n
/// (counting from 1) scores 22 − n, so the fastest possible win is 18; a loss is the negative.
/// Not thread-safe; one solve at a time.
/// </summary>
public sealed class EndgameSolver
{
    public const int MinScore = -(Position.CellCount / 2) + 3;
    public const int MaxScore = (Position.CellCount + 1) / 2 - 3;
    public const int InvalidMove = -1000;

    // Checking the token at every node would cost more than it saves.
    private const long CancellationCheckMask = 0x3FF;

    private readonly EndgameTable _table;
    private CancellationToken _cancellation;
    private long _deadline;

    /// <param name="logSize">The hash table has the first prime above 2^logSize entries of 5 bytes (24 ≈ 84 MB).</param>
    public EndgameSolver(int logSize = EndgameTable.DefaultLogSize)
    {
        _table = new EndgameTable(logSize);
    }

    /// <summary>Positions searched since the last <see cref="Reset"/>.</summary>
    public long NodeCount { get; private set; }

    public void Reset()
    {
        NodeCount = 0;
        _table.Reset();
    }

    /// <summary>The exact score of the position; if <paramref name="weak"/>, only the sign (loss / draw / win) is exact.</summary>
    /// <param name="timeLimit">Checked with a clock, not a timer, so it also works where timers cannot fire during a search (a browser worker).</param>
    /// <remarks>The position must not contain a four. Results found before a cancellation stay in the hash table.</remarks>
    /// <exception cref="OperationCanceledException">The token was cancelled or the time limit was reached.</exception>
    public int Solve(Position position, bool weak = false, CancellationToken cancellation = default, TimeSpan? timeLimit = null)
    {
        SetLimits(cancellation, timeLimit);
        return SolveCore(position, weak);
    }

    /// <summary>The score of every column, as <see cref="Solve"/>; <see cref="InvalidMove"/> for full columns.</summary>
    /// <param name="timeLimit">For all columns together.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled or the time limit was reached.</exception>
    public int[] Analyze(Position position, bool weak = false, CancellationToken cancellation = default, TimeSpan? timeLimit = null)
    {
        SetLimits(cancellation, timeLimit);
        int[] scores = new int[Position.Width];
        for (int column = 0; column < Position.Width; column++)
        {
            if (!position.CanPlay(column))
            {
                scores[column] = InvalidMove;
            }
            else if (position.IsWinningMove(column))
            {
                scores[column] = (Position.CellCount + 1 - position.Moves) / 2;
            }
            else
            {
                scores[column] = -SolveCore(position.Play(column), weak);
            }
        }

        return scores;
    }

    private void SetLimits(CancellationToken cancellation, TimeSpan? timeLimit)
    {
        _cancellation = cancellation;
        _deadline = timeLimit is { } limit
            ? Stopwatch.GetTimestamp() + (long)(limit.TotalSeconds * Stopwatch.Frequency)
            : long.MaxValue;
    }

    private int SolveCore(Position position, bool weak)
    {
        if (position.CanWinNext)
        {
            return (Position.CellCount + 1 - position.Moves) / 2;
        }

        int min = -(Position.CellCount - position.Moves) / 2;
        int max = (Position.CellCount + 1 - position.Moves) / 2;
        if (weak)
        {
            min = -1;
            max = 1;
        }

        while (min < max)
        {
            int med = min + (max - min) / 2;
            if (med <= 0 && min / 2 < med)
            {
                med = min / 2;
            }
            else if (med >= 0 && max / 2 > med)
            {
                med = max / 2;
            }

            int r = Negamax(position, med, med + 1);
            if (r <= med)
            {
                max = r;
            }
            else
            {
                min = r;
            }
        }

        return min;
    }

    /// <summary>
    /// Negamax alpha-beta. The side to move must not be able to win at once. Returns the exact score if it is
    /// inside (alpha, beta), otherwise a bound on the same side of the window as the exact score.
    /// </summary>
    private int Negamax(Position position, int alpha, int beta)
    {
        NodeCount++;
        if ((NodeCount & CancellationCheckMask) == 0)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (Stopwatch.GetTimestamp() >= _deadline)
            {
                throw new OperationCanceledException("The time limit was reached.");
            }
        }

        ulong possible = position.NonLosingMoves();
        if (possible == 0)
        {
            return -(Position.CellCount - position.Moves) / 2;
        }

        if (position.Moves >= Position.CellCount - 2)
        {
            return 0;
        }

        int min = -(Position.CellCount - 2 - position.Moves) / 2;
        if (alpha < min)
        {
            alpha = min;
            if (alpha >= beta)
            {
                return alpha;
            }
        }

        int max = (Position.CellCount - 1 - position.Moves) / 2;
        if (beta > max)
        {
            beta = max;
            if (alpha >= beta)
            {
                return beta;
            }
        }

        ulong key = position.Key;
        int value = _table.Get(key);
        if (value != 0)
        {
            if (value > MaxScore - MinScore + 1)
            {
                min = value + 2 * MinScore - MaxScore - 2;
                if (alpha < min)
                {
                    alpha = min;
                    if (alpha >= beta)
                    {
                        return alpha;
                    }
                }
            }
            else
            {
                max = value + MinScore - 1;
                if (beta > max)
                {
                    beta = max;
                    if (alpha >= beta)
                    {
                        return beta;
                    }
                }
            }
        }

        var moves = new MoveSorter();
        MoveOrdering.Add(ref moves, position, possible);

        for (ulong next = moves.GetNext(); next != 0; next = moves.GetNext())
        {
            int score = -Negamax(position.PlayMove(next), -beta, -alpha);
            if (score >= beta)
            {
                _table.Put(key, (byte)(score + MaxScore - 2 * MinScore + 2));
                return score;
            }

            if (score > alpha)
            {
                alpha = score;
            }
        }

        _table.Put(key, (byte)(alpha - MinScore + 1));
        return alpha;
    }
}
