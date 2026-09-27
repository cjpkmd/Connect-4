namespace Connect4.Engine;

/// <summary>
/// Chooses the computer's move in a game. There is no opening book, so every move is searched.
/// Not thread-safe; run one move at a time.
/// </summary>
public sealed class ComputerPlayer(SearchEngine engine)
{
    /// <summary>True when the engine had to use smaller hash tables than requested.</summary>
    public bool UsesFallbackTables => engine.UsesFallbackTables;

    /// <param name="cancellationToken">Stops the search and throws <see cref="OperationCanceledException"/>.</param>
    /// <param name="moveNowToken">Stops the search and returns the best move found so far.</param>
    /// <exception cref="InvalidOperationException">The game is over.</exception>
    public SearchResult ChooseMove(
        Game game,
        SearchLimits limits,
        IProgress<SearchInfo>? progress = null,
        CancellationToken cancellationToken = default,
        CancellationToken moveNowToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.IsGameOver)
        {
            throw new InvalidOperationException("The game is over.");
        }

        return engine.Search(game.Position, limits, progress, cancellationToken, moveNowToken);
    }

    /// <summary>Forgets the search results of the previous game.</summary>
    public void NewGame() => engine.ClearHash();
}
