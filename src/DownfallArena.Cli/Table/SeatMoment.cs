using System.Text.Json;

namespace DownfallArena.Cli.Table;

/// <summary>Where a seat stands when its player sends feedback (ADR 0189): the round, the sub-phase, the journal's length, and its own view when attached.</summary>
internal sealed record SeatMoment(TableSeat Seat, int? Round, string? SubPhase, int? JournalLength, JsonElement? Position);
