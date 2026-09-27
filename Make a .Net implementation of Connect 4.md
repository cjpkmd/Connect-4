# Specification: a .NET implementation of Connect 4

## Scope

The project creates a WPF application and a Blazor web app, similar to the Stello solution in `C:\Udvikling\Privat\Stello`, with the same features as Stello except where this specification says otherwise.

The Connect 4 brain is based on the C++ code in `connect4-master`, translated to a modern C# structure.

**Goal of the brain:** connect4-master is used as a fast search core (bitboard, move generation, threat detection, move ordering). It is *not* the goal to solve the game on every move. As in Stello, the search is iterative deepening alpha-beta that stops on a depth or time limit, and a heuristic evaluation function scores the positions at the search horizon. When there is time left near the end of the game, the ported C++ solver finds the exact result (like Stello's endgame solver). There is no opening book.

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

Later (not at the start): `Connect4.Tools` console app for benchmarks, engine-vs-engine matches and comparison with the C++ program.

References: `WPF` → `App`, `Engine`; `Web` → `App`, `Engine`; `App` → `Engine`.

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
| `key()` | `Key` | Transposition table key. `key3()` (mirror key for the book) is not needed without a book. |
| `Solver::solve` / `negamax` | `EndgameSolver` | 1:1 port of the exact solver (null-window loop, alpha-beta, pruning). Added: deadline and `CancellationToken`, so an unfinished solve can be dropped. |
| `TranspositionTable` (8-bit bound value, prime size) | `EndgameTable` | 1:1 port, used only by `EndgameSolver`. Same size as C++: 2^24 entries (≈ 84 MB), on desktop and in the browser. Allocated the first time the solver runs. |
| – | `TranspositionTable` (Stello design) | For the heuristic search, which also needs depth, bound type and best move. |
| `OpeningBook`, `7x6.book`, `generator.cpp` | Not ported | No opening book. |
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
   - **Fixed depth:** if the number of empty cells is at most the depth setting. No time cap (only Move Now stops it).
3. If it finishes, its exact result is used: the best move and an exact score. If it runs out of time or Move Now is pressed, the heuristic result from step 1 is used.
4. Once a position is solved, later moves in the same game are usually solved at once (the solver's table is kept between moves).

The endgame threshold is a setting in the Settings dialog, used only in the two time modes: 0–42 empty cells, default 24, 0 = never.

The C++ solver returns one score for the position, not a move. The best move is found as in the C++ `analyze`: solve the position after each non-losing move, best score wins (ties: see 4.7). A strong solve is used (not the weak win/draw/loss solve), because the computer must play the fastest win.

### 4.4 Scores

- Inside the engine, scores are from the side to move (negamax).
- Heuristic score range, e.g. −1000…1000.
- Win/loss scores outside that range: `Win − ply`, so a faster win scores higher (Stello uses ±32600 in the same way).
- A win/loss found by the search is exact (`ScoreKind.Exact`) even at a limited depth.
- `EndgameSolver` uses the C++ convention (−18…18, 22 − number of the winner's own disc). It is converted to the engine's win scores. The test files use the C++ convention.
- UI: the heuristic score is shown from **Red's view** (positive = good for Red); exact results as "Red wins in N moves" / "Yellow wins in N moves" / "Draw".

### 4.5 Evaluation function (new code)

connect4-master has no evaluation function (it always searches to the end). Terms, all computed with bitboards:

| Term | Idea |
|------|------|
| Threats | Number of empty cells that complete a four for each player (`WinningCells`). The strongest term. |
| Odd/even threats (zugzwang) | Red (first player) profits from threats on odd rows (1, 3, 5 counted from the bottom), Yellow from even rows. Near the end of the game this decides most games. |
| Stacked / shared threats | Two threats on top of each other in one column usually win. A cell that is a threat for both players. |
| Open lines | Lines of 4 cells with only one colour: count lines with 2 and 3 discs, weighted. |
| Centre | Cell weights by the number of possible lines through each cell (centre column highest). |
| Tempo | Small bonus for the side to move (optional). |

The evaluation must be symmetric: the mirrored position gives the same score, and swapping colours negates it.

Weights are tuned by hand. Engine-vs-engine tuning can come later with `Connect4.Tools`.

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
- Both sizes are constructor parameters. Tests use small tables (e.g. 2^16), because clearing a large table for every test position costs more than the search.
- Browser: if the allocation fails (e.g. on a phone), the Web Worker falls back to 2^20 entries per table.
- The heuristic table is cleared at New Game; both tables are kept between moves in the same game.

### 4.7 Computer strength and variation

- Same settings as Stello: fixed depth (1–20 plies), seconds per move (1–60), minutes per game (1–60). Plus the endgame threshold (4.3). No easy levels for now.
- Variation: among root moves with *exactly* the same best score, one is picked at random. The random generator can be seeded, so tests are repeatable.
- To know which moves are equal, the root must search the other moves with a window of "best − 1" instead of a null window above the best score. This costs a little time at the root only.
- Exact results: a win is always the fastest win (a faster win has a higher score). Only wins of the same length are picked at random. In the same way a lost position plays the slowest loss.

### 4.8 Speed

- No allocations in the search: `Position` is a struct, move lists on the stack.
- Node rate is compared with the C++ code later, when `Connect4.Tools` is added.

## 5. Application features

Stello's features and whether they apply to Connect 4:

| Stello feature | Connect 4 | Notes |
|----------------|-----------|-------|
| File: New (Ctrl+N), Open (Ctrl+O), Save (Ctrl+S), Save As, Exit | Yes | `.txt` file with the column sequence, e.g. `4453`. |
| Switch Sides | Yes | Human vs computer: swaps the colours of human and computer. Disabled in human vs human. |
| Move Now (Ctrl+M) | Yes | Stops the search (or the endgame solve) and plays the best move found. |
| Undo (Ctrl+Z) / Redo (Ctrl+Y) | Yes | Human vs computer: takes back the computer's reply and the human move, as in Stello. Human vs human: one move. |
| Settings dialog | Yes | Same as Stello: fixed depth / seconds per move / minutes per game. Plus the endgame threshold. |
| Analysis panel (Ctrl+A) | Yes | As Stello: depth, score (Red's view), best move, principal variation, nodes, time, "solving" state. No score per column. Empty in human vs human (no search runs). |
| Add Game to Book, Evaluate Book, Self-play, Stop Learning | No | No opening book. The Book menu is removed. |
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
- Caption: "Claus Pedersen – assisted Opus 5.5 making a Connect 4 in C# in 2026".
- "Connect 4 is a game for two players on a board with 7 columns and 6 rows. You play against the computer or against a friend, and you can see the computer think in the analysis panel."
- "Its brain searches ahead with alpha-beta search, a hash table and iterative deepening, and judges positions by their threats and open lines. Near the end of the game it works out the exact result and plays perfectly."
- "The search core is a C# port of Pascal Pons' C++ Connect 4 solver. The same brain runs as a Windows program (WPF) and in the browser (Blazor WebAssembly)."

## 6. Blazor web app

As Stello.Web:

- Blazor WebAssembly standalone, engine runs in a Web Worker via `[JSExport]`, AOT compilation in Release (`wasm-tools` workload).
- Pages: `/` game, `/docs` and `/docs/{slug}` brain documentation (Markdown → HTML with Markdig), NotFound.
- Settings and appearance in local storage. No book.
- Same hash table sizes as desktop (2^24 entries each, ≈ 218 MB in the Web Worker; fallback 2^20, see 4.6).
- Sound with HTML audio; the sound files are in `wwwroot/sounds/`.
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
