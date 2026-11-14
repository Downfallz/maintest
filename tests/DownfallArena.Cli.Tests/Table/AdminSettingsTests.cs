using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Hosting;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The Settings tab's route (ADR 0177): the host's settings read and replaced by the operator, stamped with who saved
/// them, and the notice every page reads on <c>/api/me</c>.
/// </summary>
public sealed class AdminSettingsTests : IDisposable
{
    private const string Token = "token-of-the-operator";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"admin-settings-{Guid.CreateVersion7():N}");
    private readonly HostedTables _hosted = new();
    private readonly TableRegistry _registry;
    private readonly FileAccountStore _store;

    public AdminSettingsTests()
    {
        _registry = _hosted.Registry();
        _store = new FileAccountStore(_root);
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

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private async Task<AdminApi> AdminAsync(bool settings = true)
    {
        var accounts = new AccountApi(new AccountRegistry(_store, TimeProvider.System), new AccountSessions(await _store.SessionKeyAsync(Cancel), TimeProvider.System), AccountsMode.Development)
        {
            Settings = settings ? new HostSettingsCache(new FileHostSettingsStore(_root), TimeProvider.System) : null,
        };
        return new AdminApi(_registry, _hosted.Composer, OperatorGate.WithToken(Token), TimeProvider.System, "Rules")
        {
            Accounts = accounts,
            Settings = accounts.Settings is { } held ? new SettingsApi(held, TimeProvider.System) : null,
        };
    }

    private static Task<StudioResponse> PutAsync(AdminApi admin, string body, string? token = Token) =>
        admin.HandleAsync("PUT", SettingsApi.Route, body, token, principal: null);

    [Fact]
    public async Task A_host_that_never_saved_its_settings_has_registration_open_and_no_notice()
    {
        var admin = await AdminAsync();

        var settings = Json(await admin.HandleAsync("GET", SettingsApi.Route, "", Token, principal: null)).GetProperty("settings");

        settings.GetProperty("registrationOpen").GetBoolean().ShouldBeTrue();
        settings.GetProperty("notice").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Saved_settings_are_kept_with_who_saved_them_and_every_page_reads_the_notice()
    {
        var admin = await AdminAsync();

        var answer = await PutAsync(admin, """{"registrationOpen":false,"notice":"Maintenance at 22:00 UTC."}""");

        answer.Status.ShouldBe(200);
        var kept = (await new FileHostSettingsStore(_root).GetAsync(Cancel)).ShouldNotBeNull();
        kept.RegistrationOpen.ShouldBeFalse();
        kept.Notice.ShouldBe("Maintenance at 22:00 UTC.");
        kept.UpdatedBy.ShouldNotBeNullOrWhiteSpace();
        var me = Json(await admin.HandleAsync("GET", AdminApi.MePath, "", token: null, principal: null));
        me.GetProperty("notice").GetString().ShouldBe("Maintenance at 22:00 UTC.");
        me.GetProperty("registrationOpen").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("""{"notice":"No switch said."}""")]
    [InlineData("""{"registrationOpen":"no"}""")]
    public async Task Settings_not_sent_whole_are_refused_and_change_nothing(string body)
    {
        var admin = await AdminAsync();

        (await PutAsync(admin, body)).Status.ShouldBe(400);

        (await new FileHostSettingsStore(_root).GetAsync(Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_notice_longer_than_a_line_is_refused()
    {
        var admin = await AdminAsync();

        var answer = await PutAsync(admin, JsonSerializer.Serialize(new { registrationOpen = true, notice = new string('a', HostSettings.LongestNotice + 1) }));

        answer.Status.ShouldBe(400);
        Json(answer).GetProperty("error").GetString().ShouldBe(HostSettings.NoticeTooLong.Code);
    }

    [Fact]
    public async Task The_settings_are_the_operator_s_alone()
    {
        var admin = await AdminAsync();

        (await PutAsync(admin, """{"registrationOpen":false}""", token: "a-seat-s-token")).Status.ShouldBe(403);
        (await new FileHostSettingsStore(_root).GetAsync(Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_host_without_settings_has_no_settings_route_and_says_no_notice()
    {
        var admin = await AdminAsync(settings: false);

        (await admin.HandleAsync("GET", SettingsApi.Route, "", Token, principal: null)).Status.ShouldBe(404);
        Json(await admin.HandleAsync("GET", AdminApi.MePath, "", token: null, principal: null)).GetProperty("notice").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
