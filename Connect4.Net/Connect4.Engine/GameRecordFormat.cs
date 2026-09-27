namespace Connect4.Engine;

/// <summary>Text game record: the 1-based columns played from the empty board, e.g. "4453".</summary>
public static class GameRecordFormat
{
    /// <summary>Writes the moves up to the current position; undone moves are not saved.</summary>
    public static string Format(Game game) => string.Concat(game.PlayedMoves.Select(column => (char)('1' + column)));

    /// <summary>Reads a game record. Whitespace (e.g. a line break at the end of a file) is ignored.</summary>
    /// <exception cref="FormatException">A character is not a column 1–7, a column is full, or the game is already over.</exception>
    public static Game Parse(string text)
    {
        var game = new Game();
        int number = 0;

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            number++;
            int column = c - '1';
            if (column is < 0 or >= Position.Width)
            {
                throw new FormatException($"Move {number}: '{c}' is not a column 1-{Position.Width}.");
            }

            if (game.IsGameOver)
            {
                throw new FormatException($"Move {number}: the game is already over.");
            }

            if (!game.CanPlay(column))
            {
                throw new FormatException($"Move {number}: column {column + 1} is full.");
            }

            game.Play(column);
        }

        return game;
    }
}
