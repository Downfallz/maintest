using System.Security.Cryptography;
using System.Text;

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
/// The players are never asked for either. A seat is joined by its code, without an account, because the
/// people who sit down at a table are not the people who have one (ADR 0080); the platform is configured to
/// let an anonymous request through, and only the admin panel looks for the stamp.
/// </para>
/// </remarks>
internal sealed class OperatorGate
{
    /// <summary>The header the platform's authentication stamps a signed-in request with.</summary>
    public const string PrincipalHeader = "X-MS-CLIENT-PRINCIPAL-NAME";

    /// <summary>Where the platform signs somebody in, and sends them back to the admin panel afterwards.</summary>
    public const string Login = "/.auth/login/aad?post_login_redirect_uri=/admin";

    private OperatorGate(string? token)
    {
        Token = token;
    }

    /// <summary>The operator's token when the console is the door, or <c>null</c> when the platform is.</summary>
    public string? Token { get; }

    public bool TrustsPlatform => Token is null;

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
    /// character at a time or not at all.
    /// </summary>
    public string? Admit(string? token, string? principal)
    {
        if (Token is { } expected)
        {
            return !string.IsNullOrWhiteSpace(token)
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expected))
                ? "operator"
                : null;
        }

        return string.IsNullOrWhiteSpace(principal) ? null : principal.Trim();
    }
}
