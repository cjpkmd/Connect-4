using CommunityToolkit.Mvvm.ComponentModel;
using Connect4.Engine;

namespace Connect4.App.ViewModels;

/// <summary>One cell of the board; column and row are 0-based, row 0 at the bottom.</summary>
public sealed partial class CellViewModel(int column, int row) : ObservableObject
{
    [ObservableProperty]
    private Player? _disc;

    [ObservableProperty]
    private bool _isLastMove;

    [ObservableProperty]
    private bool _isWinning;

    public int Column { get; } = column;

    public int Row { get; } = row;
}
