# 06 – Move ordering

[Back to the index](README.md)

## In short

Alpha-beta prunes most when the best move is searched first (chapter 07). The order comes from connect4-master: moves that create the most threats first, and among those the centre columns first. The search puts the move from the hash table in front of them.

## The order

[MoveOrdering.cs](../../Connect4.Net/Connect4.Engine/Search/MoveOrdering.cs) adds each non-losing move to a `MoveSorter` with a score:

| Move | Score |
|---|---|
| The best move stored in the hash table for this position | `int.MaxValue` |
| Any other move | `Position.MoveScore(move)`: the number of threats the side to move has after the move (C++ `moveScore`) |

The moves are added in reverse column order, and the sorter returns equal scores in reverse order of adding, so between equal scores the order is the column order:

$$
3, 2, 4, 1, 5, 0, 6 \quad (\text{0-based})
$$

On the board, from the centre outwards:

| Column (1-based) | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Tried as number | 6 | 4 | 2 | 1 | 3 | 5 | 7 |

The C++ comment says `3, 4, 2, 5, 1, 6, 0`, but its formula `WIDTH/2 + (1 - 2*(i%2)) * (i+1)/2` gives the order above, and that order is kept.

## MoveSorter

[MoveSorter.cs](../../Connect4.Net/Connect4.Engine/MoveSorter.cs) is the C++ insertion sort for at most 7 moves. It is a struct with an inline array, so it lives on the stack and a node allocates nothing. `GetNext()` returns the move with the highest score and 0 when none are left.

## Where it is used

- In every node of the heuristic search, with the hash move (chapter 07).
- At the root, to order the root moves before the first iteration; after each iteration the best moves are moved to the front.
- In the endgame solver, without a hash move, exactly as in C++ (chapter 09).

Only non-losing moves (chapter 03) are ordered, so a move that lets the opponent win at once is never searched.
