using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// Plays the greedy choice most of the time and a uniformly chosen legal one the rest of the time (ADR 0014).
/// </summary>
/// <remarks>
/// Recording with it gives every action key states where it was taken although greedy would not have chosen
/// it. That is what a value regression needs and what greedy self-play cannot give: under a deterministic
/// policy every action key is fitted on its own distribution of states, so the observed return measures how
/// good the situation was rather than how good the action is. The exploring branch draws uniformly over the
/// candidates the encoder lists for the decision, which is not what the random agent does: it never passes
/// while an unlock is available, favours the creature with fewer unlockable spells, and draws a target count
/// before the targets. An action the exploration cannot reach keeps the very defect this agent exists to
/// remove. Deterministic for a seeded source, so a recorded run replays; never a benchmark baseline, since
/// the digest is defined by agents that draw nothing (ADR 0013, decision I).
/// </remarks>
public sealed class ExploringAgent : IPlayerAgent
{
    private readonly IPlayerAgent _greedy;
    private readonly IRandomSource _source;

    public ExploringAgent(double rate, IPlayerAgent greedy, IRandomSource source)
    {
        if (!double.IsFinite(rate) || rate <= 0 || rate > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "An exploration rate is above 0 and at most 1.");
        }

        Rate = rate;
        _greedy = greedy ?? throw new ArgumentNullException(nameof(greedy));
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>The share of decisions taken at random rather than greedily.</summary>
    public double Rate { get; }

    /// <summary>Every unlock, then passing, which stays legal while an unlock is available.</summary>
    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Explores())
        {
            return _greedy.DecideEvolution(board, options);
        }

        var unlocks = options.Creatures
            .SelectMany(creature => creature.UnlockableSpells.Select(spell => new EvolutionChoice(creature.Creature, spell)))
            .ToList();
        var index = _source.NextInt32(0, unlocks.Count + 1);
        return index == unlocks.Count ? EvolutionDecision.Pass : EvolutionDecision.Unlock(unlocks[index]);
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
    {
        if (!Explores())
        {
            return _greedy.DecideSpeed(board, creature);
        }

        return _source.NextInt32(0, 2) == 0 ? Speed.Quick : Speed.Standard;
    }

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        ArgumentNullException.ThrowIfNull(intentOption);
        if (!Explores())
        {
            return _greedy.DecideIntent(board, intentOption);
        }

        var spells = intentOption.CastableSpells;
        return spells.Count > 0
            ? spells[_source.NextInt32(0, spells.Count)]
            : throw new InvalidOperationException("The options offer no castable spell.");
    }

    /// <summary>Every legal target set of an allowed size, the ones the encoder lists, with equal weight.</summary>
    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Explores())
        {
            return _greedy.DecideTargets(board, options);
        }

        if (!options.LegalTargets.IsCastable)
        {
            return [];
        }

        var sets = TargetSets.Of(options.LegalTargets).ToList();
        return sets[_source.NextInt32(0, sets.Count)];
    }

    /// <summary>One draw per decision, so the rate is the share of decisions and not of matches or rounds.</summary>
    private bool Explores() => _source.NextDouble() < Rate;
}
