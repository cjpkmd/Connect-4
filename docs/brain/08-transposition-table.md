# 08 – Transposition table

[Back to the index](README.md)

## In short

Many move orders lead to the same position. The **transposition table** (hash table) remembers what the search found for a position — a score or a bound, the depth, and the best move — so the position is not searched again, and the best move is tried first in the next iteration. It follows Stello's design, but each entry is packed into one 64-bit number, so 2^24 entries take 128 MiB.

## The entry

[TranspositionTable.cs](../../Connect4.Net/Connect4.Engine/Search/TranspositionTable.cs):

| Bits | Field | Contents |
|---|---|---|
| 0–2 | Column | The best move, 0–6; 7 = none |
| 3–4 | Bound | `Lower`, `Upper` or `Exact` (0 = empty entry) |
| 5–10 | Depth | The remaining depth of the search that stored it, 0–63 |
| 11–26 | Value | The score, a signed 16-bit number |
| 27–30 | Age | Which search stored it |
| 31–63 | Key check | The part of the key not given by the slot |

A **lower** bound means the score is at least the value (a cutoff: the search stopped when a move reached β); an **upper** bound means at most the value (no move beat α); **exact** means the value is the score.

```text
bit 63                          31 30   27 26            11 10     5 4    3 2    0
    +-----------------------------+-------+----------------+--------+------+------+
    | key check (33)              | age   | value (16)     | depth  | bound| col  |
    +-----------------------------+-------+----------------+--------+------+------+
```

## The slot and the key check

The position key is 49 bits (chapter 02). The table multiplies it by an odd constant modulo $2^{49}$, which is a bijection (every key gives a different result):

$$
h = (\text{key} \cdot \texttt{0x9E3779B97F4A7C15}) \bmod 2^{49}
$$

With $2^k$ slots, the top $k$ bits of $h$ choose the slot and the other $49 - k$ bits are stored as the key check. Slot and key check together are $h$, and $h$ gives back the key, so a hit is never a different position: there are no false hits. The multiplication also spreads the keys, whose low bits (the left columns) would otherwise cluster.

```text
h (49 bits):   [ slot: top k bits ][ key check: low 49 - k bits ]
               with k = 24:  24 bits      25 bits
```

The 33-bit key check field limits the table to at least $2^{16}$ slots; the maximum is $2^{26}$.

## Using the entries

In `Negamax` (chapter 07):

- **Lookup:** if the entry's depth is at least the remaining depth, an exact value is returned, a lower bound ≥ β or an upper bound ≤ α too. Otherwise only the best move is used, for the move ordering.
- **Store:** after searching the node, with the bound found from the original window.

## Replacement

Each slot holds one entry. A new entry replaces the old one, except when the old one is from another position, from the same search, and deeper. `NewSearch()` starts a new age at every search, so entries from earlier moves are replaced first. `Clear()` empties the table at New Game; between moves in the same game it is kept, which makes the next search start where the last one ended.

## Size

| Where | Entries | Memory |
|---|---|---|
| Desktop and browser | $2^{24}$ | 128 MiB |
| If that cannot be allocated | $2^{20}$ | 8 MiB |
| Tests | $2^{16}$ | 512 KiB |

`SearchEngine` falls back to $2^{20}$ entries when the allocation fails (for example in a browser on a phone) and sets `UsesFallbackTables`; the app then shows a notice.

## How it is tested

[TranspositionTableTests.cs](../../Connect4.Net/Connect4.Engine.Tests/TranspositionTableTests.cs): store and look up, misses, negative and win scores, no false hits for 65 536 keys, the replacement rules, and the size limits.
