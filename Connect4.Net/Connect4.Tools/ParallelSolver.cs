using Connect4.Engine;
using Connect4.Engine.Endgame;

namespace Connect4.Tools;

/// <summary>Strong-solves positions on several threads, one <see cref="EndgameSolver"/> (with its own table) per thread.</summary>
internal static class ParallelSolver
{
    /// <param name="onSolved">Called on the worker threads, once per solved position.</param>
    /// <param name="onTick">Called about once a second on the calling thread while the workers run.</param>
    /// <remarks>Returns early when <paramref name="cancellation"/> is cancelled; positions being solved then are skipped.</remarks>
    public static void SolveAll(
        IReadOnlyList<string> positions,
        int workers,
        int tableLogSize,
        Action<string, int> onSolved,
        Action onTick,
        CancellationToken cancellation) =>
        ForEach(
            positions,
            workers,
            tableLogSize,
            (solver, moves) => onSolved(moves, solver.Solve(Position.FromMoves(moves), cancellation: cancellation)),
            onTick,
            cancellation);

    /// <param name="work">Called on the worker threads, once per item; an <see cref="OperationCanceledException"/> stops that worker.</param>
    /// <param name="onTick">Called about once a second on the calling thread while the workers run.</param>
    public static void ForEach<T>(
        IReadOnlyList<T> items,
        int workers,
        int tableLogSize,
        Action<EndgameSolver, T> work,
        Action onTick,
        CancellationToken cancellation)
    {
        int next = -1;
        var threads = Enumerable.Range(0, Math.Min(workers, items.Count))
            .Select(_ => new Thread(() =>
            {
                var solver = new EndgameSolver(tableLogSize);
                int index;
                while (!cancellation.IsCancellationRequested && (index = Interlocked.Increment(ref next)) < items.Count)
                {
                    try
                    {
                        work(solver, items[index]);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            })
            { IsBackground = true })
            .ToList();

        threads.ForEach(thread => thread.Start());
        foreach (Thread thread in threads)
        {
            while (!thread.Join(1000))
            {
                onTick();
            }
        }

        onTick();
    }
}
