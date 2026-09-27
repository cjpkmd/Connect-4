# 09 – Endgame solver

[Back to the index](README.md)

## In short

Near the end of the game the computer can work out the exact result. `EndgameSolver` ([EndgameSolver.cs](../../Connect4.Net/Connect4.Engine/Endgame/EndgameSolver.cs)) is Pascal Pons' C++ solver, ported 1:1 without its opening book. It searches to the end of the game with null-window alpha-beta and its own hash table. The heuristic search runs first and always gives a move; the solver is then tried with the time that is left.

## Scores

The solver uses the C++ scores, from the side to move:

$$
\text{score} = 22 - n
$$

where $n$ is the winner's own disc number for the winning disc (counting that player's discs from 1). The fastest possible win, with the fourth disc, scores 18; a win with the last disc scores 1; 0 is a draw; a loss is the negative. The test files use the same scores. `Scores.FromSolver` converts them to the search scores of chapter 07: Red's $n$-th disc is move $2n - 1$, Yellow's is move $2n$.

## The algorithm

`Solve(position)`:

1. If the side to move can win at once, return $(43 - \text{Moves}) / 2$.
2. Start with the possible range $[\min, \max]$ and narrow it with **null-window** searches: pick a value `med` in the range (biased towards 0, first `min / 2` or `max / 2`), and ask `Negamax(position, med, med + 1)` whether the score is above `med`. Each answer halves the range, until $\min = \max$.

`Negamax(position, α, β)` is the C++ function:

1. `NonLosingMoves()`; none: lost on the next move.
2. With 40 or more discs: a draw.
3. Tighten α and β with the best and worst score still possible from this move count.
4. Look up the hash table; its value is an upper or a lower bound, which may tighten the window further.
5. Order the moves (chapter 06, without a hash move) and search them.
6. Store a lower bound after a cutoff, otherwise an upper bound.

With `weak: true` the range is [−1, 1]; as in C++, only the sign of the result is then exact.

`Analyze(position)` solves the position after every move (a full column gets `InvalidMove`). The search uses it to find the best move, because `Solve` only gives the score of the position. A strong solve is used, not the weak one, because the computer must play the fastest win.

## The hash table

[EndgameTable.cs](../../Connect4.Net/Connect4.Engine/Endgame/EndgameTable.cs) is the C++ `TranspositionTable`:

- The size is the first prime above $2^{24}$, 16 777 259 entries.
- Each entry is a 32-bit part of the key and one byte for the value, so the table takes about 84 MB.
- The slot is $\text{key} \bmod \text{size}$. Because the size is prime and the stored part is the key modulo $2^{32}$, the pair (slot, stored part) is unique for every 49-bit key (the Chinese remainder theorem): a hit is never wrong.
- The byte holds an upper bound as $\text{score} + 19$ (1–37) or a lower bound as $\text{score} + 56$ (38–74); 0 means empty. The last entry stored in a slot wins.

It is created the first time the solver runs and kept for the whole session: its results never go stale, so later moves in the same game are usually solved at once.

## When it is used

| Time mode | The solver runs when | Its time |
|---|---|---|
| Seconds per move, minutes per game | The empty cells are at most the **endgame threshold** (setting, 0–42, default 24; 0 = never) and the hard time limit is not reached | Until the hard limit |
| Fixed depth | The empty cells are at most the depth setting | No limit; only Move Now stops it |
| `Solve` (tests) | Always | No limit |

The heuristic search runs first. In fixed-depth mode it is then limited to 8 plies, because it only has to give a move in case Move Now stops the solver. The solver is skipped when the heuristic search has already found a final result.

If the solver runs out of time or Move Now is pressed, it throws `OperationCanceledException`, and the move of the heuristic search is played. The time limit is checked with a clock every 1 024 nodes, not with a timer: in the browser the engine runs in a Web Worker, where a timer cannot fire while the search runs.

## Speed

About 12 million positions per second on the desktop. Solving from the beginning of the game without a book can take long: the hardest test set (`Test_L1_R3`) takes about 2.4 seconds per position.

## How it is tested

[EndgameSolverTests.cs](../../Connect4.Net/Connect4.Engine.Tests/EndgameSolverTests.cs) solves Pascal Pons' test positions and compares them with the exact scores:

| Set | Positions | Run |
|---|---|---|
| `Test_L3_R1` (end, easy) | 1 000 | Always |
| `Test_L2_R1` (middle, easy) | 1 000 | Always |
| `Test_L2_R2` (middle, medium) | 1 000 | The first 100 always, all as a slow test |
| `Test_L1_R1`, `Test_L1_R2`, `Test_L1_R3` (beginning) | 1 000 each | Slow tests only |

Slow tests are run on purpose with `dotnet test Connect4.Engine.Tests -c Release -s Connect4.Engine.Tests/slow.runsettings`; `Test_L1_R3` alone takes about 40 minutes. Also tested: weak solves, `Analyze`, cancellation and the time limit, and the hash table.
