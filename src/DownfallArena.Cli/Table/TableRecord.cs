using System.Text.Json.Serialization;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// <c>table.json</c>: what a host needs to pick up a table it does not hold (ADR 0159) -- who was asked to sit
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

    /// <summary>Missing on legacy tables; their trace indices must remain unchanged across restarts (ADR 0132).</summary>
    public int EventStreamVersion { get; init; } = 1;

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

    /// <summary>
    /// The format the table <em>played</em> (<c>2v2</c>), which is the resolved one and not the one the request
    /// named: a request that named none played the host's own, and recording that as nothing would rebuild the
    /// table in whatever the host's default has become since. A rebuilt table has to come back in the format it
    /// was recorded in, because the replay feeds it the decisions of a match with that many creatures
    /// (ADR 0159). Null only in a record written before this field existed.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// The combat declaration the table played (ADR 0110), resolved like <see cref="Format" /> and for the same
    /// reason: a replay feeds the rebuilt match the decisions of one declaration. Null in a record written before
    /// a table could play another, which was always <c>hidden-intent</c>.
    /// </summary>
    public string? Declaration { get; init; }

    public required IReadOnlyList<RecordedSeat> Seats { get; init; }

    public required string PilotToken { get; init; }

    public required string Status { get; init; }

    /// <summary>
    /// The basis a host refused to rebuild this table under, or none. Since ADR 0163 a refusal voids the table
    /// (<see cref="Void" />), and this says under what. A record refused before then is still open and kept for the
    /// operator (ADR 0115): a host with the same basis does not replay it again, and one on another basis tries it
    /// once more, and voids it if it is refused again.
    /// </summary>
    public ReplayBasis? RefusedUnder { get; init; }

    /// <summary>
    /// Whether the table ended as a void match (ADR 0163): its journal no longer replays on the engine that picked it
    /// up, so it was closed with no outcome, nothing was kept for the ladder, and its seats were told why.
    /// </summary>
    public bool Void { get; init; }

    /// <summary>Why the operator voided the table (ADR 0176), or null for a table that ended otherwise or voided itself (ADR 0163).</summary>
    public Application.Accounts.MatchVoid? VoidReason { get; init; }

    /// <summary>
    /// The hash of the content the table began on (ADR 0163), kept in the store under it: a host on other content
    /// replays the table on this one. None in a record written before it was kept, and such a table is replayed on
    /// the host's own content.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// The rule set the table plays (ADR 0163), so a host reading another rules file replays it under these. None in
    /// a record written before it was kept, and such a table plays the host's rules in its recorded format.
    /// </summary>
    public RecordedRules? Rules { get; init; }

    /// <summary>Only explicitly offered seats are public. Older, invitation-only tables offer none.</summary>
    public IReadOnlyList<string> OpenSeats { get; init; } = [];

    /// <summary>Opened as unranked (ADR 0155). False in a record written before a game could be.</summary>
    public bool Casual { get; init; }

    /// <summary>How many hours a player asked has to answer before they lose (ADR 0169); none for a table without a limit.</summary>
    public int? TurnLimitHours { get; init; }

    /// <summary>When the match began, by the clock of the host that began it; none before it has, and in an older record.</summary>
    public DateTimeOffset? BegunAt { get; init; }

    /// <summary>
    /// How many times the record has been written, the first write being 1: what a conditional write is read
    /// and refused on, and the record's half of the table's version once a second process writes (ADR 0159).
    /// </summary>
    public long Revision { get; init; }

    /// <summary>
    /// Whether the match has begun: every person reached their seat (ADR 0092), or there was nobody to wait for.
    /// A table picked up again that had begun begins at once, whether or not a decision was taken yet.
    /// </summary>
    public bool Begun { get; init; }

    public static TableRecord Of(PlayedTable table, TableRequest request)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(request);
        return new TableRecord
        {
            EventStreamVersion = table.EventStreamVersion,
            Id = table.Id,
            CreatedAt = table.CreatedAt,
            Seed = request.Seed ?? throw new InvalidOperationException("A composed table has a seed."),
            Match = table.Session.MatchId.ToString(),
            Player1 = request.Player1?.ToString(),
            Player2 = request.Player2?.ToString(),
            Who = request.Who,
            Handover = request.Handover,
            Format = table.Session.Rules.Format.ToString(),
            Declaration = CombatDeclarationText.Of(table.Session.Rules.CombatDeclaration),
            Seats = [.. table.Seats.Select(seat => new RecordedSeat(seat.Name, seat.Token, seat.Code, seat.Person is not null, seat.Invitation, seat.Person is null ? null : seat.Taken, seat.Push, seat.Account?.Value, seat.InvitedAccount?.Value, seat.InvitedAt, seat.DeclinedBy?.Value, seat.Person?.HasArrived))],
            PilotToken = table.Pilot.Token,
            Status = Open,
            OpenSeats = table.OpenSeats,
            Casual = table.Casual,
            TurnLimitHours = table.TurnLimit is { } limit ? (int)limit.TotalHours : null,
            Begun = table.Session.HasBegun,
            BegunAt = table.Session.BegunAt,
            Content = table.Content,
            Rules = RecordedRules.Of(table.Session.Rules),
        };
    }

    /// <summary>
    /// This record written over the one stored, which another host may have written since this one last read
    /// it: a table that ended stays ended, a refusal and a beginning are kept, and the revision is the next one.
    /// </summary>
    public TableRecord Over(TableRecord? stored) =>
        this with
        {
            Revision = (stored?.Revision ?? 0) + 1,
            Status = stored is { Status: not Open } ended && Status == Open ? ended.Status : Status,
            RefusedUnder = RefusedUnder ?? stored?.RefusedUnder,
            Void = Void || stored?.Void == true,
            VoidReason = VoidReason ?? stored?.VoidReason,
            Content = Content ?? stored?.Content,
            Rules = Rules ?? stored?.Rules,
            Begun = Begun || stored?.Begun == true,
            // The first beginning stands: a host picking a begun table up begins it again, later.
            BegunAt = stored?.BegunAt ?? BegunAt,
            TurnLimitHours = TurnLimitHours ?? stored?.TurnLimitHours,
        };

    /// <summary>The request the table was composed from, to compose it again.</summary>
    public TableRequest Request() =>
        new(
            Player1 is null ? null : Application.Agents.AgentSpec.Parse(Player1),
            Player2 is null ? null : Application.Agents.AgentSpec.Parse(Player2),
            Who,
            Handover,
            Seed,
            Domain.Matches.MatchFormat.TryParse(Format, out var format) ? format : null,
            CombatDeclarationText.TryParse(Declaration, out var declaration) ? declaration : Domain.Matches.CombatDeclaration.HiddenIntent);
}

/// <summary>
/// One seat as the record keeps it: its token, the code its person types, whether there is one, the invitation
/// still outstanding for it, and whether a browser has taken it (ADR 0116). <paramref name="Taken" /> is null in
/// a record written before invitations existed, whose links carried the seat's token itself. <paramref name="Push" />
/// is where the browser holding the seat asked its notices to be sent (ADR 0118), kept so the next host sends
/// there too. <paramref name="Account" /> is the account the seat belongs to (ADR 0122), so a rebuilt table is still
/// theirs. <paramref name="InvitedAccount" /> and <paramref name="InvitedAt" /> are the friend the seat is kept for
/// and since when (ADR 0129), so a rebuilt table still waits for them; <paramref name="DeclinedBy" /> is who declined it.
/// </summary>
internal sealed record RecordedSeat(string Slot, string Token, string? Code, bool Person, string? Invitation = null, bool? Taken = null, PushSubscription? Push = null, string? Account = null, string? InvitedAccount = null, DateTimeOffset? InvitedAt = null, string? DeclinedBy = null, bool? Arrived = null);
