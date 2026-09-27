namespace Connect4.Engine.Book;

/// <summary>
/// Builds a full-width opening book: every position with at most <c>depth</c> discs (mirror images once).
/// Only the leaves (exactly <c>depth</c> discs) are solved; shallower scores are backed up by negamax.
/// </summary>
public static class BookBuilder
{
    public const int MaxDepth = 16;

    internal delegate bool ScoreLookup(ulong canonicalKey, out int score);

    /// <summary>
    /// One move sequence per unique position, by number of discs: <c>levels[ply]</c> is sorted, and mirror images
    /// appear once. Positions after a winning move are left out (as <c>generator.cpp explore</c>).
    /// </summary>
    public static IReadOnlyList<string>[] Enumerate(int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(depth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(depth, MaxDepth);

        var levels = new IReadOnlyList<string>[depth + 1];
        List<string> moves = [""];
        List<Position> positions = [Position.Empty];
        levels[0] = moves;
        for (int ply = 1; ply <= depth; ply++)
        {
            var seen = new HashSet<ulong>();
            var nextMoves = new List<string>();
            var nextPositions = new List<Position>();
            for (int i = 0; i < positions.Count; i++)
            {
                Position parent = positions[i];
                for (int column = 0; column < Position.Width; column++)
                {
                    if (!parent.CanPlay(column) || parent.IsWinningMove(column))
                    {
                        continue;
                    }

                    Position child = parent.Play(column);
                    if (seen.Add(child.CanonicalKey))
                    {
                        nextMoves.Add(moves[i] + (char)('1' + column));
                        nextPositions.Add(child);
                    }
                }
            }

            moves = nextMoves;
            positions = nextPositions;
            levels[ply] = moves;
        }

        return levels;
    }

    /// <summary>The book lines of all levels, shallowest first, from the solved leaves (the last level).</summary>
    /// <exception cref="ArgumentException">A leaf has no score.</exception>
    public static List<BookLine> BackUp(IReadOnlyList<string>[] levels, IReadOnlyDictionary<string, int> leafScores)
    {
        int depth = levels.Length - 1;
        var scores = new Dictionary<ulong, int>();
        foreach (string leaf in levels[depth])
        {
            if (!leafScores.TryGetValue(leaf, out int score))
            {
                throw new ArgumentException($"No score for the leaf \"{leaf}\".", nameof(leafScores));
            }

            scores[Position.FromMoves(leaf).CanonicalKey] = score;
        }

        for (int ply = depth - 1; ply >= 0; ply--)
        {
            foreach (string moves in levels[ply])
            {
                Position position = Position.FromMoves(moves);
                if (!TryBackUp(position, scores.TryGetValue, out int score, out _))
                {
                    throw new InvalidOperationException($"A child of \"{moves}\" is missing.");
                }

                scores[position.CanonicalKey] = score;
            }
        }

        var lines = new List<BookLine>(scores.Count);
        foreach (IReadOnlyList<string> level in levels)
        {
            lines.AddRange(level.Select(moves => new BookLine(moves, scores[Position.FromMoves(moves).CanonicalKey])));
        }

        return lines;
    }

    /// <summary>Enumerates, solves every leaf with <paramref name="solve"/> and backs up the scores.</summary>
    public static List<BookLine> Build(int depth, Func<Position, int> solve)
    {
        IReadOnlyList<string>[] levels = Enumerate(depth);
        var leafScores = levels[depth].ToDictionary(moves => moves, moves => solve(Position.FromMoves(moves)));
        return BackUp(levels, leafScores);
    }

    /// <summary>The lines above the book depth whose score is not the back-up of their children's scores.</summary>
    public static List<BookLine> CheckBackUp(IReadOnlyList<BookLine> lines)
    {
        OpeningBook book = OpeningBook.FromLines(lines);
        return lines
            .Where(line => line.Moves.Length < book.Depth)
            .Where(line => !book.TryGetBackedUpScore(Position.FromMoves(line.Moves), out int score, out _) || score != line.Score)
            .ToList();
    }

    /// <summary>
    /// The negamax score of a position from its children: a winning move scores (43 − moves) / 2, other moves
    /// −(child score). <paramref name="bestColumns"/> has a bit for every column with the best score.
    /// </summary>
    /// <returns>False if a child's score is missing.</returns>
    internal static bool TryBackUp(Position position, ScoreLookup lookup, out int score, out int bestColumns)
    {
        score = int.MinValue;
        bestColumns = 0;
        for (int column = 0; column < Position.Width; column++)
        {
            if (!position.CanPlay(column))
            {
                continue;
            }

            int value;
            if (position.IsWinningMove(column))
            {
                value = (Position.CellCount + 1 - position.Moves) / 2;
            }
            else if (lookup(position.Play(column).CanonicalKey, out int child))
            {
                value = -child;
            }
            else
            {
                score = 0;
                bestColumns = 0;
                return false;
            }

            if (value > score)
            {
                score = value;
                bestColumns = 1 << column;
            }
            else if (value == score)
            {
                bestColumns |= 1 << column;
            }
        }

        if (bestColumns == 0)
        {
            score = 0;
            return false;
        }

        return true;
    }
}
