using System.Windows;

namespace Connect4.WPF.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => InitializeComponent();

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
