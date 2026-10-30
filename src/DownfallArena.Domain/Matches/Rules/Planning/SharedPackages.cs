using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// What a shared package does (ADR 0114): its buyer owns it, and every other creature of the side holds its
/// passive too, once for each copy the side owns. One rule for the match's reveal and for a hypothetical
/// board's, so the two cannot disagree.
/// </summary>
internal static class SharedPackages
{
    /// <summary>After every pick of a reveal is bought: each shared one's passive to the rest of its buyer's side.</summary>
    public static void Share(IReadOnlyList<Creature> creatures, IEnumerable<(Creature Buyer, Tier Tier)> purchases)
    {
        foreach (var (buyer, tier) in purchases.Where(purchase => purchase.Tier.SharedWithTeam))
        {
            foreach (var ally in creatures.Where(creature => creature.Owner == buyer.Owner && creature.Id != buyer.Id))
            {
                ally.HoldShared(tier);
            }
        }
    }

    /// <summary>What the shared packages a creature's teammates own give it, read off a board.</summary>
    public static Passive HeldBy(CreatureSnapshot creature, IReadOnlyList<CreatureSnapshot> board, IGameResources resources) =>
        Passive.Of(board
            .Where(ally => ally.Owner == creature.Owner && ally.Id != creature.Id)
            .SelectMany(ally => ally.AcquiredTiers)
            .Select(resources.GetTier)
            .Where(tier => tier.SharedWithTeam));
}
