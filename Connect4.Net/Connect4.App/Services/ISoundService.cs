namespace Connect4.App.Services;

public enum Sound
{
    Drop,
    Win,
    Loss,
    Draw,
}

/// <summary>Plays the game sounds; the view model only calls it when the sound is on.</summary>
public interface ISoundService
{
    void Play(Sound sound);
}
