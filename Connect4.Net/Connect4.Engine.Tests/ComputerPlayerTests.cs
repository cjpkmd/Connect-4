using Connect4.Engine;
using Connect4.Engine.Endgame;

namespace Connect4.Engine.Tests;

public class ComputerPlayerTests
{
    private static ComputerPlayer NewPlayer(int seed = 1) =>
        new(new SearchEngine(hashLogSize: 16, endgameLogSize: EndgameTable.MinLogSize, random: new Random(seed)));

    [Fact]
    public void ChooseMove_ReturnsAPlayableColumn()
    {
        Game game = GameRecordFormat.Parse("4453");

        SearchResult result = NewPlayer().ChooseMove(game, SearchLimits.FixedDepth(6));

        Assert.True(game.CanPlay(result.Column));
        Assert.Equal(ScoreKind.Heuristic, result.Kind);
    }

    [Fact]
    public void ChooseMove_TakesAWin()
    {
        Game game = GameRecordFormat.Parse("121212");

        Assert.Equal(0, NewPlayer().ChooseMove(game, SearchLimits.FixedDepth(6)).Column);
    }

    [Fact]
    public void ChooseMove_GameOver_Throws()
    {
        Game game = GameRecordFormat.Parse("1212121");

        Assert.Throws<InvalidOperationException>(() => NewPlayer().ChooseMove(game, SearchLimits.FixedDepth(6)));
    }

    [Fact]
    public void NewGame_ForgetsThePreviousSearches()
    {
        Game game = GameRecordFormat.Parse("4453");
        SearchLimits limits = SearchLimits.FixedDepth(8);
        long fresh = NewPlayer().ChooseMove(game, limits).Nodes;

        ComputerPlayer player = NewPlayer();
        player.ChooseMove(game, limits);
        long remembered = player.ChooseMove(game, limits).Nodes;
        player.NewGame();
        long forgotten = player.ChooseMove(game, limits).Nodes;

        Assert.True(remembered < fresh);
        Assert.Equal(fresh, forgotten);
    }

    [Fact]
    public void SelfPlay_FinishesAGame()
    {
        Game game = PlaySelf(NewPlayer());

        Assert.True(game.IsGameOver);
    }

    [Fact]
    public void AllocateWithFallback_UsesTheFallbackWhenOutOfMemory()
    {
        var engine = new SearchEngine(hashLogSize: 16, endgameLogSize: EndgameTable.MinLogSize);

        int size = engine.AllocateWithFallback<int>(() => throw new OutOfMemoryException(), () => 20);

        Assert.Equal(20, size);
        Assert.True(engine.UsesFallbackTables);
        Assert.True(new ComputerPlayer(engine).UsesFallbackTables);
    }

    [Fact]
    public void RequestedTables_AreNotAFallback()
    {
        Assert.False(NewPlayer().UsesFallbackTables);
    }

    /// <summary>The computer plays both sides at depth 4.</summary>
    private static Game PlaySelf(ComputerPlayer player)
    {
        var game = new Game();
        while (!game.IsGameOver)
        {
            game.Play(player.ChooseMove(game, SearchLimits.FixedDepth(4)).Column);
        }

        return game;
    }
}
