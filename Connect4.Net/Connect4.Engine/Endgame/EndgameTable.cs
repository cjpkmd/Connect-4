namespace Connect4.Engine.Endgame;

/// <summary>
/// Hash table of the endgame solver, ported from connect4-master <c>TranspositionTable.hpp</c>.
/// The last entry wins on a collision. Only the low 32 bits of the 49-bit key are stored; the prime
/// table size makes (key mod 2^32, key mod size) unique for every key (Chinese remainder theorem),
/// so a hit is never wrong. A value of 0 means "no entry".
/// </summary>
internal sealed class EndgameTable
{
    public const int DefaultLogSize = 24;

    // 32 stored key bits + log size bits must cover the 49-bit key.
    public const int MinLogSize = 17;
    public const int MaxLogSize = 27;

    private readonly uint[] _keys;
    private readonly byte[] _values;

    public EndgameTable(int logSize = DefaultLogSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(logSize, MinLogSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(logSize, MaxLogSize);

        Size = NextPrime(1 << logSize);
        _keys = new uint[Size];
        _values = new byte[Size];
    }

    public int Size { get; }

    public void Reset()
    {
        Array.Clear(_keys);
        Array.Clear(_values);
    }

    public void Put(ulong key, byte value)
    {
        int index = Index(key);
        _keys[index] = (uint)key;
        _values[index] = value;
    }

    public byte Get(ulong key)
    {
        int index = Index(key);
        return _keys[index] == (uint)key ? _values[index] : (byte)0;
    }

    private int Index(ulong key) => (int)(key % (ulong)Size);

    internal static int NextPrime(int n)
    {
        while (!IsPrime(n))
        {
            n++;
        }

        return n;
    }

    private static bool IsPrime(int n)
    {
        if (n < 2)
        {
            return false;
        }

        for (int divisor = 2; (long)divisor * divisor <= n; divisor++)
        {
            if (n % divisor == 0)
            {
                return false;
            }
        }

        return true;
    }
}
