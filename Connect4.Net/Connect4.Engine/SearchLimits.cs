namespace Connect4.Engine;

public enum TimeControlMode
{
    /// <summary>Search a fixed number of plies.</summary>
    FixedDepth,

    /// <summary>A fixed thinking time for each move.</summary>
    TimePerMove,

    /// <summary>Share the remaining time for the whole game over the remaining moves.</summary>
    TimePerGame,

    /// <summary>Solve the position exactly with the endgame solver, with no time limit.</summary>
    Solve,
}

public sealed record SearchLimits
{
    public const int MaxDepth = Position.CellCount;

    // Measured with "Connect4.Tools endgame measure": 90 % of the solves finish in the time left at 5 s per move.
    public const int DefaultEndgameThreshold = 30;

    private readonly int _endgameThreshold = DefaultEndgameThreshold;

    private SearchLimits(TimeControlMode mode, int depth, TimeSpan time)
    {
        Mode = mode;
        Depth = depth;
        Time = time;
    }

    public static SearchLimits Solve { get; } = new(TimeControlMode.Solve, 0, TimeSpan.Zero);

    public TimeControlMode Mode { get; }

    /// <summary>Plies to search in <see cref="TimeControlMode.FixedDepth"/>.</summary>
    public int Depth { get; }

    /// <summary>Time for this move, or the time left for the whole game.</summary>
    public TimeSpan Time { get; }

    /// <summary>
    /// In the two time modes the endgame solver is tried when at most this many cells are empty; 0 = never.
    /// Fixed depth uses the depth instead.
    /// </summary>
    public int EndgameThreshold
    {
        get => _endgameThreshold;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, Position.CellCount);
            _endgameThreshold = value;
        }
    }

    /// <summary>Play a book move when the position is in the opening book.</summary>
    public bool UseBook { get; init; } = true;

    public static SearchLimits FixedDepth(int plies)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(plies, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(plies, MaxDepth);
        return new SearchLimits(TimeControlMode.FixedDepth, plies, TimeSpan.Zero);
    }

    public static SearchLimits TimePerMove(TimeSpan time)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(time, TimeSpan.Zero);
        return new SearchLimits(TimeControlMode.TimePerMove, 0, time);
    }

    /// <param name="remaining">Time left on the computer's clock; may be negative when the time is used up.</param>
    public static SearchLimits TimePerGame(TimeSpan remaining) =>
        new(TimeControlMode.TimePerGame, 0, remaining);
}
