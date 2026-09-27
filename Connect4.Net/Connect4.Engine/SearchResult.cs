namespace Connect4.Engine;

public enum ScoreKind
{
    /// <summary>No search was needed (only one move does not lose at once).</summary>
    None,

    /// <summary>Evaluation points, within ±<see cref="Evaluation.Evaluator.MaxScore"/>.</summary>
    Heuristic,

    /// <summary>The game-theoretic result: a win or loss (see <see cref="Scores"/>) or a draw (0).</summary>
    Exact,
}

/// <summary>Progress from a running search.</summary>
/// <param name="Depth">Plies searched, or empty cells while the endgame solver runs.</param>
/// <param name="Column">The root move just searched (0-based).</param>
/// <param name="Solving">The endgame solver is running.</param>
public sealed record SearchInfo(
    int Depth,
    int Column,
    int? BestColumn,
    int Score,
    ScoreKind Kind,
    long Nodes,
    TimeSpan Elapsed,
    IReadOnlyList<int> PrincipalVariation,
    bool Solving);

/// <param name="Column">The chosen move (0-based).</param>
/// <param name="Score">From the side to move.</param>
public sealed record SearchResult(
    int Column,
    int Score,
    ScoreKind Kind,
    int Depth,
    long Nodes,
    TimeSpan Elapsed,
    IReadOnlyList<int> PrincipalVariation);
