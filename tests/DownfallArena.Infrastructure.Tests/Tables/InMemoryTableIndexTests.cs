using DownfallArena.Application.Tables.Ports;
using DownfallArena.Infrastructure.Tables;

namespace DownfallArena.Infrastructure.Tests.Tables;

/// <summary>The index of a host that keeps nothing: held in its process.</summary>
public sealed class InMemoryTableIndexTests : TableIndexContract
{
    protected override Task<ITableIndex> IndexAsync() => Task.FromResult<ITableIndex>(new InMemoryTableIndex());
}
