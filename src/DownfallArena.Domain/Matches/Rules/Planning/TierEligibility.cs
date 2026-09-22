using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Which packages a creature may buy next: the ones it does not own whose prerequisite packages it does.
/// </summary>
/// <remarks>
/// <para>
/// Prerequisites are the only rule here (ADR 0056). The talent tree draws the same families for a reader and
/// used to gate what could be unlocked, but a creature is free to buy the opener of another family — that is
/// what multiclassing is — so a second gate reading the tree would forbid what the design allows.
/// </para>
/// <para>
/// This is what is available to a creature *now*, which is not the same question as what a creature could
/// ever reach: a package deeper in a family is unavailable today and reachable in two purchases.
/// </para>
/// </remarks>
public static class TierEligibility
{
    public static IReadOnlyList<TierId> AvailableTiers(CreatureSnapshot creature, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(resources);

        if (creature.IsDead)
        {
            return [];
        }

        // Ordered by id so that two runs of the same match offer the same list in the same order: an agent
        // that breaks a tie by taking the first candidate would otherwise depend on a dictionary's order.
        return
        [
            .. resources.Tiers
                .Where(tier => !creature.OwnsTier(tier.Id) && tier.Prerequisites.All(creature.OwnsTier))
                .Select(tier => tier.Id)
                .OrderBy(tier => tier.Value, StringComparer.Ordinal),
        ];
    }
}
