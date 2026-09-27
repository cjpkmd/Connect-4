# Connect 4 porting documentation

This document describes, phase by phase, how the Connect 4 program was built: which parts were ported from Pascal Pons' C++ solver (`connect4-master/`, AGPL-3.0; a private project, so the licence is not an issue), which were taken over from Stello (the author's Othello program), and which are new. For each part it says whether it is a **1:1 port**, **changed**, or **new**, and why.

The goal of the brain (from the specification): connect4-master is used as a fast search core — bitboard, move generation, threat detection, move ordering — and its exact solver is used near the end of the game. The move itself is chosen as in Stello: iterative deepening alpha-beta, stopped by a depth or time limit, with a heuristic evaluation at the horizon. The first 6 plies come from a small opening book of solved positions (phases 11 and 12).

The engine is described in detail in [docs/brain](docs/brain/README.md).

## Overview

| C++ (connect4-master) | C# | Phase | Assessment |
|---|---|---|---|
| `Position.hpp` | `Position`, `Player` | 2 | 1:1 port, plus helpers for the app |
| (none) | `Game`, `GameRecordFormat` | 2 | New (Stello design) |
| `MoveSorter.hpp` | `MoveSorter` | 3 | 1:1 port |
| `Solver.cpp` (`solve`, `negamax`, `analyze`) | `Endgame/EndgameSolver` | 3 | 1:1 port, plus cancellation and a time limit |
| `TranspositionTable.hpp` | `Endgame/EndgameTable` | 3 | 1:1 port for this key size |
| `Solver.cpp` column order and `moveScore` | `Search/MoveOrdering` | 3, 5 | 1:1 port, plus the hash move |
| (none) | `Evaluation/Evaluator`, `EvaluationWeights` | 4 | New |
| (none; Stello `SearchEngine`) | `SearchEngine`, `Search/TranspositionTable`, `Search/TimeControl`, `SearchLimits`, `Scores` | 5 | New, Stello design |
| (none; Stello `ComputerPlayer`) | `ComputerPlayer` | 6 | New, without a book |
| `OpeningBook.hpp`, `generator.cpp`, `key3` | `Book/OpeningBook`, `Book/BookBuilder`, `Book/BookFile`, `Position.Mirror`/`CanonicalKey`, `Connect4.Tools` | 11, 12 | Changed: same pipeline, full-width book with backed-up scores, text file, used by the search |
| `main.cpp` (command line) | — | — | Not ported: `Connect4.Tools` only makes the book so far |
| (Stello.App, Stello.Net, Stello.Web) | `Connect4.App`, `Connect4.WPF`, `Connect4.Web` | 7–9 | New, Stello design |

---

## Phase 1 – Setup

**C#:** the solution `Connect4.Net/Connect4.Net.slnx` with `Connect4.Engine`, `Connect4.App`, `Connect4.WPF`, `Connect4.Web` and the test projects `Connect4.Engine.Tests` and `Connect4.App.Tests`, with the same settings and package versions as Stello. The existing WPF project was renamed from `Connect 4.WPF` (namespace `Connect_4.WPF`) to `Connect4.WPF`. Pascal Pons' test files in `Test positions/` are linked into the test output.

**Assessment:** New; the layout is Stello's.

---

## Phase 2 – Board and rules

### C++

`Position` holds two `uint64_t` bitboards (`current_position`, `mask`) and the move count, with 7 bits per column. It provides `play`, `canPlay`, `isWinningMove`, `canWinNext`, `possibleNonLosingMoves`, `moveScore`, `key` and `key3`, and a template for boards wider than 64 bits (`__int128`). `play(seq)` reads a 1-based move string and stops at the first invalid move.

### C#

