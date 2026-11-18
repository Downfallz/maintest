using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary><c>scenario-cut</c> keeps the operator's rule (#736, #833): a session still played is not cut.</summary>
public sealed class ScenarioCutCommandTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly string _output = Path.Combine(Path.GetTempPath(), $"scenario-cut-{Guid.CreateVersion7():N}.json");

    public void Dispose()
    {
        _hosted.Dispose();
        File.Delete(_output);
    }

    [Fact]
    public async Task A_session_still_played_is_not_cut_from_the_console()
    {
        using var registry = _hosted.Registry();
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        using (var place = (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, TestContext.Current.CancellationToken);
        }

        await table.RecordAsync(TestContext.Current.CancellationToken);

        var exit = await ScenarioCommands.CutAsync(new CliOptions { Command = "scenario-cut", Session = Path.Combine(_hosted.RunsDirectory, table.Id), Round = 1, Output = _output });

        exit.ShouldBe(1);
        File.Exists(_output).ShouldBeFalse();
    }
}
