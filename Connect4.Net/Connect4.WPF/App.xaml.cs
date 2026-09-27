using System.Windows;
using Connect4.App.Services;
using Connect4.App.ViewModels;
using Connect4.Engine;
using Connect4.WPF.Services;

namespace Connect4.WPF;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var engine = new SearchEngine();
        string? notice = engine.UsesFallbackTables ? "Low memory: the computer uses a smaller hash table." : null;
        var viewModel = new MainViewModel(
            new LocalEngineHost(new ComputerPlayer(engine)),
            new DialogService(),
            new GameFileService(),
            new JsonSettingsStore(AppPaths.SettingsFile),
            new SoundService(),
            notice);
        MainWindow = new MainWindow(viewModel);
        MainWindow.Show();
    }
}
