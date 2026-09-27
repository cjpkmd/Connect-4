using Connect4.App.Models;

namespace Connect4.App.Services;

public interface IDialogService
{
    /// <returns>The new settings, or null if the user cancelled.</returns>
    Task<GameSettings?> EditSettingsAsync(GameSettings current);

    void ShowError(string message);

    void ShowAbout();

    void Beep();
}
