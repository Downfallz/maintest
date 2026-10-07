namespace DownfallArena.Application.Agents;

/// <summary>
/// How far the lookahead reads a combat move past the round it is made in: how many rounds each rollout plays
/// after that round, and how many rollouts, each on its own dice, the reading is averaged over. The round
/// itself is always played out (ADR 0047); this is the reading of the rounds after it, the way a purchase is
/// read (ADR 0094). <see cref="None"/> is the one-round reading, which is what the agent plays unless a spec
/// says otherwise, and the only reading an agent without dice can make.
/// </summary>
public sealed record CombatReading
{
    public CombatReading(int rounds, int rollouts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rounds);
        ArgumentOutOfRangeException.ThrowIfLessThan(rollouts, 1);
        Rounds = rounds;
        Rollouts = rollouts;
    }

    /// <summary>The one-round reading: nothing past the round the move is made in.</summary>
    public static CombatReading None { get; } = new(0, 1);

    /// <summary>The rounds played after the round of the move.</summary>
    public int Rounds { get; }

    /// <summary>The rollouts, each on its own dice, the rounds after are averaged over.</summary>
    public int Rollouts { get; }

    /// <summary>Whether anything past the round of the move is read at all.</summary>
    public bool ReadsAhead => Rounds > 0;
}
