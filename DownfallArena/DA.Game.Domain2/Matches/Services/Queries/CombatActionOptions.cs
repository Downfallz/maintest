using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;

namespace DA.Game.Domain2.Matches.Services.Queries;

// =================================================
// Combat_RevealAndTarget
// =================================================
public sealed record CombatActionOptions
{
    public required int RemainingReveals { get; init; }
    public CreatureId? NextActorId { get; init; }

    public required int MinTargets { get; init; }
    public required int MaxTargets { get; init; }

    public required IReadOnlyList<CreatureId> LegalTargetIds { get; init; }
}