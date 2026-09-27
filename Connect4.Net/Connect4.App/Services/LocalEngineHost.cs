using Connect4.Engine;

namespace Connect4.App.Services;

/// <summary>The engine on a thread-pool thread in this process, as in the desktop app.</summary>
public sealed class LocalEngineHost(ComputerPlayer computer) : IEngineHost
{
    public Task<SearchResult> ChooseMoveAsync(
        IReadOnlyList<int> moves,
        SearchLimits limits,
        IProgress<SearchInfo>? progress,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken)
    {
        // The engine gets its own copy, so the caller's game is never touched from another thread.
        var game = new Game();
        foreach (int column in moves)
        {
            game.Play(column);
        }

        return Task.Run(() => computer.ChooseMove(game, limits, progress, cancellationToken, moveNowToken), CancellationToken.None);
    }

    public void NewGame() => computer.NewGame();
}
