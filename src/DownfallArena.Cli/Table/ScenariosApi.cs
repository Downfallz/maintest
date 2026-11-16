using DownfallArena.Cli.Studio;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A player's side of the match scenarios (docs/tabletop/scenarios-plan.md, S4, ADR 0179). A signed-in account reads
/// what a scenario is (<c>GET /api/scenarios/&lt;id&gt;</c>), opens its practice from the cut in place of whatever
/// practice it had (<c>POST /api/scenarios/&lt;id&gt;/practice</c>), and finds the one it has again
/// (<c>GET .../practice</c>), which is how a reloaded page resumes it. <c>GET /api/scenarios</c> lists the scenarios the
/// operator listed under Practice (S6); one left off the list still opens by its link. Signed out, every route answers 401.
/// A scenario the repository ships opens for the host's admin accounts alone (<see cref="Admins" />), except its
/// tutorials (ADR 0187, onboarding-plan.md O3a): <c>GET /api/tutorials</c> lists them, in the order of the series, to
/// any signed-in account, and any signed-in account opens one as a practice by the same routes.
/// </summary>
internal sealed class ScenariosApi(ScenarioStore store, ScenarioPractices practices, AccountApi accounts)
{
    /// <summary>
    /// The host's admin accounts (ADR 0172). A scenario the repository ships (S7) is the admin panel's: its id is its
    /// file's name, which anybody can read, so on a host that names admin accounts only they open one, and it does
    /// not exist for anybody else. A host that names none is reached by its operator's token alone, and any account
    /// it signs in opens one. A tutorial is not the panel's: every signed-in account opens it.
    /// </summary>
    public IReadOnlySet<AccountId> Admins { get; init; } = new HashSet<AccountId>();

    public const string Prefix = "/api/scenarios";

    /// <summary>The tutorials, for any signed-in account.</summary>
    public const string TutorialsRoute = "/api/tutorials";

    public static bool Names(string path) => path == Prefix || path == TutorialsRoute || path.StartsWith(Prefix + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, AccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(request);
        if (await accounts.WhoAsync(request) is not { } who)
        {
            return Error("Account.SignInRequired", "Sign in to practise a scenario.", 401);
        }

        if (path is Prefix or TutorialsRoute)
        {
            return Renewed(await ListAsync(method, path), who.Renewed);
        }

        var segments = path[(Prefix.Length + 1)..].Split('/');
        var id = segments[0];
        if (!ScenarioStore.Names(id) || Hidden(id, who.Account.Id))
        {
            return Error("Scenario.NotFound", "This scenario does not exist, or was removed.", 404);
        }

        var account = who.Account.Id.ToString();
        var name = who.Account.Username.Value;
        var answer = (method, segments) switch
        {
            ("GET", [_]) => await DescribeAsync(id),
            ("GET", [_, "practice"]) => await practices.CurrentAsync(account, id) is { } current
                ? Opened(current, name)
                : Error("Scenario.NoPractice", "You have no practice of this scenario open.", 404),
            ("POST", [_, "practice"]) => await OpenAsync(id, account, name),
            _ => NoRoute(method, path),
        };
        return Renewed(answer, who.Renewed);
    }

    /// <summary>The scenarios listed under Practice, or the tutorials: a read alone.</summary>
    private async Task<StudioResponse> ListAsync(string method, string path)
    {
        if (method != "GET")
        {
            return NoRoute(method, path);
        }

        return path == Prefix ? await ListedAsync() : Tutorials();
    }

    /// <summary>Whether a scenario the repository ships, and not a tutorial, is kept from an account that is not an admin's.</summary>
    private bool Hidden(string id, AccountId account) =>
        store.Ships(id) && !store.Teaches(id) && Admins.Count > 0 && !Admins.Contains(account);

    private static StudioResponse NoRoute(string method, string path) => Error("Scenario.NoRoute", $"No such route: {method} {path}", 404);