- `Position` is an immutable `readonly record struct`; `Play` returns a new position. The bit layout, `ComputeWinningPosition`, `NonLosingMoves` and `MoveScore` are the C++ code line for line.
- Only 7 × 6 is supported, so everything is `ulong`; the `__int128` template and `static_assert`s are gone. `popcount` is `BitOperations.PopCount`; the `constexpr` masks are `const` fields.
- Added for the app: the cell indexer, `Discs(player)`, `WinningCells(player)`, `FindFours` (the winning line), `ToString`, and `FromMoves`, which throws a `FormatException` instead of silently stopping at an invalid move.
- `Game` and `GameRecordFormat` are new: the history with undo/redo and the winner (Stello's `Game`), and the `4453` text format of connect4-master's input.

**Assessment:** 1:1 port of the bitboard code; changed only where C# makes it simpler (immutable struct, exceptions). `key3` was not ported; the opening book got `Mirror` and `CanonicalKey` in phase 11 instead.

**Tests:** 1 500 random games compared with a naive grid board (`ReferenceBoard`), perft 1–8, all 6 000 test positions played.

---

## Phase 3 – Endgame solver

### C++

- `Solver::solve` narrows the score window with null-window searches; `negamax` uses the non-losing moves, bounds from the move count, the transposition table (upper and lower bounds in one byte), the opening book and `MoveSorter`.
- `TranspositionTable<partial_key_t, key_t, value_t, log_size>` stores a truncated key and a value in two arrays; its size is the next prime above $2^{\text{log\_size}}$, computed at compile time, and `uint_t<S>` picks the key type.
- `columnOrder` is computed in the constructor.

### C#

- `EndgameSolver` is the same algorithm, line for line, without the opening book lookup.
- **Added:** a `CancellationToken` and a time limit, checked every 1 024 nodes. The time limit is checked with a clock (`Stopwatch.GetTimestamp`), not a timer, because in the browser's Web Worker a timer cannot fire while the search runs (found in phase 9).
- `EndgameTable` fixes the key part to `uint`: with 49-bit keys this is correct for tables of $2^{17}$ to $2^{27}$ entries (Chinese remainder theorem), and the default $2^{24}$ is the C++ size (16 777 259 entries, about 84 MB). The prime is found at run time.
- `MoveSorter` is a struct with an `[InlineArray(7)]`, so it lives on the stack.

**C++ quirk kept:** the comment says the column order is `3, 4, 2, 5, 1, 6, 0`, but the formula gives `3, 2, 4, 1, 5, 0, 6`. The formula's order is kept, so the solver tries the moves in the same order as the C++ program.

**C++ behaviour kept:** a weak solve only guarantees the sign of the score; the value can be outside [−1, 1].

**Tests:** all positions of `Test_L3_R1` and `Test_L2_R1` and the first 100 of `Test_L2_R2` in every run; the full `Test_L2_R2` and `Test_L1_R1`–`R3` as slow tests (all exact; `Test_L1_R3` spot-checked, 40 minutes in full). About 12 million positions per second.

---

## Phase 4 – Evaluation

**C++:** none; the solver always searches to the end.

**C#:** `Evaluator` with threats, threats on the player's good rows (odd rows for Red, even for Yellow), stacked threats, open lines with two and three discs, and centre weights counted from the 69 lines of four. The weights are in the record `EvaluationWeights` (30, +20, 40, 4, 6, 1), tuned by hand; the score is clamped to ±1 000.

**Assessment:** New. The sign agrees with the exact result in 62–80 % of the decided test positions. With 1 second on the empty board the search does not yet choose the centre column; more centre weight is the first tuning step to try.

---

## Phase 5 – Search

**C++:** none for depth-limited play.

**C#:** `SearchEngine`, following Stello's `SearchEngine`:

- Iterative deepening with principal variation search, a hash table, progress reports, `SearchLimits` (fixed depth, time per move, time per game, solve) and `TimeControl` (soft limit 2/3, hard limit), Move Now and cancellation — as in Stello.
- From connect4-master in every node: an immediate win ends the line, no non-losing move is a loss, a draw with 40 or more discs, and bounds from the move count.
- New: a single non-losing move does not use up depth (forced-move extension); the root searches with the window (best − 1, best + 1) to find all equal moves, and one is picked at random; the endgame solver is used when few cells are empty and time is left (chapter 09 of the docs).
- Scores: $\text{Win} - n$ with $n$ the absolute move number of the winning disc, instead of Stello's "win minus ply". The score of a position then does not depend on the root, so it can be stored in the hash table as it is.
- The hash table packs Stello's entry (key check, depth, bound, value, best move, age) into one `ulong`, with a bijective hash of the 49-bit key, so there are no false hits. $2^{24}$ entries take 128 MiB.

**Assessment:** New, Stello design. About 6–7 million positions per second; depth 15 in 1 second from the empty board.

---

## Phase 6 – Computer player

**C#:** `ComputerPlayer` wraps the search for the app: it refuses a finished game, and `NewGame` clears the search hash table (the endgame table is kept, its results never go stale). `SearchEngine` falls back to $2^{20}$-entry tables when the large ones cannot be allocated.

**Assessment:** New. Much thinner than Stello's `ComputerPlayer`, because there is no book.

---

## Phase 7 – App layer

**C#:** `Connect4.App` with `MainViewModel`, `AnalysisViewModel`, `SettingsViewModel`, `CellViewModel`, the models (`GameSettings` with the endgame threshold, `AppSettings`, `AboutInfo`) and the service interfaces, as in Stello.App.

**Changed from Stello:** two game modes (Human vs Computer, Human vs Human) that can be changed during a game; Undo and Redo go one move in Human vs Human; Switch Sides only against the computer; a sound service; scores shown from Red's view and as "Red wins in N moves"; no Book menu; the engine host gets the game as a list of moves. The default thinking time is 5 seconds per move (Stello: 5 minutes per game), because a Connect 4 game is short.

---

## Phase 8 – Desktop app

**C#:** `Connect4.WPF`, with Stello's menus, panels, dialogs, settings file and window placement. New: the Connect 4 board with seven column buttons, the hover preview, the falling-disc animation (clicks are ignored while a disc falls), the winning-line highlight, and generated sounds (`SoundWaves`) played one after the other.

---

## Phase 9 – Web app

**C#:** `Connect4.Web`, Blazor WebAssembly with the engine in a Web Worker, WebAssembly AOT in Release, Stello's menus, dialogs and docs pages, and Stello's board themes (blue or two woods, 3D discs, animation on or off). As in Stello, Move Now terminates the worker and plays the best move reported so far.

**Changed in the engine:** the endgame solver's time limit is checked with a clock (phase 3). With AOT the browser searches about as fast as the desktop.

---

## Phase 10 – Documentation and deployment

- The brain documentation in [docs/brain](docs/brain/README.md), also shown by the web app (Brain > How Connect 4 Thinks).
- This document.
- A GitHub Actions workflow (`.github/workflows/azure-static-web-apps.yml`) that runs the engine tests, publishes the web app with AOT and deploys it to Azure Static Web Apps on every push to `main` that changes the web app, the shared code, the images or the docs.

---

## Phase 11 – Opening book generator

### C++

- `generator <depth>` (`explore`) prints every position up to the depth once, mirror images once by `key3` (a base-3 key that is the same for a position and its mirror), leaving out games that have ended.
- The positions are solved outside the program. `generator` without a depth reads the lines `moves score` back into a `TranspositionTable` of $2^{23}$ entries with partial keys (positions up to 14 plies) and saves it as `7x6.book` (`OpeningBook::save`: a header, then the raw key and value arrays).
- `Solver::solve` asks the book first.

### C#

- `Position.Mirror()` swaps the 7-bit column groups; `CanonicalKey` is the smaller of `Key` and `Mirror().Key`. This replaces `key3`.
- `BookBuilder.Enumerate` is `explore` (breadth first, sorted). New: only the leaves (exactly *depth* discs) are solved, and `BookBuilder.BackUp` works out the shallower scores by negamax from the children, so the slow solves near the empty board are not needed.
- `BookFile`: a text file with one `moves score` line per position (the same lines `generator` reads), with `#` comments, instead of the binary hash-table dump.
- `OpeningBook`: a sorted `ulong[]` of `CanonicalKey << 8 | (score + 64)` with binary search instead of a hash table; `TryGetScore` and `TryGetMove` (the best child, random among equal ones).
- `Connect4.Tools book generate` solves the leaves on several threads, one `EndgameSolver` per thread, and appends every result to a `.partial` file so a stopped run can continue; `book verify` checks the book.

**Assessment:** Changed. The C++ pipeline (enumerate, solve, store) is kept, but the book is small and full-width, the file is text, and it is used by the search, not the solver.

**Tests:** positions per ply (1, 4, 25, 121, 568, 2 144, 8 231), the back-up against a plain negamax over a fake solver, the file format, lookups and random ties. A depth-1 book made with the real solver has the known first-move scores −2, −1, 0, +1, 0, −1, −2.

---

## Phase 12 – Opening book in the engine and apps

- The depth-6 book was generated on the author's computer: 11 094 positions, 8 231 leaves, 37 minutes with 12 threads (100 KB). It is `Connect4.Engine/Book/OpeningBook.txt`, an embedded resource, read by `OpeningBook.Default` the first time it is used.
- `SearchEngine.Search` plays the best book move at once when `SearchLimits.UseBook` is on and the position has fewer than 6 discs; `SearchResult.FromBook` marks it, and the analysis panel shows "book".
- `GameSettings.UseOpeningBook` (default on) with a check box in both Settings dialogs; the web worker protocol passes it on.
- The About text mentions the book.

**Assessment:** New. The C++ solver does not use this book (it only runs near the end of the game).

**Tests:** the built-in book has the known first-move scores and a consistent back-up, and agrees with every position of at most 6 moves in `Test_L1_R1`–`R3`; the search uses it, picks among equal book moves at random, and searches when the book is off. `book verify` solved a random sample of the book again.

## Not done

- **Tools project:** benchmarks, engine-against-engine matches for tuning, and a command line like the C++ `main.cpp` are left for later; `Connect4.Tools` only makes the opening book so far.
- **Tuning:** the evaluation weights are set by hand (phase 4).
