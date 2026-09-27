using System.Text.Json;
using Connect4.App.Models;
using Connect4.App.Services;
using Connect4.App.ViewModels;
using Connect4.Engine;

namespace Connect4.App.Tests;

public sealed class ViewModelTests
{
    [Theory]
    [InlineData("", 25, "+25")]   // Red to move, good for Red
    [InlineData("4", 25, "-25")]  // Yellow to move, good for Yellow
    [InlineData("", 0, "0")]
    public void FormatScore_HeuristicIsFromRedsView(string moves, int score, string expected)
    {
        Assert.Equal(expected, AnalysisViewModel.FormatScore(score, ScoreKind.Heuristic, Position.FromMoves(moves)));
    }

    [Fact]
    public void FormatScore_WinCountsTheWinnersMoves()
    {
        // Red to move after 6 moves wins with move 7: one Red move.
        Position position = Position.FromMoves("121212");

        Assert.Equal("Red wins in 1 move", AnalysisViewModel.FormatScore(Scores.Win - 7, ScoreKind.Exact, position));
        Assert.Equal("Red wins in 2 moves", AnalysisViewModel.FormatScore(Scores.Win - 9, ScoreKind.Exact, position));
        Assert.Equal("Yellow wins in 2 moves", AnalysisViewModel.FormatScore(-(Scores.Win - 10), ScoreKind.Exact, position));
    }

    [Fact]
    public void FormatScore_DrawAndNone()
    {
        Assert.Equal("Draw", AnalysisViewModel.FormatScore(0, ScoreKind.Exact, Position.Empty));
        Assert.Equal("", AnalysisViewModel.FormatScore(0, ScoreKind.None, Position.Empty));
    }

    [Fact]
    public void Analysis_ShowsColumnsOneBased()
    {
        var analysis = new AnalysisViewModel();
        var result = new SearchResult(3, 12, ScoreKind.Heuristic, 9, 1234, TimeSpan.FromSeconds(1.5), [3, 2, 4]);

        analysis.Update(result, Position.Empty, TimeSpan.FromSeconds(1.5));

        Assert.Equal("4", analysis.BestMove);
        Assert.Equal("435", analysis.Line);
        Assert.Equal("9 plies", analysis.Depth);
        Assert.Equal("+12", analysis.Value);
    }

    [Fact]
    public void Analysis_ShowsTheSolver()
    {
        var analysis = new AnalysisViewModel();
        var info = new SearchInfo(20, 3, 3, 0, ScoreKind.Heuristic, 10, TimeSpan.Zero, [3], Solving: true);

        analysis.Update(info, Position.Empty);

        Assert.Equal("solving (20 empty)", analysis.Depth);
    }

    [Fact]
    public void GameSettings_Normalize_ClampsEveryValue()
    {
        GameSettings settings = new GameSettings(TimeControlMode.Solve, 0, 99, -1, 50).Normalize();

        Assert.Equal(new GameSettings(GameSettings.Default.Mode, 1, 60, 1, 42), settings);
    }

    [Fact]
    public void GameSettings_ToLimits_PassesTheEndgameThreshold()
    {
        SearchLimits limits = new GameSettings(TimeControlMode.TimePerGame, 8, 5, 5, 12).ToLimits(TimeSpan.FromMinutes(2));

        Assert.Equal(TimeControlMode.TimePerGame, limits.Mode);
        Assert.Equal(TimeSpan.FromMinutes(2), limits.Time);
        Assert.Equal(12, limits.EndgameThreshold);
    }

    [Fact]
    public void SettingsViewModel_RoundTrips()
    {
        var settings = new GameSettings(TimeControlMode.FixedDepth, 12, 7, 9, 30);
        var vm = new SettingsViewModel(settings);

        Assert.True(vm.IsFixedDepth);
        vm.IsTimePerGame = true;

        Assert.Equal(settings with { Mode = TimeControlMode.TimePerGame }, vm.ToSettings());
    }

    [Fact]
    public void AppSettings_JsonRoundTrips()
    {
        var settings = new AppSettings(
            new GameSettings(TimeControlMode.FixedDepth, 12, 7, 9, 30),
            GameMode.HumanVsHuman,
            ShowAnalysis: false,
            SoundOn: false,
            new WindowPlacement(10, 20, 800, 600, Maximized: true));

        string json = JsonSerializer.Serialize(settings, AppSettingsJson.Default.AppSettings);

        Assert.Contains("\"HumanVsHuman\"", json);
        Assert.Equal(settings, JsonSerializer.Deserialize(json, AppSettingsJson.Default.AppSettings));
    }

    [Fact]
    public void AppSettings_MissingEndgameThreshold_GetsTheDefault()
    {
        const string json = """
            { "Game": { "Mode": "TimePerMove", "Depth": 8, "SecondsPerMove": 5, "MinutesPerGame": 5 },
              "Mode": "HumanVsComputer", "ShowAnalysis": true, "SoundOn": true, "Window": null }
            """;

        AppSettings? settings = JsonSerializer.Deserialize(json, AppSettingsJson.Default.AppSettings);

        Assert.Equal(SearchLimits.DefaultEndgameThreshold, settings!.Game.EndgameThreshold);
    }

    [Theory]
    [InlineData(Sound.Drop)]
    [InlineData(Sound.Win)]
    [InlineData(Sound.Loss)]
    [InlineData(Sound.Draw)]
    public void SoundWaves_AreValidWavFiles(Sound sound)
    {
        byte[] wav = SoundWaves.Create(sound);

        Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
        Assert.Equal("WAVE"u8.ToArray(), wav[8..12]);
        Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
        Assert.Equal(wav.Length - 44, BitConverter.ToInt32(wav, 40));
        Assert.True(wav.Length > 1000);
        Assert.Same(wav, SoundWaves.Create(sound));
    }

    [Fact]
    public async Task LocalEngineHost_SearchesTheGivenMoves()
    {
        var host = new LocalEngineHost(new ComputerPlayer(new SearchEngine(hashLogSize: 16, endgameLogSize: 17)));

        SearchResult result = await host.ChooseMoveAsync([0, 1, 0, 1, 0, 1], SearchLimits.FixedDepth(4), null, default, default);

        Assert.Equal(0, result.Column);
    }
}
