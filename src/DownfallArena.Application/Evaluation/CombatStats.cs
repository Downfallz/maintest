namespace DownfallArena.Application.Evaluation;

/// <summary>
/// How one player's actions resolved in a match.
/// </summary>
public sealed record CombatStats(int Actions, int Fizzles, int Criticals)
{
    public CombatStats Plus(CombatStats other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new CombatStats(Actions + other.Actions, Fizzles + other.Fizzles, Criticals + other.Criticals);
    }
}
