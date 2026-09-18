using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A decision the aggregate took, and the two moments that bound it: when this seat's options were put in
/// front of somebody, and when the tap was accepted.
/// </summary>
/// <remarks>
/// Both are read where they mean something and carried here rather than read again later. Submitting releases
/// the driver, which can run a whole command before this thread is scheduled again, so a clock read further
/// down would put engine time inside a duration that is supposed to measure a person reading a screen. The
/// question is named too, so the clock hands back the moment of *this* decision rather than of the one the
/// engine asked next.
/// </remarks>
/// <param name="ServedAt">Null when nothing can say the options ever reached a person.</param>
internal sealed record AcceptedDecision(HumanSeat.Question? Answered, DateTimeOffset? ServedAt, DateTimeOffset AcceptedAt);
