using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FileMatchResultsTests : MatchResultsContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"results-{Guid.CreateVersion7():N}");

    protected override Task<IMatchResults> ResultsAsync() => Task.FromResult<IMatchResults>(new FileMatchResults(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
