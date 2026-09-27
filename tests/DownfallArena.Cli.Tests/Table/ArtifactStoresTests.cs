using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests.Table;

public sealed class ArtifactStoresTests
{
    [Fact]
    public async Task A_directory_is_a_file_store_under_it()
    {
        var store = await ArtifactStores.OpenAsync(Path.Combine(Path.GetTempPath(), "downfall-runs"), TestContext.Current.CancellationToken);

        store.ShouldBeOfType<FileArtifactStore>().RootDirectory.ShouldBe(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "downfall-runs")));
    }

    [Theory]
    [InlineData("https://downfall.blob.core.windows.net/playtests", true)]
    [InlineData("http://127.0.0.1:10000/devstoreaccount1/playtests", true)]
    [InlineData("runs/playtest", false)]
    [InlineData("C:\\runs\\playtest", false)]
    public void A_url_names_a_container_and_anything_else_a_directory(string target, bool container)
    {
        ArtifactStores.IsContainerUrl(target).ShouldBe(container);
    }

    /// <summary>The emulator's well-known account is the one URL a published key belongs to; a real account never is.</summary>
    [Theory]
    [InlineData("http://127.0.0.1:10000/devstoreaccount1/playtests", true)]
    [InlineData("http://localhost:10000/devstoreaccount1/playtests", true)]
    [InlineData("https://downfall.blob.core.windows.net/playtests", false)]
    [InlineData("https://devstoreaccount1.blob.core.windows.net/playtests", false)]
    [InlineData("http://127.0.0.1:10000/other/playtests", false)]
    public void Only_the_development_account_on_this_machine_is_the_emulator(string url, bool emulator)
    {
        ArtifactStores.IsEmulator(new Uri(url)).ShouldBe(emulator);
    }
}
