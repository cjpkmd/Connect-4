using System.Numerics;

namespace Connect4.Engine.Evaluation;

/// <summary>
/// Heuristic score of a position at the search horizon (not in connect4-master, which always searches to the end).
/// A threat is an empty cell that completes a four. A cell that is a threat for both players is scored
/// by the parity term: only the player whose good row it is on gets the bonus.
/// </summary>
public sealed class Evaluator
{
    /// <summary>Heuristic scores are clamped to ±MaxScore, below the search's win scores.</summary>
    public const int MaxScore = 1000;

    // 0-based rows 0, 2, 4 are the 1-based odd rows 1, 3, 5.
    private const ulong RedGoodRows = Position.BottomMask * 0b010101;
    private const ulong YellowGoodRows = Position.BottomMask * 0b101010;

    /// <summary>The 69 lines of four cells on the board.</summary>
    internal static readonly ulong[] Lines = CreateLines();

    /// <summary>For each cell, the number of lines through it.</summary>
    internal static readonly int[] CellLineCounts = CreateCellLineCounts();

    // Cells grouped by line count, so the centre term needs one popcount per group.
    private static readonly (int Weight, ulong Cells)[] CentreGroups =
        [.. Enumerable.Range(0, 64)
            .Where(bit => CellLineCounts[bit] > 0)
            .GroupBy(bit => CellLineCounts[bit])
            .Select(group => (group.Key, group.Aggregate(0UL, (cells, bit) => cells | 1UL << bit)))];

    private readonly EvaluationWeights _weights;
    private readonly int[] _lineScores;

    public Evaluator(EvaluationWeights? weights = null)
    {
        _weights = weights ?? EvaluationWeights.Default;
        _lineScores = [0, 0, _weights.OpenTwo, _weights.OpenThree, 0];
    }

    public static Evaluator Default { get; } = new();

    /// <summary>The score for the side to move (negamax convention).</summary>
    public int Evaluate(Position position)
    {
        int score = EvaluateForRed(position);
        return position.ToMove == Player.Red ? score : -score;
    }

    /// <summary>The score from Red's view: positive is good for Red.</summary>
    public int EvaluateForRed(Position position) =>
        EvaluateForRed(position.Discs(Player.Red), position.Discs(Player.Yellow));

    internal int EvaluateForRed(ulong red, ulong yellow)
    {
        ulong mask = red | yellow;
        ulong redThreats = Position.ComputeWinningPosition(red, mask);
        ulong yellowThreats = Position.ComputeWinningPosition(yellow, mask);

        int score = _weights.Threat * (BitOperations.PopCount(redThreats) - BitOperations.PopCount(yellowThreats));
        score += _weights.ParityThreat *
            (BitOperations.PopCount(redThreats & RedGoodRows) - BitOperations.PopCount(yellowThreats & YellowGoodRows));
        score += _weights.StackedThreat *
            (BitOperations.PopCount(redThreats & (redThreats >> 1)) - BitOperations.PopCount(yellowThreats & (yellowThreats >> 1)));

        foreach (ulong line in Lines)
        {
            int redCount = BitOperations.PopCount(red & line);
            int yellowCount = BitOperations.PopCount(yellow & line);
            if (yellowCount == 0)
            {
                score += _lineScores[redCount];
            }
            else if (redCount == 0)
            {
                score -= _lineScores[yellowCount];
            }
        }

        foreach ((int weight, ulong cells) in CentreGroups)
        {
            score += _weights.Centre * weight * (BitOperations.PopCount(red & cells) - BitOperations.PopCount(yellow & cells));
        }

        return Math.Clamp(score, -MaxScore, MaxScore);
    }

    private static ulong[] CreateLines()
    {
        var lines = new List<ulong>();
        foreach ((int dc, int dr) in (ReadOnlySpan<(int, int)>)[(0, 1), (1, 0), (1, 1), (1, -1)])
        {
            for (int column = 0; column < Position.Width; column++)
            {
                for (int row = 0; row < Position.Height; row++)
                {
                    int endColumn = column + 3 * dc;
                    int endRow = row + 3 * dr;
                    if (endColumn >= Position.Width || endRow is < 0 or >= Position.Height)
                    {
                        continue;
                    }

                    ulong line = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        line |= Position.CellBit(column + i * dc, row + i * dr);
                    }

                    lines.Add(line);
                }
            }
        }

        return [.. lines];
    }

    private static int[] CreateCellLineCounts()
    {
        int[] counts = new int[64];
        foreach (ulong line in Lines)
        {
            for (ulong cells = line; cells != 0; cells &= cells - 1)
            {
                counts[BitOperations.TrailingZeroCount(cells)]++;
            }
        }

        return counts;
    }
}
