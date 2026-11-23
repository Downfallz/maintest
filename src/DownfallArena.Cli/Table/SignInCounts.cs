using System.Globalization;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The sign-ins' steps counted in memory, a provider each, and said as one console line a minute per provider (#1171):
/// <c>Sign-ins (&lt;provider&gt;): 3 started, 2 finished, 1 refused, 0 unavailable, 0 limited.</c> The sign-in route
/// needs no sign-in, so a line per step would let a script posting forged proofs write two lines a request to the
/// logs, which bill by volume. The ops report sums these lines (<c>scripts/ops/read.sh</c>). Thread-safe.
/// </summary>
internal sealed class SignInCounts
{
    /// <summary>How often the counts are said: the first count opens a minute, and the first request after it says them.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private readonly SortedDictionary<string, long[]> _providers = new(StringComparer.Ordinal);
    private DateTimeOffset? _opened;

    /// <summary>A step of a sign-in, as the line names it.</summary>
    public enum Step
    {
        Started,
        Finished,
        Refused,
        Unavailable,
        Limited,
    }

    /// <summary>Counts one step of the provider's sign-ins; how many of that step the open minute holds now.</summary>
    public long Add(string provider, Step step, DateTimeOffset now)
    {
        lock (_gate)
        {
            _opened ??= now;
            if (!_providers.TryGetValue(provider, out var counts))
            {
                _providers[provider] = counts = new long[Enum.GetValues<Step>().Length];
            }

            return ++counts[(int)step];
        }
    }

    /// <summary>The lines of the minute, a provider each, once it is over, and the counts begun again; none before.</summary>
    public IReadOnlyList<string> Due(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_opened is not { } opened || now - opened < Every)
            {
                return [];
            }

            var lines = _providers.Select(provider => Line(provider.Key, provider.Value)).ToList();
            _providers.Clear();
            _opened = null;
            return lines;
        }
    }

    private static string Line(string provider, long[] counts) => string.Create(
        CultureInfo.InvariantCulture,
        $"  Sign-ins ({provider}): {counts[(int)Step.Started]} started, {counts[(int)Step.Finished]} finished, {counts[(int)Step.Refused]} refused, {counts[(int)Step.Unavailable]} unavailable, {counts[(int)Step.Limited]} limited.");
}
