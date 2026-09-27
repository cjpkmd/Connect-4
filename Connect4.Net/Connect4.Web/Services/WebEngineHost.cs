using System.Diagnostics;
using Microsoft.JSInterop;
using Connect4.App.Services;
using Connect4.Engine;
using Connect4.Web.Worker;

namespace Connect4.Web.Services;

/// <summary>
/// Runs the engine in a Web Worker so the page stays responsive. A running search cannot be interrupted inside
/// the worker, so Stop and Move Now terminate it and start a new one (with fresh hash tables).
/// </summary>
public sealed class WebEngineHost : IEngineHost
{
    // As on the desktop; the engine falls back to 2^20 entries per table when the browser cannot allocate them.
    private const int HashLogSize = 24;
    private const int EndgameLogSize = 24;

    private readonly IJSRuntime _js;
    private readonly DotNetObjectReference<WebEngineHost> _self;
    private IJSInProcessObjectReference? _client;
    private Task _started;
    private Action<SearchInfo>? _progress;

    public WebEngineHost(IJSRuntime js)
    {
        _js = js;
        _self = DotNetObjectReference.Create(this);

        // Start at once, so the worker is usually ready before the computer's first move.
        _started = StartAsync();
    }

    public async Task<SearchResult> ChooseMoveAsync(
        IReadOnlyList<int> moves,
        SearchLimits limits,
        IProgress<SearchInfo>? progress,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        IJSInProcessObjectReference client = await StartedAsync();

        SearchInfo? last = null;
        var stopped = new TaskCompletionSource<bool>();
        using CancellationTokenRegistration cancelled = cancellationToken.Register(() => stopped.TrySetResult(false));
        using CancellationTokenRegistration movedNow = moveNowToken.Register(() => stopped.TrySetResult(true));

        _progress = info =>
        {
            last = info;
            progress?.Report(info);
        };
        Task<string> search = client.InvokeAsync<string>("call", "chooseMove", EngineProtocol.WriteRequest(moves, limits)).AsTask();
        try
        {
            if (await Task.WhenAny(search, stopped.Task) == search)
            {
                return EngineProtocol.ReadResult(await search);
            }
        }
        finally
        {
            _progress = null;
        }

        Forget(search);
        Restart(client);
        if (!await stopped.Task)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        return EngineProtocol.MoveNowResult(moves, last, clock.Elapsed);
    }

    public void NewGame()
    {
        // A worker that is still starting has empty tables anyway.
        if (_client is not null && _started.IsCompletedSuccessfully)
        {
            Forget(_client.InvokeVoidAsync("call", "newGame").AsTask());
        }
    }

    [JSInvokable]
    public void OnProgress(string json) => _progress?.Invoke(EngineProtocol.ReadProgress(json));

    private static void Forget(Task task) =>
        task.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);

    private async Task<IJSInProcessObjectReference> StartedAsync()
    {
        if (_started.IsFaulted)
        {
            _started = StartAsync();
        }

        await _started;
        return _client!;
    }

    private async Task StartAsync()
    {
        _client ??= await _js.InvokeAsync<IJSInProcessObjectReference>("import", "./js/engine-client.js");
        await _client.InvokeVoidAsync("start", _self, HashLogSize, EndgameLogSize);
    }

    private void Restart(IJSInProcessObjectReference client)
    {
        client.InvokeVoid("terminate");
        _started = StartAsync();
        Forget(_started);
    }
}
