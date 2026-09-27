using CommunityToolkit.Mvvm.ComponentModel;
using Connect4.App.Models;
using Connect4.Engine;

namespace Connect4.App.ViewModels;

/// <summary>The settings dialog: Stello's time control plus the endgame threshold.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFixedDepth), nameof(IsTimePerMove), nameof(IsTimePerGame))]
    private TimeControlMode _mode;

    [ObservableProperty]
    private int _depth;

    [ObservableProperty]
    private int _secondsPerMove;

    [ObservableProperty]
    private int _minutesPerGame;

    [ObservableProperty]
    private int _endgameThreshold;

    public SettingsViewModel(GameSettings settings)
    {
        Mode = settings.Mode;
        Depth = settings.Depth;
        SecondsPerMove = settings.SecondsPerMove;
        MinutesPerGame = settings.MinutesPerGame;
        EndgameThreshold = settings.EndgameThreshold;
    }

    public int MaxDepth => GameSettings.MaxDepth;

    public int MaxSecondsPerMove => GameSettings.MaxSecondsPerMove;

    public int MaxMinutesPerGame => GameSettings.MaxMinutesPerGame;

    public int MaxEndgameThreshold => GameSettings.MaxEndgameThreshold;

    public bool IsFixedDepth
    {
        get => Mode == TimeControlMode.FixedDepth;
        set => SelectMode(value, TimeControlMode.FixedDepth);
    }

    public bool IsTimePerMove
    {
        get => Mode == TimeControlMode.TimePerMove;
        set => SelectMode(value, TimeControlMode.TimePerMove);
    }

    public bool IsTimePerGame
    {
        get => Mode == TimeControlMode.TimePerGame;
        set => SelectMode(value, TimeControlMode.TimePerGame);
    }

    public GameSettings ToSettings() =>
        new GameSettings(Mode, Depth, SecondsPerMove, MinutesPerGame, EndgameThreshold).Normalize();

    private void SelectMode(bool selected, TimeControlMode mode)
    {
        if (selected)
        {
            Mode = mode;
        }
    }
}
