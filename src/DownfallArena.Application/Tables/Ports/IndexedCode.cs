namespace DownfallArena.Application.Tables.Ports;

/// <summary>A join code, and the table and seat it opens.</summary>
public sealed record IndexedCode(string Code, string Table, string Seat);
