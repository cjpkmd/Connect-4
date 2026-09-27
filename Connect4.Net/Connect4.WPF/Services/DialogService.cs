using System.Media;
using System.Windows;
using Connect4.App.Models;
using Connect4.App.Services;
using Connect4.App.ViewModels;
using Connect4.WPF.Views;

namespace Connect4.WPF.Services;

internal sealed class DialogService : IDialogService
{
    private const string Caption = "Connect 4";

    private static Window? Owner => Application.Current?.MainWindow;

    public Task<GameSettings?> EditSettingsAsync(GameSettings current)
    {
        var viewModel = new SettingsViewModel(current);
        var window = new SettingsWindow { Owner = Owner, DataContext = viewModel };
        return Task.FromResult(window.ShowDialog() == true ? viewModel.ToSettings() : null);
    }

    public void ShowError(string message)
    {
        if (Owner is { } owner)
        {
            MessageBox.Show(owner, message, Caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            MessageBox.Show(message, Caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ShowAbout() => new AboutWindow { Owner = Owner }.ShowDialog();

    public void Beep() => SystemSounds.Beep.Play();
}
