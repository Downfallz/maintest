using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The providers this release can sign players in with (sign-in methods plan, P2), and how each is built from what
/// the host was given. A provider joins <see cref="Known" /> and <see cref="Build" /> in the same change, with its
/// client id's flag; the page draws a button for each one <c>/api/me</c> lists.
/// </summary>
internal static class SignInProviders
{
    /// <summary>The names <c>--sign-in</c> accepts.</summary>
    public static readonly IReadOnlyList<string> Known = [ExternalIdentity.Google];

    /// <summary>
    /// The providers <paramref name="names" /> lists, in order, or the one line that says what is missing: Google needs
    /// its client id and the keys its tokens are checked against.
    /// </summary>
    public static (IReadOnlyList<ISignInProvider> Providers, string? Problem) Build(IReadOnlyList<string> names, string? googleClientId, Func<ISigningKeySource> googleKeys, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(googleKeys);
        var built = new List<ISignInProvider>();
        foreach (var name in names)
        {
            switch (name)
            {
                case ExternalIdentity.Google when string.IsNullOrWhiteSpace(googleClientId):
                    return ([], $"'--sign-in google' needs the Google client id: --google-client-id <id>, or {TableHost.GoogleClientIdEnvironment}.");
                case ExternalIdentity.Google:
                    built.Add(new GoogleIdTokenVerifier(googleClientId, googleKeys(), clock));
                    break;
                default:
                    return ([], $"'--sign-in' does not know '{name}'.");
            }
        }

        return (built, null);
    }
}
