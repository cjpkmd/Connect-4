using System.Text.Json.Serialization;
using Connect4.Engine;

namespace Connect4.App.Models;

/// <summary>How long the computer may think, as in Stello, plus when the endgame solver is tried.</summary>
public sealed record GameSettings(
    TimeControlMode Mode,
    int Depth,
    int SecondsPerMove,
    int MinutesPerGame,
    int EndgameThreshold = SearchLimits.DefaultEndgameThreshold)
{
    public const int MaxDepth = 20;
    public const int MaxSecondsPerMove = 60;
    public const int MaxMinutesPerGame = 60;
    public const int MaxEndgameThreshold = Position.CellCount;

    public static GameSettings Default { get; } = new(TimeControlMode.TimePerMove, 8, 5, 5);

    [JsonIgnore]
    public TimeSpan GameTime => TimeSpan.FromMinutes(MinutesPerGame);

    /// <summary>The same settings with every value in its allowed range.</summary>
    public GameSettings Normalize() => new(
        Mode is TimeControlMode.FixedDepth or TimeControlMode.TimePerMove or TimeControlMode.TimePerGame ? Mode : Default.Mode,
        Math.Clamp(Depth, 1, MaxDepth),
        Math.Clamp(SecondsPerMove, 1, MaxSecondsPerMove),
        Math.Clamp(MinutesPerGame, 1, MaxMinutesPerGame),
        Math.Clamp(EndgameThreshold, 0, MaxEndgameThreshold));

    public SearchLimits ToLimits(TimeSpan computerTimeLeft)
    {
        SearchLimits limits = Mode switch
        {
            TimeControlMode.FixedDepth => SearchLimits.FixedDepth(Depth),
            TimeControlMode.TimePerMove => SearchLimits.TimePerMove(TimeSpan.FromSeconds(SecondsPerMove)),
            _ => SearchLimits.TimePerGame(computerTimeLeft),
        };
        return limits with { EndgameThreshold = EndgameThreshold };
    }
}
