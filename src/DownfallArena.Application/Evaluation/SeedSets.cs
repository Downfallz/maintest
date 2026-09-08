namespace DownfallArena.Application.Evaluation;

/// <summary>
/// A stable identity for an explicit seed list, so that a run stamp names the seed set an evaluation played
/// rather than a base seed it never used: the same list gives the same number on every machine.
/// </summary>
public static class SeedSets
{
    public static int IdentityOf(IReadOnlyList<int> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);

        var identity = 17;
        foreach (var seed in seeds)
        {
            identity = unchecked((identity * 31) + seed);
        }

        return identity;
    }
}
