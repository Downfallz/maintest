using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// What a reveal does after every pick is bought (ADR 0114): a shared package goes to every creature of its
/// buyer's side. One rule for the match's reveal and for a hypothetical board's, so the two cannot disagree.
/// </summary>
internal static class SharedPackages
{
    public static void Share(IReadOnlyList<Creature> creatures, IEnumerable<(Creature Buyer, Tier Tier)> purchases)
    {
        foreach (var (buyer, tier) in purchases.Where(purchase => purchase.Tier.SharedWithTeam))
        {
            foreach (var ally in creatures.Where(creature => creature.Owner == buyer.Owner))
            {
                ally.Share(tier);
            }
        }
    }
}
