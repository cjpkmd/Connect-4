using Connect4.Engine;

namespace Connect4.App.Services;

/// <summary>Runs the engine; the desktop runs it in-process, the browser in a Web Worker.</summary>
public interface IEngineHost
{
    /// <param name="moves">The game so far as 0-based columns from the empty board.</param>
    /// <param name="cancellationToken">Stops the search and throws <see cref="OperationCanceledException"/>.</param>
    /// <param name="moveNowToken">Stops the search and returns the best move found so far.</param>
    Task<SearchResult> ChooseMoveAsync(
        IReadOnlyList<int> moves,
        SearchLimits limits,
        IProgress<SearchInfo>? progress,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken);

    /// <summary>Forgets the search results of the previous game.</summary>
    void NewGame();
}
