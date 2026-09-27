using Connect4.Engine;

namespace Connect4.Engine.Tests;

/// <summary>Pascal Pons' test sets in "Test positions/": one "&lt;moves&gt; &lt;score&gt;" per line.</summary>
public class TestSetTests
{
    public static TheoryData<string> Files =>
        ["Test_L3_R1", "Test_L2_R1", "Test_L2_R2", "Test_L1_R1", "Test_L1_R2", "Test_L1_R3"];

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryPositionCanBePlayed(string file)
    {
        TestPosition[] positions = TestPosition.Load(file);

        Assert.Equal(1000, positions.Length);
        Assert.All(positions, test =>
        {
            Position position = Position.FromMoves(test.Moves);
            Assert.Equal(test.Moves.Length, position.Moves);
        });
    }
}

internal readonly record struct TestPosition(string Moves, int Score)
{
    public static TestPosition[] Load(string file) =>
        File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Data", file))
            .Where(line => line.Length > 0)
            .Select(line =>
            {
                string[] parts = line.Split(' ');
                return new TestPosition(parts[0], int.Parse(parts[1]));
            })
            .ToArray();
}
