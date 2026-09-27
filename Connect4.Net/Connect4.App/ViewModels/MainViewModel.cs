using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Connect4.App.Models;
using Connect4.App.Services;
using Connect4.Engine;

namespace Connect4.App.ViewModels;

/// <summary>
/// The game window: a human plays against the computer or against another human. The computer thinks in the
/// background; commands that change the game stop it first.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IEngineHost _engine;
    private readonly IDialogService _dialogs;
    private readonly IGameFileService _files;
    private readonly ISettingsStore _settingsStore;
    private readonly ISoundService _sounds;
    private readonly bool _settingsLoaded;

    // Computer clock before its move at each ply, so taking back moves also gives the time back.
    private readonly Dictionary<int, TimeSpan> _timeLeftAtPly = [];

    private Game _game = new();
    private Player _human = Player.Red;
    private string? _fileName;
    private string? _notice;
    private TimeSpan _computerTimeLeft;
    private CancellationTokenSource? _cancel;
    private CancellationTokenSource? _moveNow;
    private int _searchId;

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private string _sidesText = "";

    [ObservableProperty]
    private string _clockText = "";

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveNowCommand))]
    private bool _isThinking;

    [ObservableProperty]
    private bool _isAnalysisVisible = true;

    [ObservableProperty]
    private bool _isSoundOn = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHumanVsComputer), nameof(IsHumanVsHuman))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSidesCommand))]
    private GameMode _mode;

    [ObservableProperty]
    private GameSettings _settings = GameSettings.Default;

    public MainViewModel(
        IEngineHost engine,
        IDialogService dialogs,
        IGameFileService files,
        ISettingsStore settingsStore,
        ISoundService sounds,
        string? startupNotice = null)
    {
        _engine = engine;
        _dialogs = dialogs;
        _files = files;
        _settingsStore = settingsStore;
        _sounds = sounds;

        AppSettings saved = settingsStore.Load().Normalize();
        Settings = saved.Game;
        Mode = saved.Mode;
        IsAnalysisVisible = saved.ShowAnalysis;
        IsSoundOn = saved.SoundOn;
        WindowPlacement = saved.Window;
        _settingsLoaded = true;

        _computerTimeLeft = Settings.GameTime;
        _notice = startupNotice;

        // Top row first, left to right, as a grid is filled.
        Cells = [.. Enumerable.Range(0, Position.Height).Reverse()
            .SelectMany(row => Enumerable.Range(0, Position.Width).Select(column => new CellViewModel(column, row)))];
        Start();
    }

    /// <summary>The 42 cells, top row first.</summary>
    public IReadOnlyList<CellViewModel> Cells { get; }

    public AnalysisViewModel Analysis { get; } = new();

    public bool IsHumanVsComputer => Mode == GameMode.HumanVsComputer;

    public bool IsHumanVsHuman => Mode == GameMode.HumanVsHuman;

    /// <summary>The saved window position, or null for the default position.</summary>
    public WindowPlacement? WindowPlacement { get; private set; }

    /// <summary>Completes when the computer has moved and a human is to move or the game is over.</summary>
    internal Task Idle { get; private set; } = Task.CompletedTask;

    internal Game Game => _game;

    internal Player Human => _human;

    private bool IsHumanToMove => Mode == GameMode.HumanVsHuman || _game.ToMove == _human;

    /// <summary>Stops the computer without waiting, e.g. when the window closes.</summary>
    public void Stop() => _cancel?.Cancel();

    public void SaveWindowPlacement(WindowPlacement placement)
    {
        WindowPlacement = placement;
        SaveSettings();
    }

    partial void OnIsAnalysisVisibleChanged(bool value) => SaveSettings();

    partial void OnIsSoundOnChanged(bool value) => SaveSettings();

    private void SaveSettings()
    {
        if (_settingsLoaded && !_settingsStore.Save(new AppSettings(Settings, Mode, IsAnalysisVisible, IsSoundOn, WindowPlacement)))
        {
            Status = $"The settings could not be saved. {Status}";
        }
    }

    /// <summary>Drops a disc in the 0-based column for the human to move.</summary>
    [RelayCommand]
    private void Play(int column)
    {
        if (IsThinking || !IsHumanToMove || column is < 0 or >= Position.Width || !_game.CanPlay(column))
        {
            _dialogs.Beep();
            return;
        }

        _game.Play(column);
        PlayMoveSounds();
        Start();
    }

    [RelayCommand]
    private async Task NewGame()
    {
        await StopAsync();
        _game.NewGame();
        _fileName = null;
        ResetClock();
        _engine.NewGame();
        Analysis.Clear();
        Start();
    }

    [RelayCommand]
    private async Task Open()
    {
        GameFile? file;
        Game loaded;
        try
        {
            file = await _files.OpenAsync();
            if (file is null)
            {
                return;
            }

            loaded = GameRecordFormat.Parse(file.Text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            _dialogs.ShowError($"The game could not be opened.\n\n{exception.Message}");
            return;
        }

        await StopAsync();

        // As in Stello, the human continues with the side to move.
        _human = loaded.ToMove;
        _game = loaded;
        _fileName = file.Name;
        ResetClock();
        Analysis.Clear();
        Start();
    }

    [RelayCommand]
    private Task Save() => WriteAsync(askForName: false);

    [RelayCommand]
    private Task SaveAs() => WriteAsync(askForName: true);

    /// <summary>The human takes the computer's colour; the computer moves at once if it is its turn.</summary>
    [RelayCommand(CanExecute = nameof(IsHumanVsComputer))]
    private async Task SwitchSides()
    {
        await StopAsync();
        _human = _human.Opponent();
        Start();
    }

    [RelayCommand]
    private async Task SetMode(GameMode mode)
    {
        if (mode == Mode)
        {
            return;
        }

        await StopAsync();
        Mode = mode;

        // The human keeps the side to move; the computer takes the other colour.
        _human = _game.ToMove;
        SaveSettings();
        Analysis.Clear();
        Start();
    }

    [RelayCommand(CanExecute = nameof(IsThinking))]
    private void MoveNow() => _moveNow?.Cancel();

    /// <summary>Against the computer: back to the previous position where the human is to move. Else one move.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task Undo()
    {
        await StopAsync();
        do
        {
            _game.Undo();
        }
        while (_game.CanUndo && !IsHumanToMove);

        RestoreClock();
        Start();
    }

    /// <summary>Against the computer: forward to the next position where the human is to move, or to the end.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private async Task Redo()
    {
        await StopAsync();
        do
        {
            _game.Redo();
        }
        while (_game.CanRedo && !IsHumanToMove);

        RestoreClock();
        Start();
    }

    [RelayCommand]
    private async Task EditSettings()
    {
        GameSettings? settings = await _dialogs.EditSettingsAsync(Settings);
        if (settings is null)
        {
            return;
        }

        bool clockChanged = settings.Mode != Settings.Mode || settings.MinutesPerGame != Settings.MinutesPerGame;
        Settings = settings.Normalize();
        SaveSettings();
        if (clockChanged)
        {
            ResetClock();
        }

        Refresh();
    }

    [RelayCommand]
    private void ToggleAnalysis() => IsAnalysisVisible = !IsAnalysisVisible;

    [RelayCommand]
    private void ToggleSound() => IsSoundOn = !IsSoundOn;

    [RelayCommand]
    private void About() => _dialogs.ShowAbout();

    private bool CanUndo() => _game.CanUndo;

    private bool CanRedo() => _game.CanRedo;

    private void Start() => Idle = RunAsync();

    private async Task StopAsync()
    {
        _cancel?.Cancel();
        await Idle;
    }

    // Plays computer moves until a human is to move or the game is over.
    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                Refresh();
                if (_game.IsGameOver)
                {
                    Status = WithNotice(GameOverText());
                    return;
                }

                if (IsHumanToMove)
                {
                    Status = WithNotice(Mode == GameMode.HumanVsHuman ? $"{_game.ToMove} to move." : "Your move.");
                    return;
                }

                Status = WithNotice("Thinking…");
                if (!await ComputerMoveAsync())
                {
                    Refresh();
                    Status = "Stopped.";
                    return;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Status = "Error.";
            _dialogs.ShowError($"Something went wrong.\n\n{exception.Message}");
        }
    }

    private async Task<bool> ComputerMoveAsync()
    {
        using var cancel = new CancellationTokenSource();
        using var moveNow = new CancellationTokenSource();
        _cancel = cancel;
        _moveNow = moveNow;
        IsThinking = true;
        Analysis.Clear();

        Position position = _game.Position;
        _timeLeftAtPly[_game.Ply] = _computerTimeLeft;
        SearchLimits limits = Settings.ToLimits(_computerTimeLeft);

        int searchId = ++_searchId;
        var progress = new Progress<SearchInfo>(info =>
        {
            // Reports can arrive after the search has finished.
            if (searchId == _searchId && IsThinking)
            {
                Analysis.Update(info, position);
            }
        });

        var stopwatch = Stopwatch.StartNew();
        try
        {
            SearchResult result = await _engine.ChooseMoveAsync(
                [.. _game.PlayedMoves], limits, progress, cancel.Token, moveNow.Token);

            _computerTimeLeft -= stopwatch.Elapsed;
            Analysis.Update(result, position, stopwatch.Elapsed);
            _game.Play(result.Column);
            PlayMoveSounds();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _cancel = null;
            _moveNow = null;
            IsThinking = false;
        }
    }

    private void PlayMoveSounds()
    {
        if (!IsSoundOn)
        {
            return;
        }

        _sounds.Play(Sound.Drop);
        if (_game.IsGameOver)
        {
            _sounds.Play(_game.Winner switch
            {
                null => Sound.Draw,
                Player winner when Mode == GameMode.HumanVsComputer && winner != _human => Sound.Loss,
                _ => Sound.Win,
            });
        }
    }

    private void Refresh()
    {
        Position position = _game.Position;
        int? lastColumn = _game.LastMove;
        int lastRow = lastColumn is { } column ? TopRow(position, column) : -1;
        ulong winning = _game.WinningLine;

        foreach (CellViewModel cell in Cells)
        {
            cell.Disc = position[cell.Column, cell.Row];
            cell.IsLastMove = cell.Column == lastColumn && cell.Row == lastRow;
            cell.IsWinning = (winning & Position.CellBit(cell.Column, cell.Row)) != 0;
        }

        if (Mode == GameMode.HumanVsHuman)
        {
            Analysis.Clear();
        }

        SidesText = Mode == GameMode.HumanVsComputer ? $"You play {_human}" : "Human vs human";
        TimeSpan left = _computerTimeLeft > TimeSpan.Zero ? _computerTimeLeft : TimeSpan.Zero;
        ClockText = Mode == GameMode.HumanVsComputer && Settings.Mode == TimeControlMode.TimePerGame
            ? $"Computer time left: {(int)left.TotalMinutes}:{left.Seconds:00}"
            : "";
        Title = $"Connect 4 – {(_fileName is null ? "Untitled" : Path.GetFileName(_fileName))}";
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private static int TopRow(Position position, int column)
    {
        for (int row = Position.Height - 1; row >= 0; row--)
        {
            if (position[column, row] is not null)
            {
                return row;
            }
        }

        return -1;
    }

    private string GameOverText() => (_game.Winner, Mode) switch
    {
        (null, _) => "Game over, a draw.",
        ({ } winner, GameMode.HumanVsHuman) => $"Game over, {winner} wins.",
        ({ } winner, _) when winner == _human => "Game over, you win.",
        _ => "Game over, the computer wins.",
    };

    private string WithNotice(string status)
    {
        string text = _notice is null ? status : $"{_notice} {status}";
        _notice = null;
        return text;
    }

    private void ResetClock()
    {
        _computerTimeLeft = Settings.GameTime;
        _timeLeftAtPly.Clear();
    }

    private void RestoreClock()
    {
        int next = _timeLeftAtPly.Keys.Where(ply => ply >= _game.Ply).DefaultIfEmpty(-1).Min();
        if (next >= 0)
        {
            _computerTimeLeft = _timeLeftAtPly[next];
        }
    }

    private async Task WriteAsync(bool askForName)
    {
        try
        {
            string? name = await _files.SaveAsync(GameRecordFormat.Format(_game) + Environment.NewLine, _fileName, askForName);
            if (name is not null)
            {
                _fileName = name;
                Refresh();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"The game could not be saved.\n\n{exception.Message}");
        }
    }
}
