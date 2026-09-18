namespace DownfallArena.Cli.Table;

/// <summary>What a swap changes, as one value: who held the seat, who takes it, and the round they take it at.</summary>
/// <remarks>
/// Three arguments in the same order at every call site is how the direction of a swap ends up recorded
/// backwards, which is the one thing a <c>Seat</c> note exists to get right.
/// </remarks>
internal sealed record SeatChange(string From, string To, int AtRound);
