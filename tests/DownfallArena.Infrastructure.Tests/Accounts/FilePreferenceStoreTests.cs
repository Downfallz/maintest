using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FilePreferenceStoreTests : PreferenceStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"preferences-{Guid.CreateVersion7():N}");

    protected override Task<IPreferenceStore> StoreAsync() => Task.FromResult<IPreferenceStore>(new FilePreferenceStore(_root));

    /// <summary>Settings saved before reactions existed (ADR 0173) show them, as every game did then.</summary>
    [Fact]
    public async Task Settings_saved_before_reactions_existed_show_them()
    {
        var account = AccountId.New();
        Directory.CreateDirectory(Path.Combine(_root, "preferences"));
        await File.WriteAllTextAsync(Path.Combine(_root, "preferences", $"{account.Value}.json"), """{ "pace": "auto", "speed": 1, "help": false, "reducedMotion": false }""", TestContext.Current.CancellationToken);

        var read = await new FilePreferenceStore(_root).GetAsync(account, TestContext.Current.CancellationToken);

        read.ShouldBe(new GamePreferences(ReadingPace.Auto, 1m, Help: false, ReducedMotion: false, Reactions: true));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
