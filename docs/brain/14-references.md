# 14 – References

[Back to the index](README.md)

## Connect 4 solving

- Pascal Pons, **Solving Connect 4: how to build a perfect AI** – the tutorial that explains the solver ported in this project step by step: negamax, alpha-beta, move ordering, bitboards, the transposition table, iterative deepening, the lower-bound table, the opening book. <http://blog.gamesolver.org/>
- Pascal Pons, **connect4** (C++ source code of the solver, AGPL-3.0) – `Position.hpp`, `Solver.cpp`, `TranspositionTable.hpp`, `MoveSorter.hpp` and the test sets used in the tests. <https://github.com/PascalPons/connect4>
- Pascal Pons, **Connect 4 solver online** – plays and analyses positions in the same `4453` notation. <https://connect4.gamesolver.org/>
- Victor Allis, **A Knowledge-based Approach of Connect-Four** (master's thesis, Vrije Universiteit Amsterdam, 1988) – one of the two first solutions of the game (James D. Allen announced the other two weeks earlier), and the rules about odd and even threats (zugzwang) behind the parity term of the evaluation. <https://tromp.github.io/c4/connect4_thesis.pdf>
- John Tromp, **John's Connect Four Playground** – the 8-ply database, the Fhourstones benchmark, the 49-bit bitboard that Pascal Pons' code also uses, and the number of positions (4 531 985 219 092). <https://tromp.github.io/c4/c4.html>

## Search

- **Chess Programming Wiki** – articles on alpha-beta, negamax, iterative deepening, principal variation search, transposition tables and perft. <https://www.chessprogramming.org/>
- Donald E. Knuth and Ronald W. Moore, **An Analysis of Alpha-Beta Pruning**, Artificial Intelligence 6 (1975) – the classic analysis of alpha-beta.

## This project

- The **Stello** documentation – the Othello program whose search framework, time control and app this project reuses.
- [Connect 4 porting documentation.md](../../Connect%204%20porting%20documentation.md) – how the C++ code was ported, part by part.
