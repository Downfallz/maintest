using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Whether the Speed sub-phase is complete, and which creatures still need a speed choice.
/// </summary>
public sealed record SpeedGateResult(bool CanAdvance, IReadOnlyList<CreatureId> Player1Missing, IReadOnlyList<CreatureId> Player2Missing)
{
    public IReadOnlyList<CreatureId> MissingOf(PlayerSlot slot) => slot == PlayerSlot.Player1 ? Player1Missing : Player2Missing;
}
