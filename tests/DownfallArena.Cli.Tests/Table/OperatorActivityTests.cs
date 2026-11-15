using System.Text;
using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Application.Admin.Ports;
using DownfallArena.Application.Hosting;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.Admin;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The operator log (admin plan, A6): what an operator does from the panel is noted with who and on what, read back in
/// the Activity tab, newest first; a log that cannot be written never stops the action it notes.
/// </summary>
public sealed class OperatorActivityTests : IDisposable
{
    private const string Token = "token-of-the-operator";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"operator-activity-{Guid.CreateVersion7():N}");
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry;

    public OperatorActivityTests()
    {
        _registry = _hosted.Registry();
    }

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private AdminApi Admin(IOperatorLog log, HostSettingsCache? settings = null)
    {
        var notes = new OperatorNotes(new OperatorActions(log, TimeProvider.System));
        return new AdminApi(_registry, _hosted.Composer, OperatorGate.WithToken(Token), TimeProvider.System, "Rules", _hosted.Stored)
        {
            Notes = notes,
            Settings = settings is null ? null : new SettingsApi(settings, TimeProvider.System) { Notes = notes },
        };
    }

    private static async Task<JsonElement[]> ActivityAsync(AdminApi admin)
    {
        var answer = await admin.HandleAsync("GET", ActivityApi.Route, "", Token, principal: null);
        answer.Status.ShouldBe(200);
        return [.. Json(answer).GetProperty("actions").EnumerateArray()];
    }

    [Fact]
    public async Task Opening_and_closing_a_table_are_noted_newest_first_with_who_and_on_which_table()
    {
        var admin = Admin(new FileOperatorLog(_root));
        var id = Json(await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy"}""", Token, principal: null)).GetProperty("id").GetString()!;
        await Task.Delay(5, TestContext.Current.CancellationToken);
        await admin.HandleAsync("DELETE", $"/api/tables/{id}", "", Token, principal: null);

        var actions = await ActivityAsync(admin);

        actions.Select(action => action.GetProperty("action").GetString()).ShouldBe([OperatorActions.CloseTable, OperatorActions.OpenTable]);
        actions.ShouldAllBe(action => action.GetProperty("target").GetString() == id && action.GetProperty("by").GetString() != null);
    }

    [Fact]
    public async Task Saving_the_settings_is_noted_with_what_they_now_say()
    {
        var admin = Admin(new FileOperatorLog(_root), new HostSettingsCache(new FileHostSettingsStore(_root), TimeProvider.System));

        await admin.HandleAsync("PUT", SettingsApi.Route, """{"registrationOpen":false,"notice":"Back at 23:00."}""", Token, principal: null);

        var saved = (await ActivityAsync(admin)).ShouldHaveSingleItem();
        saved.GetProperty("action").GetString().ShouldBe(OperatorActions.SaveSettings);
        saved.GetProperty("detail").GetString().ShouldBe("registration closed, notice \"Back at 23:00.\"");
    }

    [Fact]
    public async Task A_log_that_cannot_be_written_never_stops_the_action_it_notes()
    {
        var admin = Admin(new Out());

        var opened = await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy"}""", Token, principal: null);

        opened.Status.ShouldBe(201);
    }

    [Fact]
    public async Task A_host_without_an_operator_log_has_no_activity_and_the_log_is_the_operator_s_alone()
    {
        var bare = new AdminApi(_registry, _hosted.Composer, OperatorGate.WithToken(Token), TimeProvider.System, "Rules");
        var kept = Admin(new FileOperatorLog(_root));

        (await bare.HandleAsync("GET", ActivityApi.Route, "", Token, principal: null)).Status.ShouldBe(404);
        (await kept.HandleAsync("GET", ActivityApi.Route, "", "a-seat-s-token", principal: null)).Status.ShouldBe(403);
    }

    /// <summary>A log whose store is out, as one whose storage account does not answer.</summary>
    private sealed class Out : IOperatorLog
    {
        public Task RecordAsync(OperatorAction action, CancellationToken cancellationToken = default) => throw new IOException("The log is out.");

        public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) => throw new IOException("The log is out.");

        public Task<OperatorAction?> FindAsync(string id, CancellationToken cancellationToken = default) => throw new IOException("The log is out.");
    }
}
