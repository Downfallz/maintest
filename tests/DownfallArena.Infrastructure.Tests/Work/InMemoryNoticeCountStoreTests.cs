using DownfallArena.Application.Ports;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Infrastructure.Tests.Work;

public sealed class InMemoryNoticeCountStoreTests : NoticeCountStoreContract
{
    protected override Task<INoticeCountStore> StoreAsync() => Task.FromResult<INoticeCountStore>(new InMemoryNoticeCountStore());
}
