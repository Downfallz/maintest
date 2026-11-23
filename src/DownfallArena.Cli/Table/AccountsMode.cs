namespace DownfallArena.Cli.Table;

/// <summary>
/// Whether players sign in at this host, and how (ADR 0122): <c>--sign-in google</c> (or its older spelling
/// <c>--accounts google</c>), <c>--accounts dev</c>, or <c>--accounts off</c>, the default.
/// </summary>
internal enum AccountsMode
{
    /// <summary>No account: seats are tokens and codes, as before ADR 0122.</summary>
    Off,

    /// <summary>The providers <c>--sign-in</c> lists, each checked by the host behind one port (sign-in methods plan, P2).</summary>
    SignIn,

    /// <summary>A name typed with no provider at all, for a laptop and the tests; refused off the loopback address.</summary>
    Development,
}
