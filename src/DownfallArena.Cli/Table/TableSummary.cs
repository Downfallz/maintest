using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What the index keeps of an open table (ADR 0161): what a lobby lists and counts, read off the table's record,
/// and no key of it. A seat's token, invitation and code stay in the record; a code is an index entry of its own.
/// </summary>
internal sealed record TableSummary(
    string Format,
    string Declaration,
    bool Casual,
    bool Begun,
    IReadOnlyList<string> OpenSeats,
    IReadOnlyList<TableSummary.Seat> Seats,
    ReplayBasis? RefusedUnder = null)
{
    /// <summary>A seat as a lobby sees it: whose, and whether a person still has to take or reach it.</summary>
    internal sealed record Seat(string Slot, bool Person, bool? Taken, string? Account, string? InvitedAccount, bool? Arrived);

    public static TableSummary Of(TableRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new TableSummary(
            record.Format ?? string.Empty,
            record.Declaration ?? string.Empty,
            record.Casual,
            record.Begun,
            record.OpenSeats,
            [.. record.Seats.Select(seat => new Seat(seat.Slot, seat.Person, seat.Taken, seat.Account, seat.InvitedAccount, seat.Arrived))],
            record.RefusedUnder);
    }

    /// <summary>A live table's summary, as its record would say it now; none for a table composed from no request.</summary>
    public static TableSummary? Of(PlayedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.Request is { } request ? Of(TableRecord.Of(table, request)) : null;
    }

    /// <summary>How many seats are offered to anybody and still wait for a person to take and reach them.</summary>
    public int OpenSeatCount => Seats.Count(seat => OpenSeats.Contains(seat.Slot) && seat.Person && seat.Arrived != true);

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, ArtifactJson.LineOptions);

    /// <summary>A summary as the index keeps it, or none when the text is not one.</summary>
    public static TableSummary? Parse(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : System.Text.Json.JsonSerializer.Deserialize<TableSummary>(text, ArtifactJson.LineOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
