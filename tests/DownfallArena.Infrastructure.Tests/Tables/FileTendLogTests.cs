using DownfallArena.Application.Tables.Ports;
using DownfallArena.Infrastructure.Tables;

namespace DownfallArena.Infrastructure.Tests.Tables;

public sealed class FileTendLogTests : TendLogContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tend-{Guid.CreateVersion7():N}");

    protected override Task<ITendLog> LogAsync() => Task.FromResult<ITendLog>(new FileTendLog(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
