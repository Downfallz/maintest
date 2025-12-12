using DA.Game.Shared.Contracts.Matches.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.ReadModels;

public sealed record CombatActionOptionsView
{
    public required int RemainingReveals { get; init; }
    public CreatureId? NextActorId { get; init; }

    public required int MinTargets { get; init; }
    public required int MaxTargets { get; init; }

    public required IReadOnlyList<CreatureId> LegalTargetIds { get; init; }
}
