using System.IO;

namespace Connect4.WPF.Services;

/// <summary>Where the app keeps its files; nothing depends on the current directory.</summary>
public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Connect4");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
}
