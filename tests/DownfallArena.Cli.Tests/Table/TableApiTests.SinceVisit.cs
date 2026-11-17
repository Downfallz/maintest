using System.Text.Json;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// What happened since a seat's last visit, as the lobby, the inbox and "Your turn" say it (async recap plan, R4):
/// the casts the seat may see past the mark it was last shown, counted from its feed.
/// </summary>
public sealed partial class TableApiTests
{
    [Fact]
    public async Task A_seat_with_no_last_visit_has_no_casts_counted()
    {
        var table = await Seated();

        var since = await table.Api.SinceVisitAsync("player1");

        since.Casts.ShouldBeNull();
    }

    [Fact]
    public async Task The_casts_counted_are_the_ones_after_the_last_visit()
    {
        var table = await Seated();
        await PlayUpToTargeting(table);
        var shown = (await Feed(table)).Max(entry => entry.Sequence);
        (await table.Api.HandleAsync("PUT", "/api/seat/player1/seen", $$"""{ "sequence": {{shown}} }""", table.Token)).Status.ShouldBe(200);
        await AnswerEach(table, PlayerOptionsKind.Target, creature => $$"""{"kind":"Target","creature":{{creature}},"targets":[3]}""", until: PlayerOptionsKind.Speed);
        var casts = (await Feed(table)).Where(entry => entry.Kind == "CombatActionResolved").ToList();

        var since = await table.Api.SinceVisitAsync("player1");

        casts.Count(entry => entry.Sequence <= shown).ShouldBeGreaterThan(0, "a cast before the visit, which is not counted");
        since.Casts.ShouldBe(casts.Count(entry => entry.Sequence > shown));
        since.Casts.ShouldNotBe(0);
    }

    [Fact]
    public async Task A_cast_past_what_the_journal_holds_is_not_counted()
    {
        var entries = await PlayedThroughCombatAsync();
        var last = entries.Last(entry => entry.Event is CombatActionResolved).Sequence;

        var all = TableApi.CastsAfter(entries, PlayerSlot.Player1, seen: -1, feedStart: 0, journaled: null);
        var journaled = TableApi.CastsAfter(entries, PlayerSlot.Player1, seen: -1, feedStart: 0, journaled: last);

        journaled.ShouldBe(all - 1);
    }

    [Fact]
    public async Task A_cast_before_where_the_seat_s_feed_starts_is_not_counted()
    {
        var entries = await PlayedThroughCombatAsync();
        var first = entries.First(entry => entry.Event is CombatActionResolved).Sequence;

        var all = TableApi.CastsAfter(entries, PlayerSlot.Player1, seen: -1, feedStart: 0, journaled: null);
        var started = TableApi.CastsAfter(entries, PlayerSlot.Player1, seen: -1, feedStart: first + 1, journaled: null);

        started.ShouldBe(all - 1);
    }

    /// <summary>A match played through its first combat, as its trace recorded it: real entries, casts among them.</summary>
    private async Task<IReadOnlyList<TraceEntry>> PlayedThroughCombatAsync()
    {
        var table = await Seated();
        await PlayUpToTargeting(table);
        await AnswerEach(table, PlayerOptionsKind.Target, creature => $$"""{"kind":"Target","creature":{{creature}},"targets":[3]}""", until: PlayerOptionsKind.Speed);
        return _host!.Services.GetRequiredService<MatchTraceRecorder>().EntriesOf(table.Session.MatchId, 0);
    }

    /// <summary>The seat's whole feed, by sequence and kind.</summary>
    private static async Task<List<(int Sequence, string? Kind)>> Feed((TableApi Api, SteppedSession Session, SteppedPerson Person, string Token) table)
    {
        using var view = JsonDocument.Parse(Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token)));
        return [.. view.RootElement.GetProperty("feed").EnumerateArray()
            .Select(entry => (entry.GetProperty("sequence").GetInt32(), entry.GetProperty("event").GetProperty("kind").GetString()))];
    }
}
