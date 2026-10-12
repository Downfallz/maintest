using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Agents;

/// <summary>
/// Every target set a legal-targets answer allows: all combinations of the candidates of every allowed size,
/// smallest first, in candidate order. Shared by the action encoder and the agents that enumerate choices.
/// </summary>
public static class TargetSets
{
    public static IEnumerable<IReadOnlyList<CreatureId>> Of(LegalTargets legal)
    {
        ArgumentNullException.ThrowIfNull(legal);

        if (!legal.IsCastable)
        {
            return [];
        }

        return Enumerable.Range(legal.MinTargets, legal.MaxTargets - legal.MinTargets + 1).SelectMany(size => Combinations(legal.Candidates, size));
    }

    /// <summary>
    /// Every spell the action options offer with every legal target set of it (ADR 0110): spells in the options'
    /// order, each spell's sets in <see cref="Of"/> order, and nothing for a spell with no legal target. The one
    /// order the encoder lists, the candidate terms read and the agents choose in, so a recorded candidate and
    /// its terms stay aligned.
    /// </summary>
    public static IEnumerable<ChosenAction> Pairs(ActionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Spells.SelectMany(offered => Of(offered.LegalTargets).Select(targets => new ChosenAction(offered.Spell, targets)));
    }

    /// <summary>All subsets of the given size, in candidate order.</summary>
    public static IEnumerable<IReadOnlyList<CreatureId>> Combinations(IReadOnlyList<CreatureId> candidates, int size)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        return Enumerate(candidates, size);
    }

    private static IEnumerable<IReadOnlyList<CreatureId>> Enumerate(IReadOnlyList<CreatureId> candidates, int size)
    {
        if (size == 0)
        {
            yield return [];
            yield break;
        }

        for (var first = 0; first <= candidates.Count - size; first++)
        {
            var head = candidates[first];
            foreach (var tail in Enumerate([.. candidates.Skip(first + 1)], size - 1))
            {
                yield return [head, .. tail];
            }
        }
    }
}
