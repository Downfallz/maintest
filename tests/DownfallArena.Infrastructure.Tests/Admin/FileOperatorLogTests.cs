using DownfallArena.Application.Admin;
using DownfallArena.Application.Admin.Ports;
using DownfallArena.Infrastructure.Admin;

namespace DownfallArena.Infrastructure.Tests.Admin;

public sealed class FileOperatorLogTests : OperatorLogContract, IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"operator-log-{Guid.CreateVersion7():N}");

    protected override Task<IOperatorLog> LogAsync() => Task.FromResult<IOperatorLog>(new FileOperatorLog(_directory));

    /// <summary>The last thousand are kept: the oldest go as new actions come (the owner's choice, 2026-11-14).</summary>
    [Fact]
    public async Task The_oldest_actions_go_past_the_last_thousand()
    {
        var log = new FileOperatorLog(_directory);
        var start = new DateTimeOffset(2026, 11, 14, 3, 0, 0, TimeSpan.Zero);
        var first = new OperatorAction(OperatorAction.NewId(start), start, "mark", OperatorActions.OpenTable, "t-0", null);
        await log.RecordAsync(first, Cancel);
        for (var index = 1; index <= OperatorAction.Kept; index++)
        {
            var at = start.AddSeconds(index);
            await log.RecordAsync(new OperatorAction(OperatorAction.NewId(at), at, "mark", OperatorActions.OpenTable, $"t-{index}", null), Cancel);
        }

        var ids = await log.IdsAsync(Cancel);

        ids.Count.ShouldBe(OperatorAction.Kept);
        ids.ShouldNotContain(first.Id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
