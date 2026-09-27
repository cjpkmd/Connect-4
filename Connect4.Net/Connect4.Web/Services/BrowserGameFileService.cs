using Microsoft.JSInterop;
using Connect4.App.Services;

namespace Connect4.Web.Services;

/// <summary>Opens a game with the browser's file picker and saves it as a download.</summary>
public sealed class BrowserGameFileService(IJSRuntime js, BrowserDialogService dialogs) : IGameFileService
{
    private const string DefaultExtension = ".txt";

    public async Task<GameFile?> OpenAsync()
    {
        string[]? file;
        try
        {
            file = await js.InvokeAsync<string[]?>("connect4.openTextFile", DefaultExtension);
        }
        catch (JSException exception)
        {
            throw new IOException(exception.Message, exception);
        }

        return file is [string name, string text] ? new GameFile(name, text) : null;
    }

    public async Task<string?> SaveAsync(string text, string? currentName, bool askForName)
    {
        string? name = currentName;
        if (askForName || name is null)
        {
            name = (await dialogs.AskTextAsync("Save the game as:", currentName ?? "Game" + DefaultExtension))?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (Path.GetExtension(name).Length == 0)
            {
                name += DefaultExtension;
            }
        }

        await js.InvokeVoidAsync("connect4.downloadTextFile", name, text);
        return name;
    }
}
