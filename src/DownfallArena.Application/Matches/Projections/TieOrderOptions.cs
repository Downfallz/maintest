using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// The ties in which the player holds two places or more, each as their creatures in the order the roll-off
/// left them (ADR 0063). A decision orders every one of them; a creature moves only within its own tie.
/// </summary>
public sealed record TieOrderOptions(IReadOnlyList<IReadOnlyList<CreatureId>> Groups)
{
    /// <summary>The order as it stands, every tie in timeline order: what a player who changes nothing submits.</summary>
    public IReadOnlyList<CreatureId> AsRolled => [.. Groups.SelectMany(group => group)];
}
