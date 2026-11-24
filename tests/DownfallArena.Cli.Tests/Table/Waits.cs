namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// How long a test of the table host waits for something the host does on its own time (a question asked, a push
/// sent) before it says it never happened. A deadline in time, not a count of looks: a count is a few seconds, which
/// a loaded runner can spend before the host has moved (#1187, #1214).
/// </summary>
internal static class Waits
{
    public static readonly TimeSpan Within = TimeSpan.FromMinutes(2);

    /// <summary>How long between two looks: short, as what is waited on is usually there within a few.</summary>
    private static readonly TimeSpan Between = TimeSpan.FromMilliseconds(20);

    /// <summary>Waits until <paramref name="done" /> holds, and says what never happened once <see cref="Within" /> has passed.</summary>
    /// <param name="what">What was waited for, as the failure says it.</param>
    public static Task UntilAsync(Func<bool> done, string what, CancellationToken cancellationToken) =>
        UntilAsync(() => Task.FromResult(done()), what, cancellationToken);

    /// <summary>Waits until <paramref name="done" /> answers true, read again until <see cref="Within" /> has passed.</summary>
    /// <param name="what">What was waited for, as the failure says it.</param>
    public static async Task UntilAsync(Func<Task<bool>> done, string what, CancellationToken cancellationToken)
    {
        if (!await HoldsAsync(done, cancellationToken))
        {
            throw new TimeoutException($"Not within {Within}: {what}.");
        }
    }

    /// <summary>Whether <paramref name="done" /> holds before <see cref="Within" /> has passed: for a test that asserts the answer with a message of its own.</summary>
    public static Task<bool> HoldsAsync(Func<bool> done, CancellationToken cancellationToken) =>
        HoldsAsync(() => Task.FromResult(done()), cancellationToken);

    /// <summary>Whether <paramref name="done" /> answers true before <see cref="Within" /> has passed.</summary>
    public static async Task<bool> HoldsAsync(Func<Task<bool>> done, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + Within;
        while (!await done())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                return false;
            }

            await Task.Delay(Between, cancellationToken);
        }

        return true;
    }
}
