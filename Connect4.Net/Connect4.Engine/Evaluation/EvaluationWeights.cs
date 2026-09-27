namespace Connect4.Engine.Evaluation;

/// <summary>Weights of the evaluation terms, in score points. Tuned by hand.</summary>
/// <param name="Threat">Per empty cell that completes a four.</param>
/// <param name="ParityThreat">Extra per threat on the player's good rows: odd rows (1, 3, 5 from the bottom) for Red, even rows for Yellow.</param>
/// <param name="StackedThreat">Per threat with another threat of the same player directly above it.</param>
/// <param name="OpenTwo">Per line of four cells with two own discs and no opponent disc.</param>
/// <param name="OpenThree">Per line of four cells with three own discs and no opponent disc.</param>
/// <param name="Centre">Per disc, times the number of lines of four through its cell (3 at the corners, 13 in the centre).</param>
public sealed record EvaluationWeights(
    int Threat = 30,
    int ParityThreat = 20,
    int StackedThreat = 40,
    int OpenTwo = 4,
    int OpenThree = 6,
    int Centre = 1)
{
    public static EvaluationWeights Default { get; } = new();
}
