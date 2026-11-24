using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using NSubstitute;

namespace DownfallArena.Cli.Tests.Table;

public sealed class SignInProvidersTests : IDisposable
{
    private const string GoogleId = "1234.apps.googleusercontent.com";
    private const string DiscordId = "1311111111111111111";
    private const string MicrosoftId = "6731de76-14a6-49ae-97bc-6eba6914391e";

    private readonly List<HttpClient> _made = [];

    private static ISigningKeySource Keys() => Substitute.For<ISigningKeySource>();

    private HttpClient Http()
    {
        var http = new HttpClient();
        _made.Add(http);
        return http;
    }

    public void Dispose() => _made.ForEach(http => http.Dispose());

    [Fact]
    public void Each_provider_listed_is_built_in_order()
    {
        var (providers, problem) = SignInProviders.Build(["discord", "microsoft", "google"], new SignInClients(GoogleId, DiscordId, MicrosoftId), Keys, Http, TimeProvider.System);

        problem.ShouldBeNull();
        providers.Select(provider => (provider.Name, provider.ClientId)).ShouldBe([("discord", DiscordId), ("microsoft", MicrosoftId), ("google", GoogleId)]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Google_without_its_client_id_is_one_line_and_no_provider(string? clientId)
    {
        var (providers, problem) = SignInProviders.Build(["google"], new SignInClients(clientId, DiscordId), Keys, Http, TimeProvider.System);

        providers.ShouldBeEmpty();
        problem.ShouldNotBeNull().ShouldContain("--google-client-id");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Discord_without_its_application_id_is_one_line_and_no_provider(string? clientId)
    {
        var (providers, problem) = SignInProviders.Build(["google", "discord"], new SignInClients(GoogleId, clientId), Keys, Http, TimeProvider.System);

        providers.ShouldBeEmpty();
        problem.ShouldNotBeNull().ShouldContain("--discord-client-id");
        problem.ShouldContain("DOWNFALL_DISCORD_CLIENT_ID");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Microsoft_without_its_application_id_is_one_line_and_no_provider(string? clientId)
    {
        var (providers, problem) = SignInProviders.Build(["google", "microsoft"], new SignInClients(GoogleId, DiscordId, clientId), Keys, Http, TimeProvider.System);

        providers.ShouldBeEmpty();
        problem.ShouldNotBeNull().ShouldContain("--microsoft-client-id");
        problem.ShouldContain("DOWNFALL_MICROSOFT_CLIENT_ID");
    }

    [Fact]
    public void No_key_source_and_no_client_is_made_for_a_host_that_signs_in_with_neither()
    {
        var made = 0;

        SignInProviders.Build([], new SignInClients(), () => { made++; return Keys(); }, () => { made++; return Http(); }, TimeProvider.System);

        made.ShouldBe(0);
    }

    [Fact]
    public void Discord_and_microsoft_are_names_sign_in_accepts()
    {
        SignInProviders.Known.ShouldBe(["google", "discord", "microsoft"]);
    }
}
