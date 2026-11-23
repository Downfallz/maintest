using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using NSubstitute;

namespace DownfallArena.Cli.Tests.Table;

public sealed class SignInProvidersTests
{
    private static ISigningKeySource Keys() => Substitute.For<ISigningKeySource>();

    [Fact]
    public void Each_provider_listed_is_built_in_order()
    {
        var (providers, problem) = SignInProviders.Build(["google"], "1234.apps.googleusercontent.com", Keys, TimeProvider.System);

        problem.ShouldBeNull();
        providers.Select(provider => (provider.Name, provider.ClientId)).ShouldBe([("google", "1234.apps.googleusercontent.com")]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Google_without_its_client_id_is_one_line_and_no_provider(string? clientId)
    {
        var (providers, problem) = SignInProviders.Build(["google"], clientId, Keys, TimeProvider.System);

        providers.ShouldBeEmpty();
        problem.ShouldNotBeNull().ShouldContain("--google-client-id");
    }

    [Fact]
    public void No_key_source_is_made_for_a_host_that_does_not_sign_in_with_google()
    {
        var made = 0;

        SignInProviders.Build([], null, () => { made++; return Keys(); }, TimeProvider.System);

        made.ShouldBe(0);
    }
}
