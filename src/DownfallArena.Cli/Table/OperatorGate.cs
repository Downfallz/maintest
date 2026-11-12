using System.Security.Cryptography;
using System.Text;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Who may open a table: the operator, and how a request proves it is theirs (ADR 0081).
/// </summary>
/// <remarks>
/// <para>
/// Two ways, and a host is started with one of them. On a laptop the operator is at the console, and the
/// console prints a token, the way it prints the pilot's: the admin panel is opened with it and every one of
/// its requests carries it. In the container there is no console anybody reads, and the platform in front of the
/// host signs the operator in (Microsoft Entra ID, the owner's tenant, ADR 0080) and stamps every request it
/// forwards with who they are, in <c>X-MS-CLIENT-PRINCIPAL-NAME</c>. That header is trusted only when the
/// host is told the platform is there (<c>--platform-auth</c>): read on a laptop, it is a header anybody can
/// type.
/// </para>
/// <para>
/// The players are never asked for either. They sign in with the host's own session (ADR 0122, Google, beside
/// this door rather than through it), or join a seat by its code with no account at all; the platform is configured to
/// let an anonymous request through, and only the admin panel looks for the stamp.
/// </para>
/// <para>
/// A third door beside either (ADR 0172): the accounts the host is told are its admins, by their ids
/// (<c>--admins</c>, <c>DOWNFALL_ADMINS</c>). A player signed in as one of them is the operator, by their player
/// name, through the same sign-in that seats them, so the owner reaches the panel from the home page's menu
/// without a second sign-in. An id and not a name: a name a deleted account lets go of can be taken by anybody.
/// </para>
/// </remarks>
internal sealed class OperatorGate
{
    /// <summary>The header the platform's authentication stamps a signed-in request with.</summary>
    public const string PrincipalHeader = "X-MS-CLIENT-PRINCIPAL-NAME";

    /// <summary>Where the platform signs somebody in, and sends them back to the admin panel afterwards.</summary>
    public const string Login = "/.auth/login/aad?post_login_redirect_uri=/admin";

    private OperatorGate(string? token, IReadOnlySet<AccountId>? admins = null)
    {
        Token = token;
        Admins = admins ?? new HashSet<AccountId>();
    }

    /// <summary>The operator's token when the console is the door, or <c>null</c> when the platform is.</summary>
    public string? Token { get; }

    public bool TrustsPlatform => Token is null;

    /// <summary>The accounts that are the operator when signed in, whatever the other door is.</summary>
    public IReadOnlySet<AccountId> Admins { get; }

    /// <summary>The same door, and the accounts that are the operator too (ADR 0172).</summary>
    public OperatorGate WithAdmins(IEnumerable<AccountId> admins)
    {
        ArgumentNullException.ThrowIfNull(admins);
        return new OperatorGate(Token, admins.ToHashSet());
    }

    /// <summary>The operator is whoever holds the token the console printed.</summary>
    public static OperatorGate WithToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new OperatorGate(token);
    }

    /// <summary>The operator is whoever the platform in front of the host signed in.</summary>
    public static OperatorGate BehindPlatform() => new(token: null);

    /// <summary>
    /// Who the operator making this request is, by the name a record and a console call them, or <c>null</c>
    /// when the request is nobody's. The token is compared in fixed time, because a token is guessed one
    /// character at a time or not at all. A signed-in admin account is the operator by its player name.
    /// </summary>
    public string? Admit(string? token, string? principal, Account? account = null)
    {
        if (account is not null && Admins.Contains(account.Id))
        {
            return account.Username.Value;
        }

        if (Token is { } expected)
        {
            return !string.IsNullOrWhiteSpace(token)
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expected))
                ? "operator"
                : null;
        }

        return string.IsNullOrWhiteSpace(principal) ? null : principal.Trim();
    }

    /// <summary>
    /// The admin accounts a setting names, comma-separated, or the problem with it: an id that is not one is a
    /// typo the host refuses to start on, rather than an owner who silently is not the operator.
    /// </summary>
    public static (IReadOnlyList<AccountId> Admins, string? Problem) ParseAdmins(string? text)
    {
        var admins = new List<AccountId>();
        foreach (var one in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!AccountId.TryParse(one, out var id))
            {
                return ([], $"'{one}' is not an account id: an admin is named by the id /api/me gives its account (acct_ and sixteen characters).");
            }

            admins.Add(id);
        }

        return (admins, null);
    }
}
