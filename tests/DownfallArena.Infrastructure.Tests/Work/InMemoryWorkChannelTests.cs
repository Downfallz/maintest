using DownfallArena.Application.Ports;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Infrastructure.Tests.Work;

/// <summary>The work queues of a host that keeps them in its process.</summary>
public sealed class InMemoryWorkChannelTests : WorkChannelContract
{
    protected override Task<IWorkChannel> ChannelAsync() =>
        new InMemoryWorkChannels(TimeProvider.System).OpenAsync("agent-turns", TestContext.Current.CancellationToken);
}
