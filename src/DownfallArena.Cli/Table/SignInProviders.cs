using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The providers this release can sign players in with (sign-in methods plan, P2 and P3), and how each is built from
/// what the host was given. A provider joins <see cref="Known" /> and <see cref="Build" /> in the same change, with its
/// client id's flag; the page draws a button for each one <c>/api/me</c> lists.
/// </summary>
internal static class SignInProviders
{
    /// <summary>The names <c>--sign-in</c> accepts.</summary>
    public static readonly IReadOnlyList<string> Known = [ExternalIdentity.Google, ExternalIdentity.Discord];

    /// <summary>How long the host waits for a provider's answer before the sign-in is refused.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The providers <paramref name="names" /> lists, in order, or the one line that says what is missing: each needs its
    /// client id, and Google the keys its tokens are checked against. <paramref name="http" /> makes the client a
    /// provider that calls its own API talks through, made only for one listed.
    /// </summary>
    public static (IReadOnlyList<ISignInProvider> Providers, string? Problem) Build(IReadOnlyList<string> names, SignInClients clients, Func<ISigningKeySource> googleKeys, Func<HttpClient> http, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(googleKeys);
        ArgumentNullException.ThrowIfNull(http);
        var built = new List<ISignInProvider>();
        foreach (var name in names)
        {
            switch (name)
            {
                case ExternalIdentity.Google when string.IsNullOrWhiteSpace(clients.Google):
                    return ([], $"'--sign-in google' needs the Google client id: --google-client-id <id>, or {TableHost.GoogleClientIdEnvironment}.");
                case ExternalIdentity.Google:
                    built.Add(new GoogleIdTokenVerifier(clients.Google, googleKeys(), clock));
                    break;
                case ExternalIdentity.Discord when string.IsNullOrWhiteSpace(clients.Discord):
                    return ([], $"'--sign-in discord' needs the Discord application id: --discord-client-id <id>, or {TableHost.DiscordClientIdEnvironment}.");
                case ExternalIdentity.Discord:
                    built.Add(new DiscordSignIn(clients.Discord, http()));
                    break;
                default:
                    return ([], $"'--sign-in' does not know '{name}'.");
            }
        }

        return (built, null);
    }
}
