using Connect4.App.Models;
using Connect4.App.Services;
using Connect4.App.ViewModels;
using Connect4.Engine;

namespace Connect4.App.Tests;

public sealed class MainViewModelTests
{
    private const string GameName = "game.txt";

    // Without the book, so the computer really searches (a book move is played at once).
    private static readonly GameSettings QuickSettings = new(TimeControlMode.FixedDepth, 2, 5, 5, UseOpeningBook: false);
    private static readonly GameSettings SlowSettings = new(TimeControlMode.TimePerMove, 8, 60, 5, UseOpeningBook: false);

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeGameFileService _files = new();
    private readonly FakeSoundService _sounds = new();
    private FakeSettingsStore _store = new(AppSettings.Default);

    [Fact]
    public void Start_HumanPlaysRedAndIsToMove()
    {
        MainViewModel vm = Create();

        Assert.Equal("Your move.", vm.Status);
        Assert.Equal("You play Red", vm.SidesText);
        Assert.Equal("Connect 4 – Untitled", vm.Title);
        Assert.Equal(42, vm.Cells.Count);
        Assert.Equal((0, 5), (vm.Cells[0].Column, vm.Cells[0].Row));
        Assert.Equal((6, 0), (vm.Cells[^1].Column, vm.Cells[^1].Row));
        Assert.All(vm.Cells, cell => Assert.Null(cell.Disc));
    }

    [Fact]
    public void Start_ShowsNotice()
    {
        MainViewModel vm = Create(notice: "Low memory.");

        Assert.Equal("Low memory. Your move.", vm.Status);
    }

    [Fact]
    public async Task Play_ComputerReplies()
    {
        MainViewModel vm = Create();

        vm.PlayCommand.Execute(3);
        await vm.Idle;

        Assert.Equal(2, vm.Game.Ply);
        Assert.False(vm.IsThinking);
        Assert.Equal("Your move.", vm.Status);
        Assert.Equal(Player.Red, Cell(vm, 3, 0).Disc);
        CellViewModel last = Assert.Single(vm.Cells, cell => cell.IsLastMove);
        Assert.Equal(vm.Game.LastMove, last.Column);
        Assert.Equal(Player.Yellow, last.Disc);
        Assert.Equal("2 plies", vm.Analysis.Depth);
        Assert.Equal([Sound.Drop, Sound.Drop], _sounds.Played);
    }

    [Fact]
    public async Task Play_WithTheBook_ComputerRepliesFromTheBook()
    {
        MainViewModel vm = Create(settings: QuickSettings with { UseOpeningBook = true });

        vm.PlayCommand.Execute(3);
        await vm.Idle;

        Assert.Equal(2, vm.Game.Ply);
        Assert.Equal("book", vm.Analysis.Depth);
        Assert.Equal("Red wins in 20 moves", vm.Analysis.Value);
    }

    [Fact]
    public async Task NextDisc_IsTheColourAHumanWouldDrop()
    {
        MainViewModel vm = Create(GameMode.HumanVsHuman);
        Assert.Equal(Player.Red, vm.NextDisc);

        vm.PlayCommand.Execute(3);
        Assert.Equal(Player.Yellow, vm.NextDisc);

        await vm.SetModeCommand.ExecuteAsync(GameMode.HumanVsComputer);
        await vm.SwitchSidesCommand.ExecuteAsync(null);
        Assert.Null(vm.NextDisc);
        await vm.Idle;
        Assert.Equal(Player.Red, vm.NextDisc);
    }

