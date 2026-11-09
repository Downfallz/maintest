using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The shape a result is kept in, the same in a directory and in a blob container:
/// <c>results/&lt;yyyy-MM&gt;/&lt;match&gt;.json</c>, so a month is one prefix to read and a match is one record.
/// </summary>
internal static class ResultDocuments
{
    public const string Prefix = "results/";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string PathOf(MatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!Guid.TryParse(result.MatchId, out var match))
        {
            throw new ArgumentException($"'{result.MatchId}' is not a match id.", nameof(result));
        }

        return $"{Prefix}{result.FinishedAt.UtcDateTime:yyyy-MM}/{match:D}.json";
    }

    public static string Write(MatchResult result) => JsonSerializer.Serialize(
        new Record(
            result.MatchId,
            result.FinishedAt,
            result.Format,
            result.Declaration,
            result.ContentHash,
            result.Rounds,
            result.Winner,
            result.Ranked,
            [.. result.Seats.Select(seat => new SeatRecord(seat.Seat, seat.Account?.Value, seat.Agent))],
            result.Table,
            result.Casual),
        Web);

    public static MatchResult Read(string json)
    {
        var record = JsonSerializer.Deserialize<Record>(json, Web) ?? throw new InvalidDataException("A result record is empty.");
        return new MatchResult(
            record.Match,
            record.FinishedAt,
            record.Format,
            record.Declaration,
            record.ContentHash,
            record.Rounds,
            record.Winner,
            [.. record.Seats.Select(seat => new ResultSeat(seat.Seat, AccountId.TryParse(seat.Account, out var id) ? id : null, seat.Agent))],
            record.Table,
            record.Casual);
    }

    private sealed record Record(string Match, DateTimeOffset FinishedAt, string Format, string Declaration, string ContentHash, int Rounds, string? Winner, bool Ranked, IReadOnlyList<SeatRecord> Seats, string? Table = null, bool Casual = false);

    private sealed record SeatRecord(string Seat, string? Account, string? Agent);
}
