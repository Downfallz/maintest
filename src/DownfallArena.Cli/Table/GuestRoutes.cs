namespace DownfallArena.Cli.Table;

/// <summary>
/// The routes a guest may call (ADR 0207): its own account, the sign-ins that upgrade it, its game settings and the
/// tutorials. The host asks this of every API request a guest's session carries, before the route that answers it, and
/// every route not listed answers <c>403 Account.GuestNotAllowed</c>: a route added later is closed to guests until it
/// is listed here. A tutorial's own seat, found by the practice's token, is the host's to allow (<see cref="TableServer" />).
/// </summary>
internal static class GuestRoutes
{
    private const string Account = AccountApi.Prefix;
    private const string Tutorials = ScenariosApi.TutorialsRoute;
    private const string Scenarios = ScenariosApi.Prefix;

    /// <summary>Whether a guest may call <paramref name="method" /> on <paramref name="path" />; <paramref name="teaches" /> says whether a scenario id is a tutorial.</summary>
    public static bool Allows(string method, string path, Func<string, bool> teaches)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(teaches);
        return (method, path) switch
        {
            ("GET", AdminApi.MePath) => true,
            ("GET" or "POST" or "DELETE", Account) => true,
            ("POST", Account + "/signout") => true,
            ("GET" or "PUT", Account + "/preferences") => true,
            ("GET", Tutorials) => true,
            ("GET", _) when path.StartsWith(Account + "/names/", StringComparison.Ordinal) => true,
            ("POST", _) when Segments(path, Account) is [var provider] && provider.All(char.IsAsciiLetterLower) => true,
            ("POST", _) when Segments(path, Tutorials) is [var id, "skip" or "done"] && teaches(id) => true,
            ("GET", _) when Segments(path, Scenarios) is [var id] && teaches(id) => true,
            ("GET" or "POST" or "DELETE", _) when Segments(path, Scenarios) is [var id, "practice"] && teaches(id) => true,
            _ => false,
        };
    }

    // The path's segments after the prefix and its slash; none when the path is not under it.
    private static string[] Segments(string path, string prefix) =>
        path.StartsWith(prefix + "/", StringComparison.Ordinal) ? path[(prefix.Length + 1)..].Split('/') : [];
}
