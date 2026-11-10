using DownfallArena.Application.Tables.Ports;
using DownfallArena.Infrastructure.Tables;

namespace DownfallArena.Infrastructure.Tests.Tables;

/// <summary>The index of a host on a directory store: files under the directory it is given.</summary>
public sealed class FileTableIndexTests : TableIndexContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"downfall-index-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    protected override Task<ITableIndex> IndexAsync() => Task.FromResult<ITableIndex>(new FileTableIndex(_root));

    [Theory]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("a.b")]
    public async Task A_key_that_could_leave_the_index_is_refused(string key)
    {
        var index = new FileTableIndex(_root);

        await Should.ThrowAsync<ArgumentException>(() => index.CodeAsync(key, TestContext.Current.CancellationToken));
    }
}
