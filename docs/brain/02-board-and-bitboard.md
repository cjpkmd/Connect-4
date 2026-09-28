# 02 – Board and bitboard

[Back to the index](README.md)

## In short

A position is stored as two 64-bit numbers: the discs of the side to move, and all discs. This is the layout of Pascal Pons' solver, ported unchanged. It makes every question the search asks (can I play here, can I win, which cells are threats) a few shifts and ANDs.

## The bit layout

Each column uses 7 bits: 6 for the cells, bottom to top, and one empty bit on top. The 7 columns use bits 0–48.

```text
.  .  .  .  .  .  .      <- the extra bit of each column, always 0
5 12 19 26 33 40 47
4 11 18 25 32 39 46
3 10 17 24 31 38 45
2  9 16 23 30 37 44
1  8 15 22 29 36 43
0  7 14 21 28 35 42
```

The cell in column $c$ and row $r$ (both 0-based, row 0 at the bottom) is bit $7c + r$ (`Position.CellBit`). The extra bit keeps lines from wrapping: a vertical or diagonal shift out of the top of a column lands in the empty extra bit, never in the next column.

For example, after `4453` (Red 4, Yellow 4, Red 5, Yellow 3) Red is to move, so `Current` holds Red's discs. Only the bottom two rows are shown:

```text
Board            Current (Red)    Mask (all discs)
. . . Y . . .    . . . . . . .    . . . 1 . . .
. . Y R R . .    . . . 1 1 . .    . . 1 1 1 . .
```

After Red's next move, the same discs are stored the other way round: `Current` then holds Yellow's discs.

## The position

`Position` ([Position.cs](../../Connect4.Net/Connect4.Engine/Position.cs)) is a `readonly record struct`:

| Member | Contents |
|---|---|
| `Current` (internal) | The discs of the side to move (C++ `current_position`) |
| `Mask` (internal) | All discs (C++ `mask`) |
| `Moves` | The number of discs on the board |
| `ToMove` | Red when `Moves` is even, Yellow when it is odd |
| `Discs(player)` | `Current`, or `Current ^ Mask` for the other player |
| `this[column, row]` | The disc in a cell, or null |

Because the bitboards are relative to the side to move, playing a move is just:

```csharp
new Position(Current ^ Mask, Mask | move, Moves + 1)
```

The old side to move's discs (`Current`) become the opponent's (`Current ^ Mask` after the new disc is added), and the move is added to the mask.

## Useful masks

| Name | Value | Meaning |
|---|---|---|
| `BottomMask` | bits 0, 7, 14, …, 42 | The bottom cell of each column |
| `BoardMask` | `BottomMask` × 0b111111 | All 42 cells |
| `ColumnMask(c)` | 6 bits from $7c$ | The cells of one column |

The cell a disc lands on in column $c$ is `(Mask + BottomMaskColumn(c)) & ColumnMask(c)`: adding the column's bottom bit to its filled cells carries up to the first empty cell. For all columns at once, `Possible = (Mask + BottomMask) & BoardMask` gives the playable cell of every column that is not full.

Column 4 of the example, as the 7 bits of the column (top bit first):

```text
Mask                0000011    two discs
+ bottom bit        0000001
= landing cell      0000100    the carry stops at row 2, the first empty cell
```

## The key

`Key = Current + Mask` is a unique 49-bit number for the position. In each column, `Mask + BottomMask` has a single 1 just above the top disc; adding `Current` puts the side to move's discs below it, so both the height and the colours can be read back. `Key` is that number minus the constant `BottomMask`, so it is unique too. In column 4 of the example:

```text
Mask + bottom bit   0000100    the marker: the column holds two discs
+ Current           0000001    Red's disc at the bottom
= 0000101           marker and colours; minus the bottom bit this column's part of Key is 0000100
```

Two move orders that reach the same discs (transpositions, e.g. `1234` and `3214`) have the same key. It is used by both hash tables (chapters 08 and 09).

The C++ code also has a mirror-symmetric base-3 key (`key3`) for its opening book. It was not ported; the opening book (chapter 11) uses `CanonicalKey` instead: the smaller of `Key` and `Mirror().Key`, where `Mirror()` swaps the 7-bit column groups, so a position and its mirror image get the same key.

## Text

`Position.ToString()` gives six lines, top row first, with `R`, `Y` and `.`; `Position.FromMoves("4453")` builds a position from 1-based columns (chapter 04).
