using DownfallArena.Domain.Matches.Rounds;

namespace DownfallArena.Application.Agents;

/// <summary>
/// Either one evolution choice or a pass for the rest of the round.
/// </summary>
public sealed record EvolutionDecision
{
    private EvolutionDecision(EvolutionChoice? choice)
    {
        Choice = choice;
    }

    public EvolutionChoice? Choice { get; }

    public bool IsPass => Choice is null;

    public static EvolutionDecision Pass { get; } = new(null);

    public static EvolutionDecision Unlock(EvolutionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return new EvolutionDecision(choice);
    }
}