    /// <summary>The answer, with the account's renewed session cookie when it was renewed.</summary>
    private static StudioResponse Renewed(StudioResponse answer, string? renewed) =>
        renewed is null ? answer : answer with { Headers = [.. answer.Headers ?? [], new KeyValuePair<string, string>("Set-Cookie", renewed)] };

    private async Task<StudioResponse> DescribeAsync(string id) =>
        await store.GetAsync(id) is { } document
            ? StudioResponse.OfJson(Described(id, document), ArtifactJson.LineOptions)
            : Error("Scenario.NotFound", "This scenario does not exist, or was removed.", 404);

    /// <summary>The scenarios listed under Practice, newest first, each as a player reads it.</summary>
    private async Task<StudioResponse> ListedAsync()
    {
        var listed = (await store.ListAsync()).Where(one => one.Listed).Select(one => Described(one.Id, one.Document)).ToArray();
        return StudioResponse.OfJson(new { scenarios = listed }, ArtifactJson.LineOptions);
    }

    /// <summary>The tutorials the repository ships, in the order of the series, each as a player reads it with its place.</summary>
    private StudioResponse Tutorials()
    {
        var series = store.Tutorials;
        return StudioResponse.OfJson(new
        {
            tutorials = series.Select(one =>
            {
                var cut = one.Document.Scenario.Cut;
                return new
                {
                    id = one.Id,
                    order = one.Document.Tutorial!.Order,
                    of = store.Series,
                    title = one.Document.Tutorial.Title,
                    goal = one.Document.Goal,
                    round = cut.Round,
                    seat = ScenarioCommands.SeatName(one.Document.Seat),
                    format = one.Document.Scenario.Source.Rules.Format.ToString(),
                    link = $"/battle?scenario={one.Id}",
                };
            }).ToArray(),
        }, ArtifactJson.LineOptions);
    }

    /// <summary>What a player reads of a scenario: what it is called and asks, where it starts, the seat, and its link.</summary>
    private static object Described(string id, Infrastructure.Matches.Scenarios.ScenarioDocument document)
    {
        var cut = document.Scenario.Cut;
        return new
        {
            id,
            title = document.Title ?? $"Round {cut.Round}",
            goal = document.Goal,
            round = cut.Round,
            seat = ScenarioCommands.SeatName(document.Seat),
            format = document.Scenario.Source.Rules.Format.ToString(),
            link = $"/battle?scenario={id}",
        };
    }

    private async Task<StudioResponse> OpenAsync(string id, string account, string name)
    {
        var (opened, refused) = await practices.OpenAsync(id, account);
        return opened is not null ? Opened(opened, name) : Error(refused!.Error, refused.Message, refused.Status);
    }

    /// <summary>
    /// The practice's seat as the battle view opens a seat: its token and a table to name it by, the person against the
    /// recorded opponent, and what the practice is.
    /// </summary>
    private static StudioResponse Opened(ScenarioPractices.Practice practice, string name)
    {
        var play = practice.Play;
        var seat = ScenarioCommands.SeatName(play.Seat);
        var other = seat == "player1" ? "player2" : "player1";
        return StudioResponse.OfJson(new
        {
            seat,
            token = play.Token,
            table = new
            {
                // The playthrough's own: the page keeps what it showed of a game under its table's id, and a restart
                // is a new game whose rounds have not been shown yet; a resumed one is the same game.
                id = $"practice-{play.Session.MatchId}",
                format = play.Session.Rules.Format.ToString(),
                seats = new[] { new { seat, name, kind = "person" }, new { seat = other, name = "Recorded opponent", kind = "bot" } },
            },
            practice = play.Api.Practice,
        }, ArtifactJson.LineOptions);
    }

    private static StudioResponse Error(string error, string message, int status) =>
        StudioResponse.OfJson(new { error, message }, ArtifactJson.LineOptions, status);
}
