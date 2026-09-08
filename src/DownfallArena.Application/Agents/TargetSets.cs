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

    /// <summary>All subsets of the given size, in candidate order.</summary>
    public static IEnumerable<IReadOnlyList<CreatureId>> Combinations(IReadOnlyList<CreatureId> candidates, int size)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (size == 0)
        {
            yield return [];
            yield break;
        }

        for (var first = 0; first <= candidates.Count - size; first++)
        {
            var head = candidates[first];
            foreach (var tail in Combinations([.. candidates.Skip(first + 1)], size - 1))
            {
                yield return [head, .. tail];
            }
        }
    }
}