    [Fact]
    public async Task NextDisc_IsNullWhenTheGameIsOver()
    {
        MainViewModel vm = await OpenAsync(Create(), "121212");

        vm.PlayCommand.Execute(0);

        Assert.Null(vm.NextDisc);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Play_ColumnOutOfRangeBeeps(int column)
    {
        MainViewModel vm = Create();

        vm.PlayCommand.Execute(column);

        Assert.Equal(1, _dialogs.Beeps);
        Assert.Equal(0, vm.Game.Ply);
    }

    [Fact]
    public async Task Play_FullColumnBeeps()
    {
        MainViewModel vm = await OpenAsync(Create(GameMode.HumanVsHuman), "111111");

        vm.PlayCommand.Execute(0);

        Assert.Equal(1, _dialogs.Beeps);
        Assert.Equal(6, vm.Game.Ply);
    }

    [Fact]
    public async Task Play_WhileThinkingBeeps()
    {
        MainViewModel vm = Create(settings: SlowSettings);
        vm.PlayCommand.Execute(3);

        vm.PlayCommand.Execute(4);

        Assert.Equal(1, _dialogs.Beeps);
        await vm.NewGameCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task MoveNow_ThePlayIsMadeAtOnce()
    {
        MainViewModel vm = Create(settings: SlowSettings);
        vm.PlayCommand.Execute(3);
        Assert.True(vm.MoveNowCommand.CanExecute(null));

        await Task.Delay(100);
        vm.MoveNowCommand.Execute(null);
        await vm.Idle.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(2, vm.Game.Ply);
    }

    [Fact]
    public async Task UndoAndRedo_MoveBetweenTheHumansTurns()
    {
        MainViewModel vm = Create();
        vm.PlayCommand.Execute(3);
        await vm.Idle;

        await vm.UndoCommand.ExecuteAsync(null);
        await vm.Idle;

        Assert.Equal(0, vm.Game.Ply);
        Assert.True(vm.RedoCommand.CanExecute(null));
        Assert.False(vm.UndoCommand.CanExecute(null));

        await vm.RedoCommand.ExecuteAsync(null);
        await vm.Idle;

        Assert.Equal(2, vm.Game.Ply);
        Assert.Equal(Player.Red, vm.Game.ToMove);
    }

    [Fact]
    public async Task SwitchSides_ComputerMovesAtOnce()
    {
        MainViewModel vm = Create();

        await vm.SwitchSidesCommand.ExecuteAsync(null);
        await vm.Idle;

        Assert.Equal(1, vm.Game.Ply);
        Assert.Equal(Player.Yellow, vm.Human);
        Assert.Equal("You play Yellow", vm.SidesText);
    }

    [Fact]
    public void HumanVsHuman_BothColoursArePlayedByHand()
    {
        MainViewModel vm = Create(GameMode.HumanVsHuman);

        vm.PlayCommand.Execute(3);

        Assert.Equal(1, vm.Game.Ply);
        Assert.Equal("Yellow to move.", vm.Status);
        Assert.Equal("Human vs human", vm.SidesText);
        Assert.False(vm.SwitchSidesCommand.CanExecute(null));

        vm.PlayCommand.Execute(3);

        Assert.Equal(2, vm.Game.Ply);
        Assert.Equal("", vm.Analysis.Depth);
    }

    [Fact]
    public async Task HumanVsHuman_UndoTakesBackOneMove()
    {
        MainViewModel vm = Create(GameMode.HumanVsHuman);
        vm.PlayCommand.Execute(3);
        vm.PlayCommand.Execute(4);

        await vm.UndoCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.Game.Ply);
        Assert.Equal("Yellow to move.", vm.Status);
    }

    [Fact]
    public async Task SetMode_ToHumanVsHuman_StopsTheComputer()
    {
        MainViewModel vm = Create(settings: SlowSettings);
        vm.PlayCommand.Execute(3);
        Assert.True(vm.IsThinking);

        await vm.SetModeCommand.ExecuteAsync(GameMode.HumanVsHuman);

        Assert.False(vm.IsThinking);
        Assert.Equal(1, vm.Game.Ply);
        Assert.Equal("Yellow to move.", vm.Status);
        Assert.True(vm.IsHumanVsHuman);
        Assert.Equal(GameMode.HumanVsHuman, _store.Settings.Mode);
    }

    [Fact]
    public async Task SetMode_ToHumanVsComputer_TheHumanKeepsTheSideToMove()
    {
        MainViewModel vm = Create(GameMode.HumanVsHuman);
        vm.PlayCommand.Execute(3);

        await vm.SetModeCommand.ExecuteAsync(GameMode.HumanVsComputer);

        Assert.Equal(Player.Yellow, vm.Human);
        Assert.Equal(1, vm.Game.Ply);
        Assert.Equal("Your move.", vm.Status);
        Assert.True(vm.SwitchSidesCommand.CanExecute(null));
    }

    [Fact]
    public async Task WinningMove_EndsTheGameWithTheWinSound()
    {
        MainViewModel vm = await OpenAsync(Create(), "121212");

        vm.PlayCommand.Execute(0);
        await vm.Idle;

        Assert.Equal("Game over, you win.", vm.Status);
        Assert.Equal(4, vm.Cells.Count(cell => cell.IsWinning));
        Assert.Equal([Sound.Drop, Sound.Win], _sounds.Played);
    }

    [Fact]
    public async Task ComputerWins_WithTheLossSound()
    {
        // Yellow to move must block column 1; it does not.
        MainViewModel vm = await OpenAsync(Create(), "12121");

        vm.PlayCommand.Execute(6);
        await vm.Idle;

        Assert.Equal("Game over, the computer wins.", vm.Status);
        Assert.Equal([Sound.Drop, Sound.Drop, Sound.Loss], _sounds.Played);
    }

    [Fact]
    public async Task HumanVsHuman_Draw()
    {
        string draw = FindDraw();
        MainViewModel vm = await OpenAsync(Create(GameMode.HumanVsHuman), draw[..^1]);

        vm.PlayCommand.Execute(draw[^1] - '1');

        Assert.Equal("Game over, a draw.", vm.Status);
        Assert.Equal([Sound.Drop, Sound.Draw], _sounds.Played);
    }

    [Fact]
    public async Task SoundOff_PlaysNothingAndIsSaved()
    {
        MainViewModel vm = Create();

        vm.ToggleSoundCommand.Execute(null);
        vm.PlayCommand.Execute(3);
        await vm.Idle;

        Assert.Empty(_sounds.Played);
        Assert.False(_store.Settings.SoundOn);
    }

    [Fact]
    public async Task Open_TheHumanContinuesWithTheSideToMove()
    {
        MainViewModel vm = await OpenAsync(Create(), "4");

        Assert.Equal(Player.Yellow, vm.Human);
        Assert.Equal("Your move.", vm.Status);
        Assert.Equal($"Connect 4 – {GameName}", vm.Title);
    }

    [Fact]
    public async Task Open_InvalidRecord_ShowsAnError()
    {
        MainViewModel vm = await OpenAsync(Create(), "1111111");

        Assert.Single(_dialogs.Errors);
        Assert.Equal(0, vm.Game.Ply);
    }

    [Fact]
    public async Task Open_MissingFile_ShowsAnError()
    {
        MainViewModel vm = Create();
        _files.OpenName = "missing.txt";

        await vm.OpenCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Errors);
    }

