namespace DownfallArena.Domain.Matches;

/// <summary>
/// The tunable parameters of a match. Values come from configuration, never from constants in the rules.
/// </summary>
public sealed record RuleSet
{
    private RuleSet(
        int teamSize,
        int energyPerRound,
        int evolutionPicksPerOpportunity,
        int roundCap,
        double criticalMultiplier,
        int firstEvolutionRound,
        int evolutionInterval)
    {
        TeamSize = teamSize;
        EnergyPerRound = energyPerRound;
        EvolutionPicksPerOpportunity = evolutionPicksPerOpportunity;
        RoundCap = roundCap;
        CriticalMultiplier = criticalMultiplier;
        FirstEvolutionRound = firstEvolutionRound;
        EvolutionInterval = evolutionInterval;
    }

    /// <summary>
    /// The prototype's values: three creatures, two energy per round, two evolution picks in an opportunity that
    /// comes at round 1 and every second round after it, thirty rounds, double damage on a crit (ADR 0056).
    /// </summary>
    public static RuleSet Default { get; } = new(3, 2, 2, 30, 2.0, 1, 2);

    public int TeamSize { get; }

    public int EnergyPerRound { get; }

    /// <summary>How many packages a player may buy in a round that offers an opportunity.</summary>
    public int EvolutionPicksPerOpportunity { get; }

    /// <summary>
    /// The round after which the match ends by the health tiebreak (ADR 0011).
    /// </summary>
    public int RoundCap { get; }

    public double CriticalMultiplier { get; }

    /// <summary>The first round that offers an evolution opportunity.</summary>
    public int FirstEvolutionRound { get; }

    /// <summary>How many rounds apart the opportunities are: 1 is every round, 2 is every other round.</summary>
    public int EvolutionInterval { get; }

    public static RuleSet Create(
        int teamSize,
        int energyPerRound,
        int evolutionPicksPerOpportunity,
        int roundCap,
        double criticalMultiplier,
        int firstEvolutionRound = 1,
        int evolutionInterval = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(teamSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(energyPerRound);
        ArgumentOutOfRangeException.ThrowIfNegative(evolutionPicksPerOpportunity);
        ArgumentOutOfRangeException.ThrowIfLessThan(roundCap, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(firstEvolutionRound, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(evolutionInterval, 1);
        if (!double.IsFinite(criticalMultiplier) || criticalMultiplier < 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(criticalMultiplier), criticalMultiplier, "The critical multiplier must be a finite number of at least 1.");
        }

        return new RuleSet(teamSize, energyPerRound, evolutionPicksPerOpportunity, roundCap, criticalMultiplier, firstEvolutionRound, evolutionInterval);
    }

    /// <summary>
    /// Whether this round offers an evolution opportunity. This is the schedule, and it is asked here by the
    /// validation, the phase gate, the projections and the labels alike: a client that worked out the odd
    /// rounds for itself would be a second schedule to keep in agreement (ADR 0056).
    /// </summary>
    public bool IsEvolutionRound(int round) =>
        round >= FirstEvolutionRound && (round - FirstEvolutionRound) % EvolutionInterval == 0;

    /// <summary>The picks a player is given in this round: the whole allowance, or none at all.</summary>
    public int EvolutionPicksIn(int round) => IsEvolutionRound(round) ? EvolutionPicksPerOpportunity : 0;

    /// <summary>
    /// The next round that offers an opportunity, counting <paramref name="round"/> itself. What a player is
    /// told while waiting through a round that offers none.
    /// </summary>
    public int NextEvolutionRound(int round)
    {
        if (round <= FirstEvolutionRound)
        {
            return FirstEvolutionRound;
        }

        var since = (round - FirstEvolutionRound) % EvolutionInterval;
        return since == 0 ? round : round + (EvolutionInterval - since);
    }
}
