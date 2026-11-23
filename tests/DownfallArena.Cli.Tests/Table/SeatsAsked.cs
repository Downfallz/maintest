using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Waits for a table's people to be asked, reading the question where the session keeps it, which holds the asking
/// made before anybody listened too, until <see cref="Waits.Within" /> has passed (#1187).
/// </summary>
internal static class SeatsAsked
{
    /// <summary>Each seat's person asked something numbered later than <paramref name="after" />; answers the first seat's number.</summary>
    public static async Task<long> AllAskedAsync(this IReadOnlyList<TableSeat> seats, long after, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + Waits.Within;
        while (seats.Any(seat => seat.Person!.Waiting is not { } question || question.Asked <= after))
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"No question later than {after} was asked of every seat within {Waits.Within}.");
            }

            await Task.Delay(20, cancellationToken);
        }

        return seats[0].Person!.Waiting.ShouldNotBeNull().Asked;
    }
}
