namespace Connect4.Engine;

/// <summary>
/// A game from the empty board with its move history. Moves after the current position are kept
/// until a new move is played, so undone moves can be redone. Columns are 0-based.
/// </summary>
public sealed class Game
{
    private readonly List<int> _moves = [];
    private readonly List<State> _states = [new(Position.Empty, null, 0)];

    /// <summary>Number of moves played up to the current position.</summary>
    public int Ply { get; private set; }

    /// <summary>All moves in the history, including moves after the current position.</summary>
    public IReadOnlyList<int> Moves => _moves;

    public IEnumerable<int> PlayedMoves => _moves.Take(Ply);

    /// <summary>The current position. After a winning move it contains the four, so it must not be searched.</summary>
    public Position Position => _states[Ply].Position;

    public Player ToMove => Position.ToMove;

    public int? LastMove => Ply > 0 ? _moves[Ply - 1] : null;

    public Player? Winner => _states[Ply].Winner;

    /// <summary>The discs of the winning four (or more, if one move made several); 0 while nobody has won.</summary>
    public ulong WinningLine => _states[Ply].WinningLine;

    public bool IsDraw => Winner is null && Position.IsFull;

    public bool IsGameOver => Winner is not null || Position.IsFull;

    public bool CanPlay(int column) => !IsGameOver && Position.CanPlay(column);

    public bool CanUndo => Ply > 0;

    public bool CanRedo => Ply < _moves.Count;

    /// <exception cref="InvalidOperationException">The game is over or the column is full.</exception>
    public void Play(int column)
    {
        if (IsGameOver)
        {
            throw new InvalidOperationException("The game is over.");
        }

        Position position = Position;
        Player mover = position.ToMove;
        bool wins = position.CanPlay(column) && position.IsWinningMove(column);
        Position next = position.Play(column);

        _moves.RemoveRange(Ply, _moves.Count - Ply);
        _states.RemoveRange(Ply + 1, _states.Count - Ply - 1);
        _moves.Add(column);
        _states.Add(wins
            ? new State(next, mover, Position.FindFours(next.Discs(mover)))
            : new State(next, null, 0));
        Ply++;
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("There is no move to undo.");
        }

        Ply--;
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("There is no move to redo.");
        }

        Ply++;
    }

    public void NewGame()
    {
        _moves.Clear();
        _states.RemoveRange(1, _states.Count - 1);
        Ply = 0;
    }

    private readonly record struct State(Position Position, Player? Winner, ulong WinningLine);
}
