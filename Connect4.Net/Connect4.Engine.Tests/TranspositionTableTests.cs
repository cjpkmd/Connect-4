using Connect4.Engine;
using Connect4.Engine.Search;

namespace Connect4.Engine.Tests;

public class TranspositionTableTests
{
    [Fact]
    public void TryGet_ReturnsTheStoredEntry()
    {
        var table = new TranspositionTable(TranspositionTable.MinLogSize);
        ulong key = Position.FromMoves("4453").Key;

        table.Store(key, 7, Bound.Exact, -1234, 3);

        Assert.True(table.TryGet(key, out TableEntry entry));
        Assert.Equal(new TableEntry(7, Bound.Exact, -1234, 3), entry);
    }

    [Fact]
    public void TryGet_UnknownPosition_Misses()
    {
        var table = new TranspositionTable(TranspositionTable.MinLogSize);
        table.Store(Position.FromMoves("4453").Key, 7, Bound.Exact, 5, 3);

        Assert.False(table.TryGet(Position.FromMoves("4454").Key, out _));
    }

    [Fact]
    public void Store_NoColumnAndWinScores_RoundTrip()
    {
        var table = new TranspositionTable(TranspositionTable.MinLogSize);
        ulong key = Position.FromMoves("1").Key;

        table.Store(key, 0, Bound.Lower, -Scores.Win + 10, -1);

        Assert.True(table.TryGet(key, out TableEntry entry));
        Assert.Equal(new TableEntry(0, Bound.Lower, -Scores.Win + 10, -1), entry);
    }

    [Fact]
    public void Keys_NeverGiveFalseHits()
    {
        var table = new TranspositionTable(TranspositionTable.MinLogSize);
        for (ulong key = 0; key < 1 << 16; key++)
        {
            table.Store(key, 1, Bound.Exact, (int)(key % 1000), 0);
            Assert.True(table.TryGet(key, out TableEntry entry));
            Assert.Equal((int)(key % 1000), entry.Value);
            Assert.False(table.TryGet(key + (1UL << 40), out _));
        }
    }

    [Fact]
    public void Store_KeepsADeeperEntryOfTheSameSearch()
    {
        var table = new TranspositionTable(TranspositionTable.MinLogSize);
        (ulong deep, ulong shallow) = SameSlot();

        table.Store(deep, 9, Bound.Exact, 1, 0);
        table.Store(shallow, 2, Bound.Exact, 2, 0);

        Assert.True(table.TryGet(deep, out _));
        Assert.False(table.TryGet(shallow, out _));
    }

    [Fact]
    public void Store_ReplacesAnEntryFromAnEarlierSearch()
    {
        var table = new TranspositionTable(TranspositionTable.MinLogSize);
        (ulong deep, ulong shallow) = SameSlot();

        table.Store(deep, 9, Bound.Exact, 1, 0);
        table.NewSearch();
        table.Store(shallow, 2, Bound.Exact, 2, 0);

        Assert.False(table.TryGet(deep, out _));
        Assert.True(table.TryGet(shallow, out _));
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var table = new TranspositionTable(TranspositionTable.MinLogSize);
        table.Store(12345, 3, Bound.Upper, 4, 5);

        table.Clear();

        Assert.False(table.TryGet(12345, out _));
    }

    [Theory]
    [InlineData(TranspositionTable.MinLogSize - 1)]
    [InlineData(TranspositionTable.MaxLogSize + 1)]
    public void Constructor_LogSizeOutOfRange_Throws(int logSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TranspositionTable(logSize));
    }

    /// <summary>Two keys that share a slot: the second one replaces the first.</summary>
    private static (ulong, ulong) SameSlot()
    {
        const ulong first = 1;
        var probe = new TranspositionTable(TranspositionTable.MinLogSize);
        probe.Store(first, 1, Bound.Exact, 0, 0);
        for (ulong key = 2; ; key++)
        {
            probe.Store(key, 1, Bound.Exact, 0, 0);
            if (!probe.TryGet(first, out _))
            {
                return (first, key);
            }
        }
    }
}
