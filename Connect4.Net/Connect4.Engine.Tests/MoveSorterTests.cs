using Connect4.Engine;

namespace Connect4.Engine.Tests;

public class MoveSorterTests
{
    [Fact]
    public void GetNext_ReturnsMovesByDecreasingScore_LastAddedFirstOnTies()
    {
        var sorter = new MoveSorter();
        sorter.Add(1, 2);
        sorter.Add(2, 5);
        sorter.Add(4, 2);
        sorter.Add(8, 0);

        Assert.Equal(2UL, sorter.GetNext());
        Assert.Equal(4UL, sorter.GetNext());
        Assert.Equal(1UL, sorter.GetNext());
        Assert.Equal(8UL, sorter.GetNext());
        Assert.Equal(0UL, sorter.GetNext());
    }
}
