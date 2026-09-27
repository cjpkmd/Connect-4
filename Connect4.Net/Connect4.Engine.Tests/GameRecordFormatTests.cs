using Connect4.Engine;

namespace Connect4.Engine.Tests;

public class GameRecordFormatTests
{
    [Fact]
    public void Format_WritesThePlayedMoves()
    {
        var game = new Game();
        game.Play(3);
        game.Play(3);
        game.Play(4);
        game.Play(2);
        game.Undo();

        Assert.Equal("445", GameRecordFormat.Format(game));
    }

    [Fact]
    public void Parse_RoundTrips()
    {
        Game game = GameRecordFormat.Parse("4453");

        Assert.Equal([3, 3, 4, 2], game.Moves);
        Assert.Equal("4453", GameRecordFormat.Format(game));
    }

    [Fact]
    public void Parse_IgnoresWhitespace()
    {
        Assert.Equal("4453", GameRecordFormat.Format(GameRecordFormat.Parse(" 44\n53\r\n")));
    }

    [Fact]
    public void Parse_AcceptsAFinishedGame()
    {
        Assert.Equal(Player.Red, GameRecordFormat.Parse("1212121").Winner);
    }

    [Theory]
    [InlineData("408", "Move 2: '0'")]
    [InlineData("1111111", "Move 7: column 1 is full")]
    [InlineData("12121213", "Move 8: the game is already over")]
    public void Parse_InvalidRecord_Throws(string text, string message)
    {
        var exception = Assert.Throws<FormatException>(() => GameRecordFormat.Parse(text));
        Assert.StartsWith(message, exception.Message);
    }
}
