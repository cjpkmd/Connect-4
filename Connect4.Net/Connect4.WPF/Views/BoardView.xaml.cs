using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Connect4.App.ViewModels;
using Connect4.Engine;

namespace Connect4.WPF.Views;

/// <summary>The board: click a column to drop a disc. New discs fall into place; clicks are ignored while they fall.</summary>
public partial class BoardView : UserControl
{
    private const double CellSize = 60;

    // Seconds for a disc to fall one cell at the start; a fall from the top takes about 0.4 s.
    private const double FallSecondsPerSquareRootCell = 0.15;

    private readonly List<TranslateTransform> _falling = [];
    private MainViewModel? _viewModel;
    private int _newDiscs;

    public BoardView()
    {
        InitializeComponent();
        for (int column = 0; column < Position.Width; column++)
        {
            var button = new Button
            {
                Template = (ControlTemplate)Resources["ColumnTemplate"],
                Tag = column,
                Focusable = false,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            AutomationProperties.SetName(button, $"Column {column + 1}");
            button.Click += OnColumnClick;
            ColumnButtons.Children.Add(button);
        }

        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>True while a disc is falling.</summary>
    public bool IsAnimating => _falling.Count > 0;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            foreach (CellViewModel cell in _viewModel.Cells)
            {
                cell.PropertyChanged -= OnCellChanged;
            }
        }

        _viewModel = e.NewValue as MainViewModel;
        if (_viewModel is not null)
        {
            foreach (CellViewModel cell in _viewModel.Cells)
            {
                cell.PropertyChanged += OnCellChanged;
            }
        }
    }

    private void OnColumnClick(object sender, RoutedEventArgs e)
    {
        if (!IsAnimating && _viewModel is not null)
        {
            _viewModel.PlayCommand.Execute((int)((Button)sender).Tag);
        }
    }

    private void OnCellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CellViewModel.Disc) || sender is not CellViewModel { Disc: not null } cell)
        {
            return;
        }

        AnimateFall(cell);

        // Many discs at once (open a game, redo) are shown at once; checked before the next render.
        if (_newDiscs++ == 0)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                if (_newDiscs > 2)
                {
                    StopAll();
                }

                _newDiscs = 0;
            });
        }
    }

    private void AnimateFall(CellViewModel cell)
    {
        if (CellsControl.ItemContainerGenerator.ContainerFromItem(cell) is not ContentPresenter container
            || container.ContentTemplate.FindName("Disc", container) is not Ellipse disc)
        {
            return;
        }

        // From the preview row above the board down to the cell.
        int cells = Position.Height - cell.Row;
        var transform = new TranslateTransform();
        disc.RenderTransform = transform;
        var fall = new DoubleAnimation(-cells * CellSize, 0, TimeSpan.FromSeconds(FallSecondsPerSquareRootCell * Math.Sqrt(cells)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        fall.Completed += (_, _) => _falling.Remove(transform);
        _falling.Add(transform);
        transform.BeginAnimation(TranslateTransform.YProperty, fall);
    }

    private void StopAll()
    {
        foreach (TranslateTransform transform in _falling)
        {
            transform.BeginAnimation(TranslateTransform.YProperty, null);
        }

        _falling.Clear();
    }
}