    [Fact]
    public async Task Save_WritesTheMoves()
    {
        MainViewModel vm = Create(GameMode.HumanVsHuman);
        vm.PlayCommand.Execute(3);
        vm.PlayCommand.Execute(3);
        _files.SaveName = GameName;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("44" + Environment.NewLine, _files.Files[GameName]);
        Assert.Equal($"Connect 4 – {GameName}", vm.Title);
    }

    [Fact]
    public async Task EditSettings_AppliesAndSaves()
    {
        MainViewModel vm = Create();
        _dialogs.NewSettings = new GameSettings(TimeControlMode.TimePerGame, 6, 5, 3, 10);

        await vm.EditSettingsCommand.ExecuteAsync(null);

        Assert.Equal(_dialogs.NewSettings, vm.Settings);
        Assert.Equal(_dialogs.NewSettings, _store.Settings.Game);
        Assert.Equal("Computer time left: 3:00", vm.ClockText);
    }

    [Fact]
    public async Task EditSettings_Cancel_KeepsTheSettings()
    {
        MainViewModel vm = Create();

        await vm.EditSettingsCommand.ExecuteAsync(null);

        Assert.Equal(QuickSettings, vm.Settings);
        Assert.Equal(0, _store.Saves);
    }

    [Fact]
    public void ToggleAnalysis_IsSaved()
    {
        MainViewModel vm = Create();

        vm.ToggleAnalysisCommand.Execute(null);

        Assert.False(vm.IsAnalysisVisible);
        Assert.False(_store.Settings.ShowAnalysis);
    }

    [Fact]
    public void About_ShowsTheDialog()
    {
        Create().AboutCommand.Execute(null);

        Assert.Equal(1, _dialogs.AboutShown);
    }

    [Fact]
    public void SaveFails_TellsTheUser()
    {
        MainViewModel vm = Create();
        _store.CanSave = false;

        vm.ToggleAnalysisCommand.Execute(null);

        Assert.StartsWith("The settings could not be saved.", vm.Status);
    }

    private MainViewModel Create(GameMode mode = GameMode.HumanVsComputer, GameSettings? settings = null, string? notice = null)
    {
        _store = new FakeSettingsStore(AppSettings.Default with { Game = settings ?? QuickSettings, Mode = mode });
        var engine = new LocalEngineHost(new ComputerPlayer(new SearchEngine(hashLogSize: 16, endgameLogSize: 17, random: new Random(1))));
        return new MainViewModel(engine, _dialogs, _files, _store, _sounds, notice);
    }

    private async Task<MainViewModel> OpenAsync(MainViewModel vm, string moves)
    {
        _files.Files[GameName] = moves;
        _files.OpenName = GameName;
        await vm.OpenCommand.ExecuteAsync(null);
        await vm.Idle;
        return vm;
    }

    private static CellViewModel Cell(MainViewModel vm, int column, int row) =>
        vm.Cells.Single(cell => cell.Column == column && cell.Row == row);

    private static string FindDraw()
    {
        var random = new Random(1);
        while (true)
        {
            var game = new Game();
            while (!game.IsGameOver)
            {
                int[] columns = [.. Enumerable.Range(0, Position.Width).Where(game.CanPlay)];
                game.Play(columns[random.Next(columns.Length)]);
            }

            if (game.IsDraw)
            {
                return GameRecordFormat.Format(game);
            }
        }
    }
}
