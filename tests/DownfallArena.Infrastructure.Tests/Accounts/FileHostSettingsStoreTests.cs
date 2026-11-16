using DownfallArena.Application.Hosting;
using DownfallArena.Application.Hosting.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FileHostSettingsStoreTests : HostSettingsStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"host-settings-{Guid.CreateVersion7():N}");

    protected override Task<IHostSettingsStore> StoreAsync() => Task.FromResult<IHostSettingsStore>(new FileHostSettingsStore(_root));

    /// <summary>A document that does not say whether registration is open leaves it open, as a host that never closed it.</summary>
    [Fact]
    public async Task A_document_that_does_not_say_leaves_registration_open()
    {
        Directory.CreateDirectory(Path.Combine(_root, "settings"));
        await File.WriteAllTextAsync(Path.Combine(_root, "settings", "host.json"), """{ "notice": "  " }""", Cancel);

        var read = await new FileHostSettingsStore(_root).GetAsync(Cancel);

        read.ShouldBe(HostSettings.Default);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
