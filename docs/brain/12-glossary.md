# 12 – Glossary

[Back to the index](README.md)

| Term | Meaning |
|---|---|
| **Alpha-beta** | A minimax search that skips moves that cannot change the result, using a window (α, β) (chapter 07). |
| **Bitboard** | A 64-bit number with one bit per cell (chapter 02). |
| **Bound** | What a hash table entry says about the score: exact, at least (lower) or at most (upper) the value (chapter 08). |
| **Column order** | The order 3, 2, 4, 1, 5, 0, 6 (0-based) in which equal moves are searched (chapter 06). |
| **Cutoff** | Stopping the search of a node because a move reached β. |
| **Decided score** | A score beyond ±1 000: a proven win or loss. |
| **Endgame solver** | The exact C++ solver, used near the end of the game (chapter 09). |
| **Endgame threshold** | The setting that says from how many empty cells the solver is tried in the time modes. |
| **Evaluation** | The heuristic score of a position at the search horizon (chapter 05). |
| **Fail-soft** | A search that returns the best score it found even when it is outside the window. |
| **Final** | A search result that a deeper search cannot change (chapter 07). |
| **Forced move** | The only move that does not lose at once, usually a block; searched without using up depth. |
| **Hard limit** | The time at which the search stops at once (chapter 10). |
| **Iterative deepening** | Searching depth 1, 2, 3, … until a limit is reached. |
| **Key** | The unique 49-bit number of a position, `Current + Mask` (chapter 02). |
| **Move Now** | Stop the computer and play the best move found so far. |
| **Negamax** | Minimax written so that every score is from the side to move: a position's score is the maximum of the negated scores after its moves. |
| **Node** | A position visited by the search. |
| **Non-losing move** | A move after which the opponent cannot win at once (chapter 03). |
| **Null window** | A window (α, α + 1) that only answers "better than α or not?". |
| **Parity (odd/even rows)** | Red's threats on rows 1, 3, 5 and Yellow's on rows 2, 4, 6 are the dangerous ones late in the game (chapter 05). |
| **Perft** | Counting the move sequences of a given length, to test move generation. |
| **Ply** | One move by one side. |
| **Principal variation (PV)** | The expected line of play, read from the hash table; shown as "Line". |
| **PVS** | Principal variation search: the first move with the full window, the others with a null window first. |
| **Soft limit** | The time after which no new depth is started (chapter 10). |
| **Stacked threats** | Two threats of the same player on top of each other in one column. |
| **Threat** | An empty cell where a player's disc would complete a four. |
| **Transposition** | The same position reached by different move orders. |
| **Transposition table** | The hash table that remembers positions already searched (chapter 08). |
| **Weak solve** | A solve that only finds win, draw or loss (only the sign of the score is exact). |
| **Web Worker** | A background thread in the browser; the web version runs the engine in one. |
| **Zugzwang** | Having to move when every move makes the position worse; in Connect 4 it decides which threats can be used. |
