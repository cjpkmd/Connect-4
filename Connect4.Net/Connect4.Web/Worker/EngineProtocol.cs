using System.Text.Json;
using System.Text.Json.Serialization;
using Connect4.Engine;

namespace Connect4.Web.Worker;

// Messages between the page and the engine worker; plain values only, so ticks stay exact.
internal sealed record MoveRequest(int[] Moves, TimeControlMode Mode, int Depth, long TimeTicks, int EndgameThreshold);

internal sealed record MoveResponse(int Column, int Score, ScoreKind Kind, int Depth, long Nodes, long ElapsedTicks, int[] Line);

internal sealed record ProgressReport(int Depth, int Column, int BestColumn, int Score, ScoreKind Kind, long Nodes, long ElapsedTicks, int[] Line, bool Solving);

[JsonSerializable(typeof(MoveRequest))]
[JsonSerializable(typeof(MoveResponse))]
[JsonSerializable(typeof(ProgressReport))]
internal sealed partial class EngineProtocolJson : JsonSerializerContext;

internal static class EngineProtocol
{
    public static string WriteRequest(IReadOnlyList<int> moves, SearchLimits limits) => JsonSerializer.Serialize(
        new MoveRequest([.. moves], limits.Mode, limits.Depth, limits.Time.Ticks, limits.EndgameThreshold),
        EngineProtocolJson.Default.MoveRequest);

    public static (Game Game, SearchLimits Limits) ReadRequest(string json)
    {
        MoveRequest request = JsonSerializer.Deserialize(json, EngineProtocolJson.Default.MoveRequest)!;
        var game = new Game();
        foreach (int column in request.Moves)
        {
            game.Play(column);
        }

        TimeSpan time = TimeSpan.FromTicks(request.TimeTicks);
        SearchLimits limits = request.Mode switch
        {
            TimeControlMode.FixedDepth => SearchLimits.FixedDepth(request.Depth),
            TimeControlMode.TimePerMove => SearchLimits.TimePerMove(time),
            TimeControlMode.TimePerGame => SearchLimits.TimePerGame(time),
            _ => SearchLimits.Solve,
        };
        return (game, limits with { EndgameThreshold = request.EndgameThreshold });
    }

    public static string WriteResult(SearchResult result) => JsonSerializer.Serialize(
        new MoveResponse(result.Column, result.Score, result.Kind, result.Depth, result.Nodes, result.Elapsed.Ticks, [.. result.PrincipalVariation]),
        EngineProtocolJson.Default.MoveResponse);

    public static SearchResult ReadResult(string json)
    {
        MoveResponse response = JsonSerializer.Deserialize(json, EngineProtocolJson.Default.MoveResponse)!;
        return new SearchResult(
            response.Column, response.Score, response.Kind, response.Depth, response.Nodes,
            TimeSpan.FromTicks(response.ElapsedTicks), response.Line);
    }

    public static string WriteProgress(SearchInfo info) => JsonSerializer.Serialize(
        new ProgressReport(
            info.Depth, info.Column, info.BestColumn ?? -1, info.Score, info.Kind, info.Nodes, info.Elapsed.Ticks,
            [.. info.PrincipalVariation], info.Solving),
        EngineProtocolJson.Default.ProgressReport);

    public static SearchInfo ReadProgress(string json)
    {
        ProgressReport report = JsonSerializer.Deserialize(json, EngineProtocolJson.Default.ProgressReport)!;
        return new SearchInfo(
            report.Depth, report.Column, report.BestColumn >= 0 ? report.BestColumn : null, report.Score, report.Kind,
            report.Nodes, TimeSpan.FromTicks(report.ElapsedTicks), report.Line, report.Solving);
    }

    /// <summary>What Move Now plays when the worker is stopped: the best move reported so far, else the most central column.</summary>
    public static SearchResult MoveNowResult(IReadOnlyList<int> moves, SearchInfo? last, TimeSpan elapsed)
    {
        var game = new Game();
        foreach (int move in moves)
        {
            game.Play(move);
        }

        int column = last?.BestColumn ?? last?.Column ?? new[] { 3, 2, 4, 1, 5, 0, 6 }.First(game.CanPlay);
        return new SearchResult(
            column,
            last?.Score ?? 0,
            last?.Kind ?? ScoreKind.None,
            last?.Depth ?? 0,
            last?.Nodes ?? 0,
            elapsed,
            last?.PrincipalVariation ?? [column]);
    }
}
