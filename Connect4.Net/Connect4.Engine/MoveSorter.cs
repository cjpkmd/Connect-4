using System.Runtime.CompilerServices;

namespace Connect4.Engine;

/// <summary>
/// Insertion sort of at most <see cref="Position.Width"/> moves, returned by decreasing score;
/// equal scores come back in reverse order of adding (ported from connect4-master <c>MoveSorter.hpp</c>).
/// </summary>
internal struct MoveSorter
{
    private Entries _entries;
    private int _size;

    public void Add(ulong move, int score)
    {
        int index = _size++;
        for (; index > 0 && _entries[index - 1].Score > score; index--)
        {
            _entries[index] = _entries[index - 1];
        }

        _entries[index] = new Entry(move, score);
    }

    /// <summary>The remaining move with the highest score; 0 when empty.</summary>
    public ulong GetNext() => _size > 0 ? _entries[--_size].Move : 0;

    private readonly record struct Entry(ulong Move, int Score);

    [InlineArray(Position.Width)]
    private struct Entries
    {
        private Entry _element;
    }
}
