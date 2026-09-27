# Specification: a .NET implementation of Connect 4

## Scope

The project creates a WPF application and a Blazor web app, similar to the Stello solution in `C:\Udvikling\Privat\Stello`, with the same features as Stello except where this specification says otherwise.

The Connect 4 brain is based on the C++ code in `connect4-master`, translated to a modern C# structure.

**Goal of the brain:** connect4-master is used as a fast search core (bitboard, move generation, threat detection, move ordering). It is *not* the goal to solve the game on every move. As in Stello, the search is iterative deepening alpha-beta that stops on a depth or time limit, and a heuristic evaluation function scores the positions at the search horizon. When there is time left near the end of the game, the ported C++ solver finds the exact result (like Stello's endgame solver). The first plies are played from a small opening book of exactly solved positions (section 4.9), made by a generator in `Connect4.Tools`.

This is a private project, so licensing (connect4-master is AGPL-3.0) is not an issue.

## 1. Game rules

- Board: 7 columns × 6 rows only. Two players, **Red** (moves first) and **Yellow**.
- A move drops a disc into a non-full column. It lands in the lowest empty cell.
- Four in a row horizontally, vertically or diagonally wins. A full board with no four in a row is a draw.
- There are no passes (unlike Othello).
- Column notation: `1`–`7`, left to right, as in connect4-master. A game record is the column sequence, e.g. `4453`.

## 2. Reference sources

| Source | Used for |
|--------|----------|
| `C:\Udvikling\Privat\Stello\Stello.Net` | Solution layout, MVVM architecture, services, UI features, tests, Blazor setup, CI. **Search framework:** iterative deepening, time control (`SearchLimits`, `TimeControl`), cancellation / Move Now, progress reporting (`SearchInfo`), `ComputerPlayer` |
| `C:\Udvikling\Privat\Stello\docs\brain` and `Stello porting documentation.md` | Structure of the documentation to write |
| `connect4-master` (Pascal Pons' solver, C++) | **Search core:** bitboard, move generation, threat detection, non-losing moves, move ordering. The exact solver (with its own transposition table) for the end of the game |
| `Test positions/` | Pascal Pons' test sets with exact scores (see section 7) |
| GitHub repository | https://github.com/cjpkmd/Connect-4, branch `main` (already connected) |

## 3. Solution structure

Mirror Stello's layout. The solution is `Connect4.Net/Connect4.Net.slnx` with the (empty) WPF project `Connect4.WPF`. Its `RootNamespace`, `App.xaml`, `MainWindow.xaml` and the `.cs` files still use the old namespace `Connect_4.WPF` and are changed to `Connect4.WPF` in phase 1.

| Project | Type | TFM | Stello equivalent |
|---------|------|-----|-------------------|
| `Connect4.Engine` | Class library, no UI dependencies | net10.0 | `Stello.Engine` |
| `Connect4.App` | Class library: view models, models, service interfaces (CommunityToolkit.Mvvm) | net10.0 | `Stello.App` |
| `Connect4.WPF` | WPF app (WinExe), exists | net10.0-windows | `Stello.Net` |
| `Connect4.Web` | Blazor WebAssembly, engine in a Web Worker, AOT in Release | net10.0 | `Stello.Web` |
| `Connect4.Engine.Tests` | xUnit | net10.0 | `Stello.Engine.Tests` |
| `Connect4.App.Tests` | xUnit, view model tests with fake services | net10.0-windows | `Stello.Net.Tests` |

Added in phase 11: `Connect4.Tools` console app (net10.0), first with the opening book generator (4.9). Later: benchmarks, engine-vs-engine matches and comparison with the C++ program. The book logic that can be tested (enumeration, back-up, file format) lives in `Connect4.Engine/Book`; the tool is a thin command line around it (arguments, worker threads, files).

References: `WPF` → `App`, `Engine`; `Web` → `App`, `Engine`; `App` → `Engine`; `Tools` → `Engine`.

Common settings as in Stello: nullable enabled, implicit usings, no static global state in the engine.

## 4. Engine

The engine combines two sources:

```mermaid
flowchart LR
    subgraph C["From connect4-master (C++ port)"]
        P[Position / bitboard]
        M[Move generation, non-losing moves, threats]
        O[Move ordering: MoveSorter, centre first, threat count]
        E[EndgameSolver: exact C++ solver]
    end
    subgraph S["From Stello (C# design)"]
        ID[Iterative deepening + time control]
        TT[Transposition table with depth and best move]
        CP[ComputerPlayer, SearchLimits, SearchInfo]
    end
    N["New: evaluation function"]
    ID --> M --> O
    ID --> TT
    ID --> N
    ID --> E
    CP --> ID
```

### 4.1 What is ported from connect4-master

| C++ | C# | Notes |
|-----|----|-------|
| `Position` (`Position.hpp`) | `Position` readonly record struct (two `ulong`: `current`, `mask`, plus `moves`) | Bit layout kept: column `c` uses bits `c*7 … c*7+5`, bit `c*7+6` is a sentinel. 49 bits fit in `ulong`. Only 7×6. |
| `play`, `canPlay`, `possible`, `isWinningMove`, `canWinNext` | Same names in C# style | 1:1 port. |
| `compute_winning_position`, `winning_position`, `opponent_winning_position` | `WinningCells(player)` | All empty cells that would complete a four. Used by move ordering, pruning **and** the evaluation. |
| `possibleNonLosingMoves` | `NonLosingMoves` | Removes moves that let the opponent win at once. If the opponent has two immediate threats, the position is lost. |
| `moveScore`, `MoveSorter`, `columnOrder` (3,2,4,1,5,0,6) | `MoveOrdering` | TT best move first (from Stello), then C++ order. |
| `popcount` loop | `BitOperations.PopCount` | |
| `key()` | `Key` | Transposition table key. |
| `key3()` (base-3 key, the same for a position and its mirror) | `Mirror()`, `CanonicalKey` | Not ported 1:1. `Mirror()` swaps the 7-bit column groups of `Current` and `Mask`; `CanonicalKey = min(Key, Mirror().Key)`. Used by the opening book. |
| `Solver::solve` / `negamax` | `EndgameSolver` | 1:1 port of the exact solver (null-window loop, alpha-beta, pruning). Added: deadline and `CancellationToken`, so an unfinished solve can be dropped. |
| `TranspositionTable` (8-bit bound value, prime size) | `EndgameTable` | 1:1 port, used only by `EndgameSolver`. Same size as C++: 2^24 entries (≈ 84 MB), on desktop and in the browser. Allocated the first time the solver runs. |
| – | `TranspositionTable` (Stello design) | For the heuristic search, which also needs depth, bound type and best move. |
| `OpeningBook`, `7x6.book`, `generator.cpp` | `Book/OpeningBook`, `Book/BookBuilder`, `Connect4.Tools book` | Not ported 1:1. The pipeline of `generator.cpp` is kept (list the unique positions, solve them, store the scores), but the book is full-width to a small depth with backed-up scores, is a text file, and is used by `SearchEngine`, not by the solver (4.9). `7x6.book` is not used. |
| `main.cpp` | Not ported now | Later in `Connect4.Tools`. |

### 4.2 Search (Stello design)

- `SearchEngine`: negamax alpha-beta with iterative deepening (depth 1, 2, 3, …) until the limit is reached, as principal variation search (null window on non-first moves).
- At each node, in this order:
  1. Draw if the board is full.
  2. If the side to move can win at once → win score (no further search).
  3. `NonLosingMoves`; if empty → loss score.
  4. If only one non-losing move (forced block), search it without reducing the depth (threat extension).
  5. At depth 0 → evaluation function.
  6. Transposition table lookup, then ordered moves.
- `SearchLimits` as Stello: `FixedDepth(plies)`, `TimePerMove(span)`, `TimePerGame(remaining)`, `Solve` (no limit, `EndgameSolver` only; used by tests).
- `TimeControl`: soft limit (do not start a new iteration) and hard limit (abort the running iteration, use the result from the last completed iteration). Move Now = cancel.
- `SearchInfo` progress per iteration: depth, score, best move, principal variation, nodes, time, and "solving" while the endgame solver runs.
- `SearchResult`: move, score, `ScoreKind` (`Heuristic`, `Exact`), depth, nodes.
- The engine is not thread-safe; one search at a time (as Stello).

### 4.3 Endgame solver

1. The heuristic iterative deepening runs first, as normal, and always gives a move.
2. When the solver runs:
   - **Seconds per move / minutes per game:** if the number of empty cells is at most the **endgame threshold** and time is left before the hard limit. The solver gets the remaining time as deadline.
   - **Fixed depth:** if the number of empty cells is at most the depth setting. No time cap (only Move Now stops it). The heuristic search before it is limited to 8 plies, only to have a move if Move Now stops the solver.
3. If it finishes, its exact result is used: the best move and an exact score. If it runs out of time or Move Now is pressed, the heuristic result from step 1 is used.
4. Once a position is solved, later moves in the same game are usually solved at once (the solver's table is kept between moves).

The endgame threshold is a setting in the Settings dialog, used only in the two time modes: 0–42 empty cells, default 24, 0 = never.

The C++ solver returns one score for the position, not a move. The best move is found as in the C++ `analyze`: solve the position after each non-losing move, best score wins (ties: see 4.7). A strong solve is used (not the weak win/draw/loss solve), because the computer must play the fastest win.

### 4.4 Scores

- Inside the engine, scores are from the side to move (negamax).
- Heuristic score range, e.g. −1000…1000.
- Win/loss scores outside that range: `Win (10000) − number of discs on the board after the winning move`, so a faster win scores higher (Stello uses ±32600 in the same way). Because the move number is absolute, the score of a position does not depend on the search root and can be stored in the hash table as it is.
- A decided score is only final (no deeper iteration needed) once the search depth reaches the winning move; before that a faster win could still exist behind a forced-move extension.
- A win/loss found by the search is exact (`ScoreKind.Exact`) even at a limited depth.
- `EndgameSolver` uses the C++ convention (−18…18, 22 − number of the winner's own disc). It is converted to the engine's win scores. The test files use the C++ convention.
- UI: the heuristic score is shown from **Red's view** (positive = good for Red); exact results as "Red wins in N moves" / "Yellow wins in N moves" / "Draw".

### 4.5 Evaluation function (new code)

connect4-master has no evaluation function (it always searches to the end). Terms, all computed with bitboards:

| Term | Idea | Default weight |
|------|------|----------------|
| Threats | Number of empty cells that complete a four for each player (`WinningCells`). | 30 per threat |
| Odd/even threats (zugzwang) | Red (first player) profits from threats on odd rows (1, 3, 5 counted from the bottom), Yellow from even rows. A cell that is a threat for both players only gives this bonus to the player whose good row it is on. | +20 per threat on a good row |
| Stacked threats | Two threats of the same player on top of each other in one column usually win. | 40 per pair |
| Open lines | Lines of 4 cells with only one colour: count lines with 2 and 3 discs. | 4 / 6 per line |
| Centre | Cell weights by the number of lines of four through each cell (3 in the corners, 13 in the centre). | 1 × cell weight per disc |

The score is clamped to ±1000. No tempo term.

The evaluation is symmetric: the mirrored position gives the same score, and swapping colours negates it except for the odd/even term (which depends on who moved first).

Weights are tuned by hand (`EvaluationWeights`). Engine-vs-engine tuning can come later with `Connect4.Tools`.

### 4.6 Transposition tables

- Heuristic search: Stello design (key check, depth, bound exact / lower / upper, value, best move; replaced on depth or age), but packed into one `ulong` per entry:

  | Field | Bits |
  |-------|------|
  | Key check (the part of the 49-bit `Key` not given by the slot index) | 25 |
  | Value | 16 |
  | Depth | 6 |
  | Bound | 2 |
  | Best move (column 0–6, 7 = none) | 3 |
  | Spare | 12 |

  The slot is found with a bijective hash of the 49-bit `Key` (multiply by an odd constant modulo 2^49): the top 24 bits give the slot, the other 25 bits are stored as key check. Slot + key check identify the position exactly, so there are no false hits.

- Sizes, desktop and browser:

  | Table | Entries | Entry size | Memory |
  |-------|---------|------------|--------|
  | Heuristic search `TranspositionTable` | 2^24 | 8 bytes | 128 MiB (≈ 134 MB) |
  | `EndgameTable` | next prime > 2^24 | 4 + 1 bytes | ≈ 84 MB |
  | Total | | | ≈ 218 MB |

  Without packing (Stello's record struct with a `ulong` key is 16 bytes with padding), the search table would take 256 MiB.
- Both sizes are constructor parameters (search table 2^16–2^26, endgame table 2^17–2^27). Tests use small tables, because clearing a large table for every test position costs more than the search.
- If a table cannot be allocated (e.g. in a browser on a phone), `SearchEngine` falls back to 2^20 entries for that table and sets `UsesFallbackTables`.
- The heuristic table is cleared at New Game; both tables are kept between moves in the same game.

### 4.7 Computer strength and variation

- Same settings as Stello: fixed depth (1–20 plies), seconds per move (1–60), minutes per game (1–60). Plus the endgame threshold (4.3). No easy levels for now.
- Variation: among root moves with *exactly* the same best score, one is picked at random. The random generator can be seeded, so tests are repeatable.
- To know which moves are equal, the root must search the other moves with a window of "best − 1" instead of a null window above the best score. This costs a little time at the root only.
- Exact results: a win is always the fastest win (a faster win has a higher score). Only wins of the same length are picked at random. In the same way a lost position plays the slowest loss.
- Book moves follow the same rule (4.9).

### 4.8 Speed

- No allocations in the search: `Position` is a struct, move lists on the stack.
- Node rate is compared with the C++ code later, when `Connect4.Tools` is added.

### 4.9 Opening book

Goal: the computer plays the first plies perfectly and at once, so the weak spots of the evaluation in the opening (e.g. it opens in column 2 at 1 s per move) do not matter. The book is small: exact scores for every position of the first 6 plies, not a large database.

**Ideas found**

- connect4-master `generator.cpp` + `OpeningBook.hpp`: `generator <depth>` prints every position up to the depth once (mirror positions once, via `key3`; games that have ended are skipped). The positions are solved outside the program, and the scored lines `moves score` are read back into a hash table (2^23 entries, partial keys, positions up to 14 plies) saved as `7x6.book`. `Solver::solve` asks the book first.
- John Tromp's 8-ply database (UCI Machine Learning Repository, "Connect-4" data set, https://archive.ics.uci.edu/dataset/26/connect+4): all 67,557 positions after 8 plies where nobody has won and the next move is not forced, with win / draw / loss for the first player. An independent check for a book of depth 8 or more (sign only).
- Known values of the first move (Pascal Pons' online solver; confirmed by the measurements below): columns 1–7 score −2, −1, 0, +1, 0, −1, −2 for Red. Only the centre wins, with Red's last disc.

**Design (differs from the C++ book)**

- **Full-width to depth N**: every position with at most N discs, mirror positions stored once.
- **Only the leaves are solved** (the positions with exactly N discs). The scores of shallower positions are *backed up* by negamax from their children: a winning move gives `(43 − moves) / 2`, otherwise the score is the maximum of −(child score) over the playable columns. This avoids the costly solves of shallow positions: one position after 1 ply takes 40–143 s, after 2 plies about 45 s (measured). A leaf after 6 plies takes 2 s on average.
- **Strong scores** (C++ convention −18…18), so the computer plays the fastest win or slowest loss and the analysis panel shows "Red wins in N moves" from the first move. A weak solve is not cheaper at the beginning: after 1 ply (`4`), strong took 143 s and weak 153 s.
- **Book move**: in a position with fewer than N discs, look up all children and play the best (ties at random with the seeded generator, as 4.7). Mirror moves always tie, so e.g. after `44` the replies in columns 3 and 5 are picked at random. A position with exactly N discs has a score in the book but no move; the normal search plays there.
- "Best variations" therefore means: the computer's own book moves are always best, and every reply of the opponent is covered, including bad ones. A book that only stores the lines of best play would still need exact scores for all alternatives at the computer's turns, which are the costly shallow solves, so it would not be cheaper.

**Measurements on this computer** (i7-12850HX, 16 cores / 24 threads, 32 GB; C# `EndgameSolver` in Release, 2^24 table cleared before every position, strong solve). Leaves: about 100 positions sampled evenly from the real leaf list of each depth.

| Depth N | Positions ≤ N (mirror reduced) | Leaves (N discs) | Mean per leaf | Median | 90 % | Max | CPU time (leaves) | Wall time, 12 workers | Book, binary (8 bytes/entry) |
|---------|-------------------------------:|-----------------:|--------------:|-------:|-----:|----:|------------------:|----------------------:|-----------------------------:|
| 5 | 2,863 | 2,144 | 4.4 s ¹ | 4.4 s | – | 10.8 s | ≈ 2.6 h | ≈ 20 min | 23 KB |
| **6** | **11,094** | **8,231** | **1.94 s** | 0.98 s | 5.0 s | 20.7 s | **≈ 4.4 h** | **≈ 35 min** | **89 KB** |
| 7 | 38,203 | 27,109 | 0.94 s | 0.63 s | 2.2 s | 7.8 s | ≈ 7.1 h | ≈ 55 min | 306 KB |
| 8 | 129,498 | 91,295 | 0.53 s | 0.26 s | 1.0 s | 11.4 s | ≈ 13.6 h | ≈ 1.7 h | 1.0 MB |
| 9 | 399,029 | 269,531 | not measured | | | | | | 3.2 MB |

¹ Depth 5: 12 positions from random games, not an even sample of the leaves (at depth 6 the two methods gave 2.3 s and 1.9 s).

- Unique positions per ply (mirror reduced): 1, 4, 25, 121, 568, 2,144, 8,231, 27,109, 91,295, 269,531 (plies 0–9).
- Parallel speed-up, measured: 12 solver processes at once each ran 1.5× slower than one alone, so 12 workers give about 8×. With 12 tables of 2^24 entries this uses about 1 GB.
- The time per position varies by a factor of 100, so the estimates are ±50 %. Keeping each worker's table between leaves (leaves sorted by move string, so neighbours share subtrees) should make it faster; this was not measured.

**Recommendation: depth 6.** The first 6 plies (3 moves for each side) come from the book: 11,094 positions, 89 KB binary or about 110 KB as text, about 4.4 CPU hours, i.e. about 35–45 minutes with 12 workers. That is far below the one-day limit. Depth 8 also fits (13.6 CPU hours, under 2 hours with 12 workers, and even on one core within a day), but the book is 12× larger; the depth is a parameter of the tool, so it can be raised later. Depth 9 and more is larger than wanted.

Side result: positions after 8 plies are solved in 0.5 s on average (max 11 s), so the endgame threshold default of 24 empty cells (4.3) is very cautious. It can be tuned later; that is not part of the book work.

**File format**

- Text, as the `Test positions/` files: one line `<moves> <score>` per position (score from the side to move, C++ convention), sorted by ply and then by moves. Lines starting with `#` are comments; the first line records depth, score type, date and generator version.
- Stored as `Connect4.Engine/Book/OpeningBook.txt`, an embedded resource of `Connect4.Engine`, so the desktop app and the web worker get it without an extra download. Text is readable, gives useful git diffs, and single lines can be checked on Pascal Pons' online solver.
- Loaded once (lazily) into a sorted `ulong[]` of `CanonicalKey << 8 | (score + 64)`, looked up with binary search (89 KB in memory, a few milliseconds to load).

**Generator: `Connect4.Tools book`**

- `book generate --depth 6 [--workers N] [--table 24] --out OpeningBook.txt`
  1. List the unique positions up to the depth (breadth first, mirror reduced, as `generator.cpp explore`).
  2. Solve the leaves in parallel: one `EndgameSolver` per worker (its own table, kept between leaves), leaves taken from a shared queue in move-string order. Default workers: `Environment.ProcessorCount / 2` (12 here).
  3. Append each result at once to `OpeningBook.txt.partial`, so a stopped run resumes and skips the solved leaves.
  4. Back up the shallower scores, write the book, and print statistics: counts, time, and the scores of the empty board and its 7 children.
  - Progress while running: solved / total, positions per second, estimated time left. Ctrl+C stops cleanly (the partial file is kept).
- `book verify --book OpeningBook.txt [--sample 200] [--min-ply 3] [--seed S] [--workers N] [--table 24]`: solves random entries with at least `--min-ply` discs directly and compares them (shallower ones are too slow). Checks that every stored shallower score equals the back-up of its children, and that the empty board scores 1 with first moves −2, −1, 0, +1, 0, −1, −2. Exit code 0 only if everything is correct.
- The enumeration and the back-up take the solve step as a delegate, so the tests can use a fake solver and small depths.

**Engine and app integration**

- `Connect4.Engine/Book/OpeningBook`: `Load(Stream)`, `Default` (the embedded book), `Depth`, `TryGetScore(Position, out int score)`, `TryGetMove(Position, Random, out int column, out int score)`.
- `SearchEngine.Search`: if the book is on and `position.Moves < book.Depth`, it returns the book move at once: `ScoreKind.Exact`, the score converted with `Scores.FromSolver`, depth 0, 0 nodes, and a new flag `FromBook` in `SearchResult` (no progress report is sent). The analysis panel shows "book" and the exact result. The book is used in all three time modes; no time is spent. `SearchEngine` takes the book as an optional constructor parameter (default `OpeningBook.Default`).
- `SearchLimits.UseBook` (default true) and `GameSettings.UseOpeningBook` (default true), with an "Use opening book" check box in the Settings dialog (WPF and web). Search tests that need the real search use an empty book (or `UseBook = false`).
- The solver does not use the book (it only runs near the end of the game), so the slow `Test_L1_*` tests are not faster.

## 5. Application features

Stello's features and whether they apply to Connect 4:

| Stello feature | Connect 4 | Notes |
|----------------|-----------|-------|
| File: New (Ctrl+N), Open (Ctrl+O), Save (Ctrl+S), Save As, Exit | Yes | `.txt` file with the column sequence, e.g. `4453`. |
| Switch Sides | Yes | Human vs computer: swaps the colours of human and computer. Disabled in human vs human. |
| Move Now (Ctrl+M) | Yes | Stops the search (or the endgame solve) and plays the best move found. |
| Undo (Ctrl+Z) / Redo (Ctrl+Y) | Yes | Human vs computer: takes back the computer's reply and the human move, as in Stello. Human vs human: one move. |
| Settings dialog | Yes | Same as Stello: fixed depth / seconds per move / minutes per game. Plus the endgame threshold and "Use opening book". |
| Analysis panel (Ctrl+A) | Yes | As Stello: depth, score (Red's view), best move, principal variation, nodes, time, "solving" or "book" state. No score per column. Empty in human vs human (no search runs). |
| Add Game to Book, Evaluate Book, Self-play, Stop Learning | No | No learning book. The opening book (4.9) is fixed and made by `Connect4.Tools`. The Book menu is removed. |
| About box | Yes | Same style and picture as Stello; text adapted to Connect 4 (below). |
| Window placement and settings saved (JSON in AppData / browser local storage) | Yes | |
| Appearance / themes (web only) | Yes | Copy Stello.Web's `AppearanceStore` and theme settings to Connect4.Web. The WPF app has no themes, as Stello. |

New features (not in Stello):

| Feature | Description |
|---------|-------------|
| Game mode | Game menu: **Human vs Computer** (default, human is Red) or **Human vs Human**. Saved in the settings. Can be changed during a game: to Human vs Computer, the human keeps the side to move and the computer takes the other colour; to Human vs Human, a running search is cancelled. |
| Disc-drop animation | The disc falls into place (short, with acceleration). Input is ignored while the animation runs. |
| Winning-line highlight | The four winning discs are marked when the game ends. |
| Sound | Simple generated sounds: disc drop, win, loss, draw. On by default. Mute option in the View menu, saved in the settings. |
| Language | English only. |

Not included: computer vs computer, hint.

Board UI:

- Click anywhere in a column to drop a disc. Hover shows the disc above the column. Full columns and moves while the computer thinks are ignored (beep, as in Stello).
- Highlight the last move and the four winning discs.
- Status line: whose turn, result, notices.

About box (same layout, style and picture `2026 Claus Pedersen.jpg` as Stello):

- Title: "About Connect 4". Version: "Connect 4 Version 1.0".
- Caption: "Claus Pedersen – It-architect with a passion for AI and computer games. Connect 4 was written using Opus 5.5 in 2026".
- "Connect 4 is a game for two players on a board with 7 columns and 6 rows. You play against the computer or against a friend, and you can see the computer think in the analysis panel."
- "Its brain plays the first moves from an opening book of solved positions. After that it searches ahead with alpha-beta search, a hash table and iterative deepening, and judges positions by their threats and open lines. Near the end of the game it works out the exact result and plays perfectly."
- "The search core is a C# port of Pascal Pons' C++ Connect 4 solver. The same brain runs as a Windows program (WPF) and in the browser (Blazor WebAssembly)."

## 6. Blazor web app

As Stello.Web:

- Blazor WebAssembly standalone, engine runs in a Web Worker via `[JSExport]`, AOT compilation in Release (`wasm-tools` workload).
- Pages: `/` game, `/docs` and `/docs/{slug}` brain documentation (Markdown → HTML with Markdig), NotFound.
- Settings and appearance in local storage. The opening book is embedded in the engine assembly (about 110 KB text), so the worker has it without an extra download.
- Same hash table sizes as desktop (2^24 entries each, ≈ 218 MB in the Web Worker; fallback 2^20, see 4.6).
- A running search cannot be interrupted inside the worker, so Stop and Move Now terminate the worker (Move Now plays the best move reported so far) and start a new one with empty tables. Time limits work inside the worker because they are checked with a clock, not a timer.
- Appearance (View menu): Blue, Golden Oak or Reddish Wood board (Stello's wood textures), 3D discs, animated drops.
- Sound with HTML audio; the WAV files are generated by `SoundWaves` (shared with the desktop app), so there are no sound files.
- Deployment: Azure Static Web Apps with a GitHub Actions workflow like Stello's, from the `main` branch of https://github.com/cjpkmd/Connect-4.
  - Manual steps for the owner (they need an Azure login): create the Static Web App in the Azure portal and add its deployment token to the GitHub repository as the secret `AZURE_STATIC_WEB_APPS_API_TOKEN`. The workflow file is part of the project.

## 7. Tests (xUnit, as Stello)

Engine:

- Position: play, can play, winning move detection in all 4 directions, full columns, draw, `WinningCells`, `NonLosingMoves`.
- Perft from the empty board, depths 1–8 (depths 1–6 must equal 7^n).
- Evaluation: mirror symmetry, colour swap negates the score, simple hand-made positions (more threats = better).
- Search: finds a win in 1 and blocks a threat at depth 1; finds a known win in N at depth ≥ N; plays the fastest win; fixed depth is deterministic with a fixed random seed; time limits, endgame threshold and cancellation are respected.
- Endgame solver: exact scores for Pascal Pons' test sets in `Test positions/` (see below).
- Transposition tables: store/lookup, bounds, replacement.
- Strength: plays against random and greedy players and wins (as Stello's `StrengthTests`).
- Opening book: `Mirror` and `CanonicalKey`; enumeration counts per ply (1, 4, 25, 121, 568, 2,144, 8,231); back-up with a fake solver; file load/save round trip; the real book: empty board 1 and first moves −2, −1, 0, +1, 0, −1, −2, a sample of leaves re-solved with `EndgameSolver`, `Test_L1_*` lines with at most N moves equal the book, back-up consistency; book move is the best, ties at random with a fixed seed; `UseBook = false` and positions at or beyond the depth use the search.

Test data (`Test positions/`, 6 files × 1000 lines, `<moves> <score>` in the C++ score convention):

| File | Position | Difficulty | Use |
|------|----------|------------|-----|
| `Test_L3_R1` | End (> 28 moves played) | Easy | All lines in normal test runs |
| `Test_L2_R1` | Middle (15–28 moves) | Easy | All lines in normal test runs |
| `Test_L2_R2` | Middle | Medium | First lines in normal runs, all as slow test |
| `Test_L1_R1`, `Test_L1_R2`, `Test_L1_R3` | Beginning (≤ 14 moves) | Easy – hard | Slow tests only; without a book they can take a long time |

The test project links the files from `Test positions/` (as Stello links its OPENING file), so they stay in one place. Slow tests get `[Trait("Category", "Slow")]` and are excluded from normal runs by `default.runsettings`; they are run on purpose with `dotnet test Connect4.Engine.Tests -c Release -s Connect4.Engine.Tests/slow.runsettings` (`Test_L1_R3` alone takes about 40 minutes).

App:

- `MainViewModel` with fake services: new game, play, computer reply, undo/redo, switch sides (disabled in human vs human), game mode (also changed during a game), open/save, settings persistence.

## 8. Documentation

As Stello's `docs/brain`, chapters in Markdown, also shown in the web app:

1. Overview
2. Board and bitboard
3. Rules, move generation, threats and non-losing moves
4. Game record
5. Evaluation
6. Move ordering
7. Search (alpha-beta, iterative deepening, threat extension)
8. Transposition table
9. Endgame solver
10. Time control
11. App integration
12. Glossary
13. References (Pascal Pons' blog: http://blog.gamesolver.org)
14. Opening book (added last, so the numbers of the existing chapters stay)

Plus `Connect 4 porting documentation.md` like Stello's: per part, the C++ original, the C# code, and whether it is a 1:1 port or changed.

## 9. Phases

1. Fix the namespace, create the solution structure.
2. Engine: `Position`, rules, threats, perft tests.
3. Engine: endgame solver (1:1 port) and tests with `Test positions/`. This also checks the port of `Position`.
4. Engine: evaluation function and tests.
5. Engine: search, transposition table, move ordering, time control, variation, endgame integration, tests.
6. `ComputerPlayer`.
7. `Connect4.App`: view models and services.
8. WPF UI, including animation, sound and game mode.
9. Blazor web app, Web Worker, appearance/themes, docs pages.
10. Documentation, GitHub Actions workflow, Azure Static Web App.
11. Opening book generator: `Mirror`/`CanonicalKey`, `Connect4.Engine/Book` (enumeration, back-up, file format, `OpeningBook`), `Connect4.Tools` with `book generate` (parallel, resumable) and `book verify`, tests with small depths and a fake solver.
12. Generate the depth-6 book on this computer (about 35–45 minutes) and verify it. Engine integration (`SearchEngine`, `FromBook`, `UseBook`), the setting in WPF and web, analysis "Book", tests, docs chapter 14, porting documentation, About text.
