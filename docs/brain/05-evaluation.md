# 05 – Evaluation

[Back to the index](README.md)

## In short

When the search stops at its depth limit, the position there is scored by `Evaluator` ([Evaluator.cs](../../Connect4.Net/Connect4.Engine/Evaluation/Evaluator.cs)). connect4-master has no evaluation (it always searches to the end), so this part is new. It counts what matters in Connect 4: **threats** (empty cells that would complete a four), which **rows** they are on, **stacked** threats, **open lines** and **centre** discs. The score is in points from Red's view, clamped to ±1 000; `Evaluate` negates it when Yellow is to move.

## The terms

$$
\text{score}_{\text{Red}} = \sum_{\text{terms}} w_t \cdot (\text{Red's count}_t - \text{Yellow's count}_t)
$$

| Term | Counted | Default weight |
|---|---|---|
| Threat | Empty cells that complete a four (`ComputeWinningPosition`, chapter 03) | 30 |
| Parity threat | Threats on the player's good rows: odd rows 1, 3, 5 (counted from the bottom) for Red, even rows 2, 4, 6 for Yellow | +20 |
| Stacked threat | A threat with another threat of the same player directly above it | 40 |
| Open two | A line of four cells with two own discs and no opponent disc | 4 |
| Open three | A line of four cells with three own discs and no opponent disc | 6 |
| Centre | Every disc, times the number of lines of four through its cell | 1 |

The weights are in `EvaluationWeights`, a record with the defaults above, so they can be tuned without changing code.

### Why odd and even rows

Late in the game the players are often forced to fill columns one disc at a time. Red moves first, so when the remaining cells are filled in turn, Red gets the cells on odd rows and Yellow those on even rows (this is the **zugzwang** analysis by Victor Allis). A Red threat on an odd row is therefore likely to be realised, and so is a Yellow threat on an even row. A cell that is a threat for both players gives this bonus only to the player whose good row it is on, so shared threats need no separate term.

```text
row 6   even   Yellow's good row
row 5   odd    Red's good row
row 4   even   Yellow's good row
row 3   odd    Red's good row
row 2   even   Yellow's good row
row 1   odd    Red's good row
```

### Stacked threats

Two threats of the same player on top of each other in one column usually win: when the opponent blocks the lower one, the player wins on the upper one.

```text
one column
row 4   .    Red's second threat
row 3   .    Red's threat: when Yellow blocks it, Red plays row 4 and wins
row 2   Y
```

### Centre

The number of lines of four through a cell:

| | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| **row 6** | 3 | 4 | 5 | 7 | 5 | 4 | 3 |
| **row 5** | 4 | 6 | 8 | 10 | 8 | 6 | 4 |
| **row 4** | 5 | 8 | 11 | 13 | 11 | 8 | 5 |
| **row 3** | 5 | 8 | 11 | 13 | 11 | 8 | 5 |
| **row 2** | 4 | 6 | 8 | 10 | 8 | 6 | 4 |
| **row 1** | 3 | 4 | 5 | 7 | 5 | 4 | 3 |

The table is not typed in: it is counted from the 69 lines of four (`Evaluator.Lines`), and the cells are grouped by count, so the centre term costs one popcount per group.

## Properties

- **Symmetric:** the mirrored position has the same score.
- **Colour swap:** swapping the colours negates the score, except for the odd/even term (it depends on who moved first).
- **Range:** clamped to ±1 000 (`Evaluator.MaxScore`), below the win scores of the search (chapter 07).
- **Speed:** about 140 ns per position; most of it is the loop over the 69 lines.

## How good is it?

Against the exact scores of Pascal Pons' test positions, the sign of the evaluation (without any search) agrees with the result in 62–80 % of the won or lost positions:

| Test set | Agreement |
|---|---|
| `Test_L3_R1` (end, easy) | 68.5 % |
| `Test_L2_R1` (middle, easy) | 76.5 % |
| `Test_L2_R2` (middle, medium) | 61.9 % |
| `Test_L1_R1` (beginning, easy) | 79.9 % |
| `Test_L1_R2` (beginning, medium) | 71.8 % |

A test keeps the agreement above 70 % for two of the sets, so a change of the weights that makes it worse is noticed.

**Known weakness:** with 1 second on the empty board the search (depth about 15) chose column 2; with 5 seconds (depth 19) it chose the centre, which is the only winning first move. A larger centre weight is the first thing to try when tuning.

## How it is tested

[EvaluatorTests.cs](../../Connect4.Net/Connect4.Engine.Tests/EvaluatorTests.cs) checks the 69 lines and the centre table, each term on its own (the difference with and without the term), the symmetries on random positions, the clamping, and the agreement above.
