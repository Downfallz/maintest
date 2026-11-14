using DownfallArena.Cli.Studio;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A player's side of the match scenarios (docs/tabletop/scenarios-plan.md, S4, ADR 0179). A signed-in account reads
/// what a scenario is (<c>GET /api/scenarios/&lt;id&gt;</c>), opens its practice from the cut in place of whatever
/// practice it had (<c>POST /api/scenarios/&lt;id&gt;/practice</c>), and finds the one it has again
/// (<c>GET .../practice</c>), which is how a reloaded page resumes it. Signed out, every route answers 401.
/// </summary>
internal sealed class ScenariosApi(ScenarioStore store, ScenarioPractices practices, AccountApi accounts)
{
    public const string Prefix = "/api/scenarios";

    public static bool Names(string path) => path.StartsWith(Prefix + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, AccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(request);
        if (await accounts.WhoAsync(request) is not { } who)
        {
            return Error("Account.SignInRequired", "Sign in to practise a scenario.", 401);
        }

        var segments = path[(Prefix.Length + 1)..].Split('/');
        var id = segments[0];
        if (!ScenarioStore.Names(id))
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
            _ => Error("Scenario.NoRoute", $"No such route: {method} {path}", 404),
        };
        return who.Renewed is { } renewed ? answer with { Headers = [.. answer.Headers ?? [], new KeyValuePair<string, string>("Set-Cookie", renewed)] } : answer;
    }

    private async Task<StudioResponse> DescribeAsync(string id)
    {
        if (await store.GetAsync(id) is not { } document)
        {
            return Error("Scenario.NotFound", "This scenario does not exist, or was removed.", 404);
        }

        var cut = document.Scenario.Cut;
        return StudioResponse.OfJson(new
        {
            id,
            title = document.Title ?? $"Round {cut.Round}",
            goal = document.Goal,
            round = cut.Round,
            seat = ScenarioCommands.SeatName(document.Seat),
            format = document.Scenario.Source.Rules.Format.ToString(),
        }, ArtifactJson.LineOptions);
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
                id = $"practice-{practice.Scenario}",
                format = play.Session.Rules.Format.ToString(),
                seats = new[] { new { seat, name, kind = "person" }, new { seat = other, name = "Recorded opponent", kind = "bot" } },
            },
            practice = play.Api.Practice,
        }, ArtifactJson.LineOptions);
    }

    private static StudioResponse Error(string error, string message, int status) =>
        StudioResponse.OfJson(new { error, message }, ArtifactJson.LineOptions, status);
}
