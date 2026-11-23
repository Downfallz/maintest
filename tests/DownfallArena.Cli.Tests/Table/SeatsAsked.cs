using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Waits for a table's people to be asked, reading the question where the session keeps it, which holds the asking
/// made before anybody listened too. The wait is bounded by a deadline in time, two minutes, not by a count of looks:
/// a count is a few seconds, which a loaded runner can spend before the session has stepped (#1187).
/// </summary>
internal static class SeatsAsked
{
    /// <summary>How long a test waits for a question before it says the session never asked it.</summary>
    public static readonly TimeSpan Within = TimeSpan.FromMinutes(2);

    /// <summary>Each seat's person asked something numbered later than <paramref name="after" />; answers the first seat's number.</summary>
    public static async Task<long> AllAskedAsync(this IReadOnlyList<TableSeat> seats, long after, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + Within;
        while (seats.Any(seat => seat.Person!.Waiting is not { } question || question.Asked <= after))
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"No question later than {after} was asked of every seat within {Within}.");
            }

            await Task.Delay(20, cancellationToken);
        }

        return seats[0].Person!.Waiting.ShouldNotBeNull().Asked;
    }
}
