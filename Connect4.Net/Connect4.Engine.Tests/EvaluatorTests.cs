using Connect4.Engine;
using Connect4.Engine.Evaluation;

namespace Connect4.Engine.Tests;

public class EvaluatorTests
{
    private static readonly Evaluator Default = Evaluator.Default;

    [Fact]
    public void Lines_AreThe69LinesOfFour()
    {
        Assert.Equal(69, Evaluator.Lines.Length);
        Assert.Equal(69, Evaluator.Lines.Distinct().Count());
        Assert.All(Evaluator.Lines, line => Assert.Equal(4UL, ulong.PopCount(line)));
    }

    [Fact]
    public void CellLineCounts_AreTheClassicCentreTable()
    {
        int[,] expected =
        {
            { 3, 4, 5, 7, 5, 4, 3 },
            { 4, 6, 8, 10, 8, 6, 4 },
            { 5, 8, 11, 13, 11, 8, 5 },
            { 5, 8, 11, 13, 11, 8, 5 },
            { 4, 6, 8, 10, 8, 6, 4 },
            { 3, 4, 5, 7, 5, 4, 3 },
        };

        for (int row = 0; row < Position.Height; row++)
        {
            for (int column = 0; column < Position.Width; column++)
            {
                int bit = column * (Position.Height + 1) + row;
                Assert.Equal(expected[row, column], Evaluator.CellLineCounts[bit]);
            }
        }
    }

    [Fact]
    public void EmptyBoard_ScoresZero()
    {
        Assert.Equal(0, Default.Evaluate(Position.Empty));
    }

    [Fact]
    public void CentreDisc_IsBetterThanEdgeDisc()
    {
        int centre = Default.EvaluateForRed(Position.FromMoves("4"));
        int edge = Default.EvaluateForRed(Position.FromMoves("1"));

        Assert.True(centre > edge);
        Assert.True(edge > 0);
    }

    [Fact]
    public void Threat_CountsEachWinningCell()
    {
        // Three in a row at the bottom, open at both ends: two threats.
        ulong red = Position.CellBit(1, 0) | Position.CellBit(2, 0) | Position.CellBit(3, 0);
        var noThreats = new Evaluator(EvaluationWeights.Default with { Threat = 0 });

        int difference = Default.EvaluateForRed(red, 0) - noThreats.EvaluateForRed(red, 0);

        Assert.Equal(2 * EvaluationWeights.Default.Threat, difference);
    }

    [Theory]
    [InlineData(2, true)]  // 0-based row 2 is row 3 from the bottom: odd, good for Red
    [InlineData(1, false)] // row 2 from the bottom: even
    public void ParityThreat_CountsOnlyOnThePlayersGoodRows(int row, bool bonus)
    {
        ulong red = Position.CellBit(0, row) | Position.CellBit(1, row) | Position.CellBit(2, row);
        var noParity = new Evaluator(EvaluationWeights.Default with { ParityThreat = 0 });

        int difference = Default.EvaluateForRed(red, 0) - noParity.EvaluateForRed(red, 0);

        Assert.Equal(bonus ? EvaluationWeights.Default.ParityThreat : 0, difference);
    }

    [Fact]
    public void ParityThreat_YellowGetsTheBonusOnEvenRows()
    {
        ulong yellow = Position.CellBit(0, 1) | Position.CellBit(1, 1) | Position.CellBit(2, 1);
        var noParity = new Evaluator(EvaluationWeights.Default with { ParityThreat = 0 });

        int difference = Default.EvaluateForRed(0, yellow) - noParity.EvaluateForRed(0, yellow);

        Assert.Equal(-EvaluationWeights.Default.ParityThreat, difference);
    }

    [Fact]
    public void StackedThreat_CountsTwoThreatsAboveEachOther()
    {
        ulong red = 0;
        for (int column = 0; column < 3; column++)
        {
            red |= Position.CellBit(column, 1) | Position.CellBit(column, 2);
        }

        var noStacked = new Evaluator(EvaluationWeights.Default with { StackedThreat = 0 });

        int difference = Default.EvaluateForRed(red, 0) - noStacked.EvaluateForRed(red, 0);

        Assert.Equal(EvaluationWeights.Default.StackedThreat, difference);
    }

    [Fact]
    public void Evaluate_IsFromTheSideToMove()
    {
        foreach (Position position in RandomPositions(seed: 1).Select(game => game.Position))
        {
            int red = Default.EvaluateForRed(position);
            Assert.Equal(position.ToMove == Player.Red ? red : -red, Default.Evaluate(position));
        }
    }

    [Fact]
    public void MirroredPosition_HasTheSameScore()
    {
        foreach ((Position position, string moves) in RandomPositions(seed: 2))
        {
            string mirrored = string.Concat(moves.Select(c => (char)('1' + '7' - c)));
            Assert.Equal(Default.Evaluate(position), Default.Evaluate(Position.FromMoves(mirrored)));
        }
    }

    [Fact]
    public void SwappedColours_NegateTheScore_ApartFromParity()
    {
        var noParity = new Evaluator(EvaluationWeights.Default with { ParityThreat = 0 });
        foreach (Position position in RandomPositions(seed: 3).Select(game => game.Position))
        {
            ulong red = position.Discs(Player.Red);
            ulong yellow = position.Discs(Player.Yellow);
            Assert.Equal(-noParity.EvaluateForRed(red, yellow), noParity.EvaluateForRed(yellow, red));
        }
    }

    [Fact]
    public void Score_StaysWithinTheHeuristicRange()
    {
        var strong = new Evaluator(new EvaluationWeights(Threat: 500));
        foreach (Position position in RandomPositions(seed: 4).Select(game => game.Position))
        {
            Assert.InRange(strong.Evaluate(position), -Evaluator.MaxScore, Evaluator.MaxScore);
        }
    }

    [Theory]
    [InlineData("Test_L2_R1")] // 76 % with the default weights
    [InlineData("Test_L1_R1")] // 80 %
    public void Sign_AgreesWithTheExactScoreInMostDecidedPositions(string file)
    {
        TestPosition[] decided = [.. TestPosition.Load(file).Where(test => test.Score != 0)];

        int agree = decided.Count(test =>
            Math.Sign(Default.Evaluate(Position.FromMoves(test.Moves))) == Math.Sign(test.Score));

        Assert.True(agree > decided.Length * 0.7, $"{agree} of {decided.Length}");
    }

    /// <summary>Positions from random games without a four, with their move sequences.</summary>
    private static IEnumerable<(Position Position, string Moves)> RandomPositions(int seed)
    {
        var random = new Random(seed);
        for (int game = 0; game < 200; game++)
        {
            Position position = Position.Empty;
            string moves = "";
            while (true)
            {
                yield return (position, moves);

                int[] columns = Enumerable.Range(0, Position.Width)
                    .Where(column => position.CanPlay(column) && !position.IsWinningMove(column))
                    .ToArray();
                if (columns.Length == 0)
                {
                    break;
                }

                int column = columns[random.Next(columns.Length)];
                position = position.Play(column);
                moves += (char)('1' + column);
            }
        }
    }
}
