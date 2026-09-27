using System.IO;
using System.Windows;
using Connect4.App.Services;
using Microsoft.Win32;

namespace Connect4.WPF.Services;

internal sealed class GameFileService : IGameFileService
{
    private const string GameFilter = "Connect 4 games (*.txt)|*.txt|All files (*.*)|*.*";

    private static Window? Owner => Application.Current?.MainWindow;

    public async Task<GameFile?> OpenAsync()
    {
        var dialog = new OpenFileDialog { Filter = GameFilter, DefaultExt = ".txt" };
        return dialog.ShowDialog(Owner) == true
            ? new GameFile(dialog.FileName, await File.ReadAllTextAsync(dialog.FileName))
            : null;
    }

    public Task<string?> SaveAsync(string text, string? currentName, bool askForName)
    {
        string? path = askForName || currentName is null ? AskForPath(currentName) : currentName;
        if (path is not null)
        {
            File.WriteAllText(path, text);
        }

        return Task.FromResult(path);
    }

    private static string? AskForPath(string? currentPath)
    {
        var dialog = new SaveFileDialog
        {
            Filter = GameFilter,
            DefaultExt = ".txt",
            FileName = currentPath is null ? "Game" : Path.GetFileName(currentPath),
            InitialDirectory = currentPath is null ? "" : Path.GetDirectoryName(currentPath),
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }
}
