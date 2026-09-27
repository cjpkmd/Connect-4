using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Connect4.Engine;

namespace Connect4.App.ViewModels;

/// <summary>The search shown while the computer thinks. Columns are shown 1-based and scores from Red's view.</summary>
public sealed partial class AnalysisViewModel : ObservableObject
{
    [ObservableProperty]
    private string _move = "";

    [ObservableProperty]
    private string _depth = "";

    [ObservableProperty]
    private string _value = "";

    [ObservableProperty]
    private string _bestMove = "";

    [ObservableProperty]
    private string _line = "";

    [ObservableProperty]
    private string _nodes = "";

    [ObservableProperty]
    private string _time = "";

    public void Clear()
    {
        Move = Depth = Value = BestMove = Line = Nodes = Time = "";
    }

    /// <param name="position">The position being searched.</param>
    public void Update(SearchInfo info, Position position)
    {
        Move = FormatColumn(info.Column);
        Depth = info.Solving ? $"solving ({info.Depth} empty)" : FormatDepth(info.Depth, info.Kind);
        Value = FormatScore(info.Score, info.Kind, position);
        BestMove = info.BestColumn is { } best ? FormatColumn(best) : "";
        Line = FormatLine(info.PrincipalVariation);
        Nodes = info.Nodes.ToString("N0", CultureInfo.CurrentCulture);
        Time = FormatTime(info.Elapsed);
    }

    public void Update(SearchResult result, Position position, TimeSpan elapsed)
    {
        Move = BestMove = FormatColumn(result.Column);
        Depth = FormatDepth(result.Depth, result.Kind);
        Value = FormatScore(result.Score, result.Kind, position);
        Line = FormatLine(result.PrincipalVariation);
        Nodes = result.Nodes.ToString("N0", CultureInfo.CurrentCulture);
        Time = FormatTime(elapsed);
    }

    /// <summary>A score for the side to move in <paramref name="position"/>, shown from Red's view.</summary>
    public static string FormatScore(int score, ScoreKind kind, Position position)
    {
        if (kind == ScoreKind.None)
        {
            return "";
        }

        if (Scores.IsDecided(score))
        {
            Player winner = score > 0 ? position.ToMove : position.ToMove.Opponent();
            int moveNumber = Scores.WinningMoveNumber(score);
            int moves = winner == Player.Red
                ? (moveNumber + 1) / 2 - (position.Moves + 1) / 2
                : moveNumber / 2 - position.Moves / 2;
            return $"{winner} wins in {moves} move{(moves == 1 ? "" : "s")}";
        }

        if (kind == ScoreKind.Exact)
        {
            return "Draw";
        }

        int red = position.ToMove == Player.Red ? score : -score;
        return red.ToString("+0;-0;0", CultureInfo.CurrentCulture);
    }

    private static string FormatDepth(int depth, ScoreKind kind) => kind == ScoreKind.None ? "" : $"{depth} plies";

    private static string FormatColumn(int column) => (column + 1).ToString(CultureInfo.InvariantCulture);

    private static string FormatLine(IReadOnlyList<int> line) => string.Concat(line.Select(FormatColumn));

    private static string FormatTime(TimeSpan elapsed) => elapsed.ToString(@"m\:ss\.f", CultureInfo.CurrentCulture);
}
