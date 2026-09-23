using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>A tie order drawn uniformly: every tie shuffled on its own, since a creature moves only within its tie.</summary>
internal static class TieShuffle
{
    public static IReadOnlyList<CreatureId> Of(TieOrderOptions options, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);

        var order = new List<CreatureId>();
        foreach (var group in options.Ties)
        {
            var shuffled = group.ToList();
            for (var index = shuffled.Count - 1; index > 0; index--)
            {
                var swap = random.NextInt32(0, index + 1);
                (shuffled[index], shuffled[swap]) = (shuffled[swap], shuffled[index]);
            }

            order.AddRange(shuffled);
        }

        return order;
    }
}
