using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class InMemoryNoticeStoreTests : NoticeStoreContract
{
    protected override Task<INoticeStore> StoreAsync() => Task.FromResult<INoticeStore>(new InMemoryNoticeStore());
}
