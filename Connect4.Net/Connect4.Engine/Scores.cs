using Connect4.Engine.Endgame;
using Connect4.Engine.Evaluation;

namespace Connect4.Engine;

/// <summary>
/// Search scores from the side to move. Heuristic scores are within ±<see cref="Evaluator.MaxScore"/>.
/// A won game scores <see cref="Win"/> minus the number of discs on the board after the winning move,
/// so a faster win scores higher and the score of a position does not depend on the search root.
/// </summary>
public static class Scores
{
    public const int Win = 10_000;

    public static bool IsDecided(int score) => Math.Abs(score) > Evaluator.MaxScore;

    /// <summary>The number of discs on the board after the winning move of a decided score.</summary>
    public static int WinningMoveNumber(int score) => Win - Math.Abs(score);

    internal static int WinAfter(int moveNumber) => Win - moveNumber;

    /// <summary>Converts an <see cref="EndgameSolver"/> score (22 − the winner's disc number) for the side to move.</summary>
    internal static int FromSolver(int solverScore, Position position)
    {
        if (solverScore == 0)
        {
            return 0;
        }

        Player winner = solverScore > 0 ? position.ToMove : position.ToMove.Opponent();
        int winnerDisc = EndgameSolver.MaxScore + 4 - Math.Abs(solverScore);
        int moveNumber = 2 * winnerDisc - (winner == Player.Red ? 1 : 0);
        return solverScore > 0 ? WinAfter(moveNumber) : -WinAfter(moveNumber);
    }
}
