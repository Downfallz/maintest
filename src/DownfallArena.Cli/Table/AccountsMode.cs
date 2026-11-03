namespace DownfallArena.Cli.Table;

/// <summary>Whether players sign in at this host, and how (ADR 0122). <c>--accounts off|google|dev</c>.</summary>
internal enum AccountsMode
{
    /// <summary>No account: seats are tokens and codes, as before ADR 0122.</summary>
    Off,

    /// <summary>Google Identity Services, the ID token checked by the host.</summary>
    Google,

    /// <summary>A name typed with no provider at all, for a laptop and the tests; refused off the loopback address.</summary>
    Development,
}
