using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// Picks uniformly among the offered options. The baseline every smarter agent is measured against, and
/// deterministic for a seeded random source.
/// </summary>
public sealed class RandomAgent(IRandomSource random) : IPlayerAgent
{
    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Creatures.Count == 0)
        {
            return EvolutionDecision.Pass;
        }

        var creature = Pick(options.Creatures);
        return EvolutionDecision.Unlock(new EvolutionChoice(creature.Creature, Pick(creature.UnlockableSpells)));
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) =>
        random.NextInt32(0, 2) == 0 ? Speed.Quick : Speed.Standard;

    public SpellId DecideIntent(PlayerBoardState board, IntentOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return Pick(option.CastableSpells);
    }

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var legal = options.LegalTargets;
        var pool = legal.Candidates.ToList();
        var count = Math.Min(random.NextInt32(legal.MinTargets, legal.MaxTargets + 1), pool.Count);
        var targets = new List<CreatureId>(count);
        for (var picked = 0; picked < count; picked++)
        {
            var index = random.NextInt32(0, pool.Count);
            targets.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return targets;
    }

    private TItem Pick<TItem>(IReadOnlyList<TItem> items)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("The options offer nothing to pick from.");
        }

        return items[random.NextInt32(0, items.Count)];
    }
}
