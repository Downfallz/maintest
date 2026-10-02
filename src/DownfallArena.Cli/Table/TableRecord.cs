using System.Text.Json.Serialization;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// <c>table.json</c>: what a host needs to rebuild a table it no longer has (ADR 0091) -- who was asked to sit
/// where, the seed, the match's id, and the tokens and codes the players already hold, so a link or a code
/// handed out before the host went away still reaches the same seat afterwards. The decisions are beside it,
/// in <c>decisions.jsonl</c>.
/// </summary>
/// <remarks>
/// The tokens are written as they are. The store is the host's own (on Azure, reachable by the app's identity
/// and nothing else, ADR 0080), and a token is what a seat's link carries in the clear anyway: a store that
/// held them hashed would make a seat unreachable after a restart, which is the one thing this file is for.
/// </remarks>
internal sealed record TableRecord
{
    public const string File = "table.json";

    public const string Open = "open";

    public const string Finished = "finished";

    /// <summary>Closed by the operator, or let go of: not to be rebuilt, whatever its decisions say.</summary>
    public const string Closed = "closed";

    public required string Id { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required int Seed { get; init; }

    /// <summary>The match's id, as text: artifacts write a <see cref="MatchId" /> but never read one back, and this file is read back.</summary>
    public required string Match { get; init; }

    [JsonIgnore]
    public MatchId MatchId => MatchId.From(Guid.Parse(Match));

    /// <summary>The agent spec seat 1 was opened with, or <c>null</c> for a person.</summary>
    public string? Player1 { get; init; }

    public string? Player2 { get; init; }

    public string? Who { get; init; }

    public int? Handover { get; init; }

    public required IReadOnlyList<RecordedSeat> Seats { get; init; }

    public required string PilotToken { get; init; }

    public required string Status { get; init; }

    public static TableRecord Of(PlayedTable table, TableRequest request, JoinCodes codes)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(codes);
        return new TableRecord
        {
            Id = table.Id,
            CreatedAt = table.CreatedAt,
            Seed = request.Seed ?? throw new InvalidOperationException("A composed table has a seed."),
            Match = table.Session.MatchId.ToString(),
            Player1 = request.Player1?.ToString(),
            Player2 = request.Player2?.ToString(),
            Who = request.Who,
            Handover = request.Handover,
            Seats = [.. table.Seats.Select(seat => new RecordedSeat(seat.Name, seat.Token, codes.Of(seat), seat.Person is not null))],
            PilotToken = table.Pilot.Token,
            Status = Open,
        };
    }

    /// <summary>The request the table was composed from, to compose it again.</summary>
    public TableRequest Request() =>
        new(Player1 is null ? null : Application.Agents.AgentSpec.Parse(Player1), Player2 is null ? null : Application.Agents.AgentSpec.Parse(Player2), Who, Handover, Seed);
}

/// <summary>One seat as the record keeps it: its token, the code its person types, and whether there is one.</summary>
internal sealed record RecordedSeat(string Slot, string Token, string? Code, bool Person);
