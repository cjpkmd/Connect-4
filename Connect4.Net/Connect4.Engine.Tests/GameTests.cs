using Connect4.Engine;

namespace Connect4.Engine.Tests;

public class GameTests
{
    [Fact]
    public void NewGame_StartsEmptyWithRedToMove()
    {
        var game = new Game();

        Assert.Equal(0, game.Ply);
        Assert.Equal(Player.Red, game.ToMove);
        Assert.Null(game.LastMove);
        Assert.False(game.IsGameOver);
        Assert.False(game.CanUndo);
        Assert.False(game.CanRedo);
    }

    [Fact]
    public void UndoRedo_MovesThroughTheHistory()
    {
        var game = new Game();
        game.Play(3);
        game.Play(4);

        game.Undo();
        Assert.Equal(1, game.Ply);
        Assert.Equal(3, game.LastMove);
        Assert.True(game.CanRedo);

        game.Redo();
        Assert.Equal(2, game.Ply);
        Assert.Equal(Position.FromMoves("45"), game.Position);
    }

    [Fact]
    public void Play_AfterUndo_DropsTheUndoneMoves()
    {
        var game = new Game();
        game.Play(3);
        game.Play(4);
        game.Undo();

        game.Play(2);

        Assert.False(game.CanRedo);
        Assert.Equal([3, 2], game.Moves);
    }

    [Fact]
    public void WinningMove_EndsTheGameAndMarksTheLine()
    {
        Game game = GameRecordFormat.Parse("1212121");

        Assert.True(game.IsGameOver);
        Assert.Equal(Player.Red, game.Winner);
        Assert.False(game.IsDraw);
        Assert.Equal(
            Position.CellBit(0, 0) | Position.CellBit(0, 1) | Position.CellBit(0, 2) | Position.CellBit(0, 3),
            game.WinningLine);
        Assert.False(game.CanPlay(2));
        Assert.Throws<InvalidOperationException>(() => game.Play(2));
    }

    [Fact]
    public void Undo_AfterWinningMove_ReopensTheGame()
    {
        Game game = GameRecordFormat.Parse("1212121");

        game.Undo();

        Assert.False(game.IsGameOver);
        Assert.Null(game.Winner);
        Assert.Equal(0UL, game.WinningLine);
    }

    [Fact]
    public void FullBoardWithoutFour_IsADraw()
    {
        var random = new Random(1);
        for (int attempt = 0; attempt < 10_000; attempt++)
        {
            var game = new Game();
            while (!game.IsGameOver)
            {
                int[] columns = Enumerable.Range(0, Position.Width).Where(game.CanPlay).ToArray();
                game.Play(columns[random.Next(columns.Length)]);
            }

            if (game.IsDraw)
            {
                Assert.Null(game.Winner);
                Assert.True(game.Position.IsFull);
                Assert.Equal(0UL, game.WinningLine);
                return;
            }
        }

        Assert.Fail("No drawn game found.");
    }

    [Fact]
    public void NewGame_ClearsTheHistory()
    {
        Game game = GameRecordFormat.Parse("4453");

        game.NewGame();

        Assert.Equal(0, game.Ply);
        Assert.Empty(game.Moves);
        Assert.Equal(Position.Empty, game.Position);
    }
}
