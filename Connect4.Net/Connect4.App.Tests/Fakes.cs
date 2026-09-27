using System.IO;
using Connect4.App.Models;
using Connect4.App.Services;

namespace Connect4.App.Tests;

internal sealed class FakeDialogService : IDialogService
{
    public GameSettings? NewSettings { get; set; }

    public List<string> Errors { get; } = [];

    public int Beeps { get; private set; }

    public int AboutShown { get; private set; }

    public Task<GameSettings?> EditSettingsAsync(GameSettings current) => Task.FromResult(NewSettings);

    public void ShowError(string message) => Errors.Add(message);

    public void ShowAbout() => AboutShown++;

    public void Beep() => Beeps++;
}

/// <summary>Game files in memory; the name the user would pick in the dialogs is set beforehand.</summary>
internal sealed class FakeGameFileService : IGameFileService
{
    public Dictionary<string, string> Files { get; } = [];

    public string? OpenName { get; set; }

    public string? SaveName { get; set; }

    public Task<GameFile?> OpenAsync()
    {
        if (OpenName is null)
        {
            return Task.FromResult<GameFile?>(null);
        }

        return Files.TryGetValue(OpenName, out string? text)
            ? Task.FromResult<GameFile?>(new GameFile(OpenName, text))
            : Task.FromException<GameFile?>(new FileNotFoundException($"Could not find file '{OpenName}'."));
    }

    public Task<string?> SaveAsync(string text, string? currentName, bool askForName)
    {
        string? name = askForName || currentName is null ? SaveName : currentName;
        if (name is not null)
        {
            Files[name] = text;
        }

        return Task.FromResult(name);
    }
}

internal sealed class FakeSettingsStore(AppSettings settings) : ISettingsStore
{
    public AppSettings Settings { get; private set; } = settings;

    public int Saves { get; private set; }

    public bool CanSave { get; set; } = true;

    public AppSettings Load() => Settings;

    public bool Save(AppSettings settings)
    {
        if (!CanSave)
        {
            return false;
        }

        Settings = settings;
        Saves++;
        return true;
    }
}

internal sealed class FakeSoundService : ISoundService
{
    public List<Sound> Played { get; } = [];

    public void Play(Sound sound) => Played.Add(sound);
}
