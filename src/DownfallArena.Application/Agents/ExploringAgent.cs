using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// Plays the greedy choice most of the time and a uniformly random legal one the rest of the time (ADR 0014).
/// </summary>
/// <remarks>
/// Recording with it gives every action key states where it was taken although greedy would not have chosen
/// it. That is what a value regression needs and what greedy self-play cannot give: under a deterministic
/// policy every action key is fitted on its own distribution of states, so the observed return measures how
/// good the situation was rather than how good the action is. Deterministic for a seeded source, like the
/// random agent, so a recorded run replays; never a benchmark baseline, since the digest is defined by agents
/// that draw nothing (ADR 0013, decision I).
/// </remarks>
public sealed class ExploringAgent : IPlayerAgent
{
    private readonly double _rate;
    private readonly IPlayerAgent _greedy;
    private readonly IPlayerAgent _random;
    private readonly IRandomSource _source;

    public ExploringAgent(double rate, IPlayerAgent greedy, IPlayerAgent random, IRandomSource source)
    {
        if (!double.IsFinite(rate) || rate <= 0 || rate > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "An exploration rate is above 0 and at most 1.");
        }

        _rate = rate;
        _greedy = greedy ?? throw new ArgumentNullException(nameof(greedy));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>The share of decisions taken at random rather than greedily.</summary>
    public double Rate => _rate;

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) =>
        Next().DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Next().DecideSpeed(board, creature);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) =>
        Next().DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) =>
        Next().DecideTargets(board, options);

    /// <summary>One draw per decision, so the rate is the share of decisions and not of matches or rounds.</summary>
    private IPlayerAgent Next() => _source.NextDouble() < _rate ? _random : _greedy;
}
