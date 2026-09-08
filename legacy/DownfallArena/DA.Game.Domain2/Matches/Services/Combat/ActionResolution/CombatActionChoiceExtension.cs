using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.ActionResolution;

public static class CombatActionChoiceExtensions
{
    public static CombatActionChoice WithFilteredTargets(
        this CombatActionChoice choice,
        IReadOnlyCollection<CreatureId> allowedTargetIds)
    {
        if (choice is null)
            throw new ArgumentNullException(nameof(choice));

        var filtered = choice.TargetIds?
            .Where(id => allowedTargetIds.Contains(id))
            .ToArray() ?? Array.Empty<CreatureId>();

        return choice with { TargetIds = filtered };
    }
}
