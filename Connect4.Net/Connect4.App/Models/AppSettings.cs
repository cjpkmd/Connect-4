namespace Connect4.App.Models;

public enum GameMode
{
    HumanVsComputer,
    HumanVsHuman,
}

/// <summary>Everything saved between sessions.</summary>
public sealed record AppSettings(GameSettings Game, GameMode Mode, bool ShowAnalysis, bool SoundOn, WindowPlacement? Window)
{
    public static AppSettings Default { get; } =
        new(GameSettings.Default, GameMode.HumanVsComputer, ShowAnalysis: true, SoundOn: true, Window: null);

    public AppSettings Normalize() => this with
    {
        Game = (Game ?? GameSettings.Default).Normalize(),
        Mode = Enum.IsDefined(Mode) ? Mode : GameMode.HumanVsComputer,
        Window = Window is { Width: > 0, Height: > 0 } ? Window : null,
    };
}

/// <summary>Window position and size in device-independent units; the size is the restored size when maximised.</summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized);
