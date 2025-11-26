using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed class CostPolicyV1 : ICostPolicy
{
    public Result EnsureCreatureHasEnoughEnergy(CreaturePerspective ctx, CombatActionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(choice);

        var spellCost = choice.SpellRef.EnergyCost;
        var actorEnergy = ctx.Actor.Energy;
        if (actorEnergy < spellCost)
            return Result.Fail("Not enough energy");
        return Result.Ok();
    }
}
