using Connect4.Engine;
using Connect4.Engine.Endgame;

namespace Connect4.Engine.Tests;

public class EndgameTableTests
{
    [Fact]
    public void Get_ReturnsTheStoredValueOrZero()
    {
        var table = new EndgameTable(EndgameTable.MinLogSize);
        table.Put(12345, 7);

        Assert.Equal(7, table.Get(12345));
        Assert.Equal(0, table.Get(12346));
    }

    [Fact]
    public void Put_SameSlot_KeepsTheLastEntry()
    {
        var table = new EndgameTable(EndgameTable.MinLogSize);
        ulong first = 12345;
        ulong second = first + (ulong)table.Size;

        table.Put(first, 7);
        table.Put(second, 9);

        Assert.Equal(0, table.Get(first));
        Assert.Equal(9, table.Get(second));
    }

    [Fact]
    public void Get_KeysWithTheSameLow32Bits_AreNotConfused()
    {
        var table = new EndgameTable(EndgameTable.MinLogSize);
        ulong first = 12345;
        ulong second = first + (1UL << 32);

        table.Put(first, 7);

        Assert.Equal(0, table.Get(second));
    }

    [Fact]
    public void Reset_RemovesAllEntries()
    {
        var table = new EndgameTable(EndgameTable.MinLogSize);
        table.Put(12345, 7);

        table.Reset();

        Assert.Equal(0, table.Get(12345));
    }

    [Theory]
    [InlineData(EndgameTable.MinLogSize - 1)]
    [InlineData(EndgameTable.MaxLogSize + 1)]
    public void Constructor_LogSizeOutOfRange_Throws(int logSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EndgameTable(logSize));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(8, 11)]
    [InlineData(13, 13)]
    [InlineData(1 << 20, 1_048_583)]
    public void NextPrime_ReturnsTheFirstPrimeAtOrAbove(int n, int expected)
    {
        Assert.Equal(expected, EndgameTable.NextPrime(n));
    }
}
