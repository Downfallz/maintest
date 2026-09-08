using DownfallArena.Domain.Matches.Rounds;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// The one decision a player can make in the current sub-phase, with everything they may pick from. Built
/// from the same rules the aggregate enforces, so a choice taken from here is accepted.
/// </summary>
public sealed record PlayerOptions
{
    public required PlayerOptionsKind Kind { get; init; }

    public RoundSubPhase? SubPhase { get; init; }

    public EvolutionOptions? Evolution { get; init; }

    public SpeedOptions? Speed { get; init; }

    public IntentOptions? Intent { get; init; }

    public TargetOptions? Target { get; init; }
}
