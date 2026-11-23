using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Hosting;
using DownfallArena.Cli.Studio;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A player's side of the match scenarios (docs/tabletop/scenarios-plan.md, S4, ADR 0179). A signed-in account reads
/// what a scenario is (<c>GET /api/scenarios/&lt;id&gt;</c>), opens its practice from the cut in place of whatever
/// practice it had (<c>POST /api/scenarios/&lt;id&gt;/practice</c>), finds the one it has again
/// (<c>GET .../practice</c>), which is how a reloaded page resumes it, and lets go of it (<c>DELETE .../practice</c>),
/// which is what leaving a tutorial does. A tutorial that is done is not resumed: it is let go of, and opens from its
/// cut the next time. <c>GET /api/scenarios</c> lists the scenarios the
/// operator listed under Practice (S6); one left off the list still opens by its link. Signed out, every route answers 401.
/// A scenario the repository ships opens for the host's admin accounts alone (<see cref="Admins" />), except its
/// tutorials (ADR 0187, onboarding-plan.md O3a): <c>GET /api/tutorials</c> lists them, in the order of the series, to
/// any signed-in account, and any signed-in account opens one as a practice by the same routes. The list says where
/// the account stands with each (new, done, skipped) and which one opens by itself (O4): the first of the series
/// while it is new. <c>POST /api/tutorials/&lt;id&gt;/skip</c> keeps that the account skipped one, and
/// <c>POST /api/tutorials/&lt;id&gt;/done</c> that it finished one, once this host's own practice of it says so.
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

    /// <summary>
    /// The host's recorded sessions, which say whether the game a scenario was cut from is still played (#833); none on
    /// a host that records nothing, which has no such game.
    /// </summary>
    public StoredSessions? Stored { get; init; }

    /// <summary>Where its console lines go: the host's console unless a test reads them.</summary>
    public Action<string> Said { get; init; } = Console.WriteLine;

    public const string Prefix = "/api/scenarios";

    /// <summary>The tutorials, for any signed-in account.</summary>
    public const string TutorialsRoute = "/api/tutorials";

    public static bool Names(string path) =>
        path == Prefix || path == TutorialsRoute || path.StartsWith(Prefix + "/", StringComparison.Ordinal) || path.StartsWith(TutorialsRoute + "/", StringComparison.Ordinal);

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
            return Renewed(await ListAsync(method, path, who.Account.Id), who.Renewed);
        }

        if (path.StartsWith(TutorialsRoute + "/", StringComparison.Ordinal))
        {
            return Renewed(await NoteAsync(method, path, who.Account.Id), who.Renewed);
        }

        var segments = path[(Prefix.Length + 1)..].Split('/');
        var id = segments[0];
        if (!ScenarioStore.Names(id) || Hidden(id, who.Account.Id))
        {
            return Error("Scenario.NotFound", "This scenario does not exist, or was removed.", 404);
        }

        // A practice follows the source's plan: not opened or resumed while the game it was cut from is played (#833).
        if (method is "GET" or "POST" && segments is [_, "practice"])
        {
            DomainError? heldBack;
            try
            {
                heldBack = await HeldBackAsync(id);
            }
            catch (Exception failure) when (TableRegistry.StoreFails(failure))
            {
                Said($"  Whether a scenario's source game is over could not be read: {HttpHost.OneLine(failure)}");
                heldBack = ScenarioSources.Unavailable;
            }

            if (heldBack is not null)
            {
                return Renewed(Error(heldBack.Code, heldBack.Message, heldBack == ScenarioSources.Unavailable ? 503 : 409), who.Renewed);
            }
        }

        var name = who.Account.Username.Value;
        var answer = (method, segments) switch
        {
            ("GET", [_]) => await DescribeAsync(id),
            ("GET", [_, "practice"]) => await ResumeAsync(who.Account.Id, id, name),
            ("POST", [_, "practice"]) => await OpenAsync(id, who.Account.Id, name),
            ("DELETE", [_, "practice"]) => await ForgetAsync(who.Account.Id, id),
            _ => NoRoute(method, path),
        };
        return Renewed(answer, who.Renewed);
    }

    /// <summary>The scenarios listed under Practice, or the tutorials: a read alone.</summary>
    private async Task<StudioResponse> ListAsync(string method, string path, AccountId account)
    {
        if (method != "GET")
        {
            return NoRoute(method, path);
        }

        return path == Prefix ? await ListedAsync() : Tutorials(await accounts.TutorialsAsync(account));
    }

    /// <summary>
    /// Keeps that the account skipped a Tutorial, or finished it. Skipped is the person's to say. Done is this
    /// host's: it is kept only while the account's practice of that Tutorial, in this process, says it is done.
    /// </summary>
    private async Task<StudioResponse> NoteAsync(string method, string path, AccountId account)
    {
        var segments = path[(TutorialsRoute.Length + 1)..].Split('/');
        if (method != "POST" || segments is not [var id, "skip" or "done"])
        {
            return NoRoute(method, path);
        }

        if (!store.Teaches(id))
        {
            return Error("Tutorial.NotFound", "This tutorial does not exist.", 404);
        }

        var state = segments[1] == "done" ? TutorialState.Done : TutorialState.Skipped;
        if (state == TutorialState.Done && (await practices.CurrentAsync(account.ToString(), id, KeepDoneAsync))?.Play.Api.Practice?.Tutorial is not { Done: true })
        {
            return Error("Tutorial.NotDone", "This tutorial is not finished yet.", 409);
        }

        return await accounts.NoteTutorialAsync(account, id, state) is { } kept
            ? StudioResponse.OfJson(new { id, state = Named(kept.StateOf(id)) }, ArtifactJson.LineOptions)
            : Error("Account.NoPreferences", "This host keeps no game settings.", 404);
    }

    private static string Named(TutorialState state) => state switch
    {
        TutorialState.Done => "done",
        TutorialState.Skipped => "skipped",
        _ => "new",
    };

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

    /// <summary>
    /// The scenarios listed under Practice, newest first, each as a player reads it. One listed before #833 whose game
    /// is still played is left off until it is over: it would not open.
    /// </summary>
    private async Task<StudioResponse> ListedAsync()
    {
        var listed = new List<object>();
        var unread = 0;
        string? why = null;
        foreach (var one in (await store.ListAsync()).Where(one => one.Listed))
        {
            try
            {
                if (await HeldBackAsync(one.Document) is null)
                {
                    listed.Add(Described(one.Id, one.Document));
                }
            }
            catch (Exception failure) when (TableRegistry.StoreFails(failure))
            {
                // Left off, as a scenario held back is: nothing shows its game is over. Said once for the list.
                unread++;
                why ??= HttpHost.OneLine(failure);
            }
        }

        if (unread > 0)
        {
            Said($"  {unread} listed {(unread == 1 ? "scenario was" : "scenarios were")} left off Practice: whether their source game is over could not be read ({why}).");
        }

        return StudioResponse.OfJson(new { scenarios = listed }, ArtifactJson.LineOptions);
    }

    /// <summary>Why the scenario kept under this id is held back (<see cref="ScenarioSources" />), or none.</summary>
    private async Task<DomainError?> HeldBackAsync(string id) =>
        Stored is not null && await store.GetAsync(id) is { } document ? await HeldBackAsync(document) : null;

    private Task<DomainError?> HeldBackAsync(Infrastructure.Matches.Scenarios.ScenarioDocument document) =>
        Stored is { } stored ? ScenarioSources.HeldBackAsync(stored, document) : Task.FromResult<DomainError?>(null);

    /// <summary>
    /// The tutorials the repository ships, in the order of the series, each as a player reads it with its place and
    /// where the account stands with it. <c>opens</c> is the one that opens by itself: the first of the series while
    /// it is new to the account, and none on a host that keeps no game settings, which could not keep it skipped.
    /// </summary>
    private StudioResponse Tutorials(TutorialProgress? kept)
    {
        var series = store.Tutorials;
        var first = series.Count > 0 && series[0].Document.Tutorial!.Order == 1 && kept?.StateOf(series[0].Id) == TutorialState.New ? series[0].Id : null;
        return StudioResponse.OfJson(new
        {
            opens = first is null ? null : $"/battle?scenario={first}",
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
                    state = Named((kept ?? TutorialProgress.None).StateOf(one.Id)),
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

    /// <summary>
    /// Keeps that the account finished a tutorial, handed to the practices, which run it when its practice here says
    /// it is done, before that practice is let go of: the page says it too, but a Leave, a reload, the next practice
    /// opened or the idle sweep can reach the host first, and once the practice is gone nothing shows it was done. The
    /// practices read whether it is done under their gate, and again where they let it go, so a match that ends
    /// meanwhile is not let go of unkept (#931, #1138). Nothing for a scenario that teaches nothing.
    /// </summary>
    private async Task KeepDoneAsync(string account, string id)
    {
        if (store.Teaches(id) && AccountId.TryParse(account, out var who))
        {
            await accounts.NoteTutorialAsync(who, id, TutorialState.Done);
        }
    }

    /// <summary>The account's practice of the scenario to go on with; a tutorial that is done is kept done and let go of.</summary>
    private async Task<StudioResponse> ResumeAsync(AccountId account, string id, string name)
    {
        try
        {
            return await practices.ResumeAsync(account.ToString(), id, KeepDoneAsync) is { } current
                ? Opened(current, name)
                : Error("Scenario.NoPractice", "You have no practice of this scenario open.", 404);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            return NotKept(failure);
        }
    }

    /// <summary>
    /// Lets go of the account's practice of the scenario, as the page does when the person leaves a tutorial: 204,
    /// whether there was one or not. A tutorial done that the account's store cannot keep done answers 503 and keeps
    /// the practice: a Leave, a reload or another practice opened tries again, and so does each idle sweep (#1138).
    /// </summary>
    private async Task<StudioResponse> ForgetAsync(AccountId account, string id)
    {
        try
        {
            await practices.ForgetAsync(account.ToString(), id, KeepDoneAsync);
            return new StudioResponse(204, StudioResponse.Plain, []);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            return NotKept(failure);
        }
    }

    /// <summary>A tutorial done that the account's store could not keep: said on the console, answered 503, the practice kept.</summary>
    private StudioResponse NotKept(Exception failure)
    {
        Said($"  A tutorial done could not be kept on its account: {HttpHost.OneLine(failure)}");
        return Error(ScenarioPractices.NotKept.Error, ScenarioPractices.NotKept.Message, ScenarioPractices.NotKept.Status);
    }

    /// <summary>
    /// Opens the practice in place of the account's last one, which is kept done first when it is a tutorial done: a
    /// store that cannot keep it answers 503 and opens nothing, as a Leave does (#1138). A tutorial opened says whether
    /// the account had done every other one (#980).
    /// </summary>
    private async Task<StudioResponse> OpenAsync(string id, AccountId who, string name)
    {
        var account = who.ToString();
        var (opened, refused) = await practices.OpenAsync(id, account, KeepDoneAsync);
        if (opened is null)
        {
            return Error(refused!.Error, refused.Message, refused.Status);
        }

        if (opened.Play.Api.Practice?.Tutorial is { } tutorial)
        {
            tutorial.OthersDone = await OthersDoneAsync(who, id);
        }

        return Opened(opened, name);
    }

    /// <summary>Whether the account has done every tutorial of the series but this one; false when its store cannot say.</summary>
    private async Task<bool> OthersDoneAsync(AccountId account, string id)
    {
        try
        {
            // A tutorial missing before the last one shipped (its order skipped) is not done: the series is not finished.
            return store.Tutorials.Count >= store.Series && await accounts.TutorialsAsync(account) is { } kept
                && store.Tutorials.Where(one => one.Id != id).All(one => kept.StateOf(one.Id) == TutorialState.Done);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            Said($"  Whether an account finished the other tutorials could not be read: {HttpHost.OneLine(failure)}");
            return false;
        }
    }

    /// <summary>
    /// The practice's seat as the battle view opens a seat: its token and a table to name it by, the person against the
    /// practice bot (the source's recorded answers while they fit), and what the practice is.
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
                seats = new[] { new { seat, name, kind = "person" }, new { seat = other, name = "Practice bot", kind = "bot" } },
            },
            practice = play.Api.Practice,
        }, ArtifactJson.LineOptions);
    }

    private static StudioResponse Error(string error, string message, int status) =>
        StudioResponse.OfJson(new { error, message }, ArtifactJson.LineOptions, status);
}
