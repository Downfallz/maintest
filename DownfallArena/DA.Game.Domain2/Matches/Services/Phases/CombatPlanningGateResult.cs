using DA.Game.Shared.Contracts.Matches.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Phases;

public sealed record CombatPlanningGateResult(
    bool CanAdvance,
    IReadOnlyList<CreatureId> MissingCreatureIds
);
