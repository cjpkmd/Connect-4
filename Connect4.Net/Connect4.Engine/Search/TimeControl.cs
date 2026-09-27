namespace Connect4.Engine.Search;

/// <param name="SoftMs">Do not start a new iteration after this many milliseconds.</param>
/// <param name="HardMs">Stop the search (and the endgame solver) after this many milliseconds.</param>
internal readonly record struct TimeBudget(long SoftMs, long HardMs)
{
    public static TimeBudget Unlimited { get; } = new(long.MaxValue, long.MaxValue);
}

/// <summary>Time allocation as in Stello: the soft limit is 2/3 of the time for the move.</summary>
internal static class TimeControl
{
    // Used when the game clock is already at or below zero.
    private const long MinimumMs = 10;

    public static TimeBudget For(SearchLimits limits, int emptyCells) => limits.Mode switch
    {
        TimeControlMode.TimePerMove => FromMoveTime(Milliseconds(limits.Time)),
        TimeControlMode.TimePerGame => ForGame(Milliseconds(limits.Time), emptyCells),
        _ => TimeBudget.Unlimited,
    };

    public static bool IsTimed(SearchLimits limits) =>
        limits.Mode is TimeControlMode.TimePerMove or TimeControlMode.TimePerGame;

    // The computer makes at most half of the remaining moves; the time is shared evenly over them.
    private static TimeBudget ForGame(long remainingMs, int emptyCells)
    {
        long movesLeft = Math.Max(1, (emptyCells + 1) / 2);
        return FromMoveTime(Math.Max(MinimumMs, remainingMs / movesLeft));
    }

    private static TimeBudget FromMoveTime(long moveMs) => new(moveMs * 2 / 3, moveMs);

    private static long Milliseconds(TimeSpan time) => (long)time.TotalMilliseconds;
}
