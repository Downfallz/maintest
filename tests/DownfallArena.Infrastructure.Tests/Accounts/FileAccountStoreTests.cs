using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FileAccountStoreTests : AccountStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"accounts-{Guid.CreateVersion7():N}");

    protected override Task<IAccountStore> StoreAsync() => Task.FromResult<IAccountStore>(new FileAccountStore(_root));

    [Fact]
    public async Task Nothing_but_the_records_is_left_in_the_directory()
    {
        var store = await StoreAsync();
        await store.TryBindSubjectAsync(new("google", "1"), SharedKernel.Identifiers.AccountId.New(), Cancel);
        await store.TryBindSubjectAsync(new("google", "1"), SharedKernel.Identifiers.AccountId.New(), Cancel);

        Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_account_written_before_it_could_hold_several_identities_reads_as_one()
    {
        var store = await StoreAsync();
        var id = SharedKernel.Identifiers.AccountId.New();
        Directory.CreateDirectory(Path.Combine(_root, "accounts"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "accounts", $"{id.Value}.json"),
            $$"""{ "id": "{{id.Value}}", "provider": "google", "subject": "1098", "username": "MarkF", "createdAt": "2026-11-03T20:14:00+00:00", "generation": 2 }""",
            Cancel);

        var read = (await store.FindAsync(id, Cancel)).ShouldNotBeNull();

        read.Identities.All.ShouldBe([new Application.Accounts.ExternalIdentity("google", "1098")]);
        read.Username.Value.ShouldBe("MarkF");
        read.Generation.ShouldBe(2);
    }

    [Fact]
    public async Task An_account_is_written_so_the_image_before_several_identities_still_reads_its_first()
    {
        var store = await StoreAsync();
        var first = new Application.Accounts.ExternalIdentity("google", "1098");
        var account = new Application.Accounts.Account(
            SharedKernel.Identifiers.AccountId.New(),
            Application.Accounts.AccountIdentities.Of(first).With(new("discord", "8035")),
            Application.Accounts.Username.Create("MarkF").Value,
            DateTimeOffset.UnixEpoch,
            1);
        await store.SaveAsync(account, Cancel);

        using var written = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_root, "accounts", $"{account.Id.Value}.json"), Cancel));

        written.RootElement.GetProperty("provider").GetString().ShouldBe("google");
        written.RootElement.GetProperty("subject").GetString().ShouldBe("1098");
        written.RootElement.GetProperty("identities").GetArrayLength().ShouldBe(2);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
