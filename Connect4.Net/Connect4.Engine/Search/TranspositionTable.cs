namespace Connect4.Engine.Search;

internal enum Bound : byte
{
    None,
    Lower,
    Upper,
    Exact,
}

/// <param name="Column">The best move found (0-based), or -1.</param>
internal readonly record struct TableEntry(int Depth, Bound Bound, int Value, int Column);

/// <summary>
/// Hash table of the heuristic search, one <see cref="ulong"/> per entry:
/// column (3 bits, 7 = none), bound (2), depth (6), value (16), age (4) and key check (33).
/// The slot and key check come from a bijective hash of the 49-bit <see cref="Position.Key"/>,
/// so together they identify the position exactly and a hit is never wrong.
/// </summary>
internal sealed class TranspositionTable
{
    public const int DefaultLogSize = 24;

    // The 49 key bits minus the slot bits must fit in the 33-bit key check.
    public const int MinLogSize = 16;
    public const int MaxLogSize = 26;

    private const int KeyBits = 49;
    private const ulong KeyMask = (1UL << KeyBits) - 1;

    // Multiplying by an odd number modulo 2^49 is a bijection on 49-bit keys.
    private const ulong Multiplier = 0x9E37_79B9_7F4A_7C15UL;

    private const int BoundShift = 3;
    private const int DepthShift = 5;
    private const int ValueShift = 11;
    private const int AgeShift = 27;
    private const int CheckShift = 31;
    private const int MaxStoredDepth = 63;
    private const int NoColumn = 7;

    private readonly ulong[] _entries;
    private readonly int _checkBits;
    private ulong _age;

    public TranspositionTable(int logSize = DefaultLogSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(logSize, MinLogSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(logSize, MaxLogSize);

        _entries = new ulong[1 << logSize];
        _checkBits = KeyBits - logSize;
    }

    public int Size => _entries.Length;

    public void Clear()
    {
        Array.Clear(_entries);
        _age = 0;
    }

    /// <summary>Marks entries from earlier searches as old, so they are replaced first.</summary>
    public void NewSearch() => _age = (_age + 1) & 0xF;

    public bool TryGet(ulong key, out TableEntry entry)
    {
        (int slot, ulong check) = Hash(key);
        ulong data = _entries[slot];
        if (data == 0 || data >> CheckShift != check)
        {
            entry = default;
            return false;
        }

        int column = (int)(data & 0x7);
        entry = new TableEntry(
            (int)((data >> DepthShift) & 0x3F),
            (Bound)((data >> BoundShift) & 0x3),
            (short)(data >> ValueShift),
            column == NoColumn ? -1 : column);
        return true;
    }

    /// <summary>Replaces an entry of another position only if it is older or not deeper.</summary>
    public void Store(ulong key, int depth, Bound bound, int value, int column)
    {
        (int slot, ulong check) = Hash(key);
        ulong old = _entries[slot];
        if (old != 0 && old >> CheckShift != check && ((old >> AgeShift) & 0xF) == _age
            && (int)((old >> DepthShift) & 0x3F) > depth)
        {
            return;
        }

        _entries[slot] = check << CheckShift
            | _age << AgeShift
            | (ulong)(ushort)(short)value << ValueShift
            | (ulong)(uint)Math.Clamp(depth, 0, MaxStoredDepth) << DepthShift
            | (ulong)bound << BoundShift
            | (uint)(column < 0 ? NoColumn : column);
    }

    private (int Slot, ulong Check) Hash(ulong key)
    {
        ulong hash = (key * Multiplier) & KeyMask;
        return ((int)(hash >> _checkBits), hash & ((1UL << _checkBits) - 1));
    }
}
