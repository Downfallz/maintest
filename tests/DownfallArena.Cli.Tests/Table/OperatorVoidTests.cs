using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The operator voids a match, in progress or finished, with a reason (ADR 0176): the record keeps it, the seats are
/// answered with the explanation, and a kept result is marked void so it counts nowhere.
/// </summary>
public sealed class OperatorVoidTests : IDisposable
{
    private const string Token = "token-of-the-operator";
    private const string Explanation = "The match stopped answering after a host restart.";

    private readonly HostedTables _hosted = new(recording: true);
    private readonly Kept _results = new();
    private readonly TableRegistry _registry;
    private readonly AdminApi _admin;

    public OperatorVoidTests()
    {
        _registry = _hosted.Registry();
        var composer = new TableComposer(_hosted.Services, HostedTables.Rules, new FileArtifactStore(_hosted.RunsDirectory)) { Results = _results, Index = _hosted.Index };
        _admin = new AdminApi(_registry, composer, OperatorGate.WithToken(Token), TimeProvider.System, "Rules", _hosted.Stored);
    }

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private async Task<(string Id, string SeatToken)> OpenedAsync()
    {
        var opened = Json(await _admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy"}""", Token, principal: null));
        var id = opened.GetProperty("id").GetString()!;
        return (id, _registry.Held(id).ShouldNotBeNull().Seats[0].Token);
    }

    private Task<StudioResponse> VoidAsync(string id, string body) => _admin.HandleAsync("POST", $"/api/tables/{id}/void", body, Token, principal: null);

    private static string Reason(string category = "stuck", string explanation = Explanation) => JsonSerializer.Serialize(new { category, explanation });

    [Fact]
    public async Task A_match_in_progress_is_voided_with_its_reason_and_let_go_of()
    {
        var (id, _) = await OpenedAsync();

        var answer = await VoidAsync(id, Reason());

        answer.Status.ShouldBe(200);
        _registry.Held(id).ShouldBeNull();
        var record = (await _hosted.Stored!.RecordAsync(id, Cancel)).ShouldNotBeNull();
        record.Status.ShouldBe(TableRecord.Closed);
        record.Void.ShouldBeTrue();
        record.VoidReason.ShouldNotBeNull().ShouldSatisfyAllConditions(
            reason => reason.Category.ShouldBe("stuck"),
            reason => reason.Explanation.ShouldBe(Explanation),
            reason => reason.By.ShouldNotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task A_seat_of_a_voided_match_is_told_the_operator_s_explanation()
    {
        var (id, seat) = await OpenedAsync();

        await VoidAsync(id, Reason());

        (await _registry.VoidSeatAsync(seat, Cancel)).ShouldBe(Explanation);
    }

    [Fact]
    public async Task A_kept_result_is_marked_void_and_counts_on_no_ladder()
    {
        var (id, _) = await OpenedAsync();
        await _results.RecordAsync(new MatchResult(Guid.CreateVersion7().ToString(), DateTimeOffset.UtcNow, "3v3", "at-activation", "hash", 9, "player1",
            [new ResultSeat("player1", AccountId.New(), null), new ResultSeat("player2", AccountId.New(), null)], id), Cancel);

        var answer = Json(await VoidAsync(id, Reason("abuse", "A player won through a bug in the order of the slots.")));

        answer.GetProperty("withdrawn").GetBoolean().ShouldBeTrue();
        var kept = _results.All.ShouldHaveSingleItem();
        kept.Voided.ShouldNotBeNull().Category.ShouldBe("abuse");
        kept.Ranked.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("""{"category":"stuck"}""")]
    [InlineData("""{"category":"bored","explanation":"The match stopped answering."}""")]
    [InlineData("""{"category":"stuck","explanation":"too short"}""")]
    public async Task A_void_without_a_reason_is_refused_and_voids_nothing(string body)
    {
        var (id, _) = await OpenedAsync();

        var answer = await VoidAsync(id, body);

        answer.Status.ShouldBe(400);
        Json(answer).GetProperty("error").GetString().ShouldBe(MatchVoid.Invalid.Code);
        _registry.Held(id).ShouldNotBeNull();
    }

    /// <summary>
    /// A results store that fails while the result is withdrawn leaves the record not void, so voiding again withdraws
    /// it: a void record whose result still counted would refuse every retry as already void.
    /// </summary>
    [Fact]
    public async Task A_void_whose_result_could_not_be_withdrawn_can_be_tried_again()
    {
        var (id, _) = await OpenedAsync();
        await _results.RecordAsync(new MatchResult(Guid.CreateVersion7().ToString(), DateTimeOffset.UtcNow, "3v3", "at-activation", "hash", 9, "player1",
            [new ResultSeat("player1", AccountId.New(), null), new ResultSeat("player2", AccountId.New(), null)], id), Cancel);
        _results.FailNextRecord = true;

        await Should.ThrowAsync<IOException>(() => VoidAsync(id, Reason()));
        (await _hosted.Stored!.RecordAsync(id, Cancel)).ShouldNotBeNull().Void.ShouldBeFalse();

        (await VoidAsync(id, Reason())).Status.ShouldBe(200);
        _results.All.ShouldHaveSingleItem().Voided.ShouldNotBeNull();
        (await _hosted.Stored!.RecordAsync(id, Cancel)).ShouldNotBeNull().Void.ShouldBeTrue();
    }

    [Fact]
    public async Task A_void_is_final()
    {
        var (id, _) = await OpenedAsync();
        await VoidAsync(id, Reason());

        (await VoidAsync(id, Reason("other", "Voided twice by mistake, to see."))).Status.ShouldBe(409);
        (await _hosted.Stored!.RecordAsync(id, Cancel)).ShouldNotBeNull().VoidReason.ShouldNotBeNull().Category.ShouldBe("stuck");
    }

    /// <summary>
    /// A result kept for a match after it was voided -- a closer elsewhere, or a store that failed once the record said
    /// void -- is withdrawn when the operator voids it again, under the reason it was voided for; the void stays final.
    /// </summary>
    [Fact]
    public async Task A_result_still_counting_for_a_void_match_is_withdrawn_when_it_is_voided_again()
    {
        var (id, _) = await OpenedAsync();
        await VoidAsync(id, Reason());
        await _results.RecordAsync(new MatchResult(Guid.CreateVersion7().ToString(), DateTimeOffset.UtcNow, "3v3", "at-activation", "hash", 9, "player1",
            [new ResultSeat("player1", AccountId.New(), null), new ResultSeat("player2", AccountId.New(), null)], id), Cancel);

        var answer = await VoidAsync(id, Reason("other", "Voided again to withdraw what still counted."));

        answer.Status.ShouldBe(200);
        Json(answer).GetProperty("withdrawn").GetBoolean().ShouldBeTrue();
        _results.All.ShouldHaveSingleItem().Voided.ShouldNotBeNull().Category.ShouldBe("stuck");
        (await _hosted.Stored!.RecordAsync(id, Cancel)).ShouldNotBeNull().VoidReason.ShouldNotBeNull().Category.ShouldBe("stuck");
    }

    [Fact]
    public async Task A_table_nobody_recorded_cannot_be_voided()
    {
        (await VoidAsync("no-such-table", Reason())).Status.ShouldBe(404);
    }

    [Fact]
    public async Task A_void_is_the_operator_s_alone()
    {
        var (id, _) = await OpenedAsync();

        (await _admin.HandleAsync("POST", $"/api/tables/{id}/void", Reason(), "a-seat-s-token", principal: null)).Status.ShouldBe(403);
    }

    [Fact]
    public async Task The_sessions_list_says_which_sessions_are_void_and_which_can_be_voided()
    {
        var (open, _) = await OpenedAsync();
        var (voided, _) = await OpenedAsync();
        await VoidAsync(voided, Reason("engine", "The engine resolved one spell twice in a row."));

        var sessions = Json(await _admin.HandleAsync("GET", "/api/sessions", "", Token, principal: null)).GetProperty("sessions").EnumerateArray().ToArray();

        var first = sessions.Single(session => session.GetProperty("id").GetString() == open);
        first.GetProperty("voidable").GetBoolean().ShouldBeTrue();
        first.GetProperty("voided").ValueKind.ShouldBe(JsonValueKind.Null);
        var second = sessions.Single(session => session.GetProperty("id").GetString() == voided);
        second.GetProperty("voidable").GetBoolean().ShouldBeFalse();
        second.GetProperty("voided").GetString().ShouldBe("engine");
    }

    private sealed class Kept : IMatchResults
    {
        private readonly Dictionary<string, MatchResult> _kept = [];

        public IReadOnlyList<MatchResult> All => [.. _kept.Values];

        /// <summary>The next record fails, as a store that is out does.</summary>
        public bool FailNextRecord { get; set; }

        public Task RecordAsync(MatchResult result, CancellationToken cancellationToken = default)
        {
            if (FailNextRecord)
            {
                FailNextRecord = false;
                throw new IOException("The results store is out.");
            }

            _kept[result.MatchId] = result;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MatchResult>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult(All);
    }
}
