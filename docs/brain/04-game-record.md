# 04 – Game record

[Back to the index](README.md)

## In short

`Game` is a game from the empty board with its move history. It knows the winner and the winning line, and moves can be taken back and played again. A game is saved as the columns played, as in connect4-master: `4453` means column 4, 4, 5 and 3.

## Game

[Game.cs](../../Connect4.Net/Connect4.Engine/Game.cs) keeps two lists: the moves (0-based columns) and, for every ply, the position, the winner and the winning line.

| Member | Meaning |
|---|---|
| `Ply` | Moves played up to the current position |
| `Moves`, `PlayedMoves` | All moves (also undone ones), and the moves up to `Ply` |
| `Position`, `ToMove`, `LastMove` | The current position |
| `Winner`, `WinningLine` | The winner and the discs of the four, or null and 0 |
| `IsDraw`, `IsGameOver` | A full board without a winner; a win or a draw |
| `CanPlay(column)` | The game is not over and the column is not full |
| `Play`, `Undo`, `Redo`, `NewGame` | Change the history |

When a move wins, `Play` stores the mover as the winner and uses `Position.FindFours` for the winning line. The position after a winning move contains a four, so it must not be searched; the app never asks for a computer move in a finished game.

`Undo` and `Redo` only move `Ply`; the moves after it are kept until a new move is played. The app decides how far to go back: against the computer back to the human's previous turn, in human against human one move (chapter 12).

## The text format

[GameRecordFormat.cs](../../Connect4.Net/Connect4.Engine/GameRecordFormat.cs):

- `Format(game)` writes the played moves as 1-based digits, e.g. `4453`; undone moves are not saved.
- `Parse(text)` reads them back. Whitespace, such as the line break at the end of a file, is ignored. A character that is not 1–7, a full column or a move after the end of the game gives a `FormatException` with the move number.

The apps save games as `.txt` files with this text. It is the same notation as Pascal Pons' solver and test files.

`Position.FromMoves` reads the same notation but refuses a move that makes four in a row, because a `Position` must not contain a four; it is used for positions to search, `Parse` for games.
