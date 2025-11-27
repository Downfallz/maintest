using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed class CostPolicyV1 : ICostPolicy
{
    // -------------------------------
    // DOMAIN FAILURES (Dxxx)
    // -------------------------------
    private const string DOM_D301_NOT_ENOUGH_ENERGY =
        "D301 - Actor does not have enough energy to perform this combat action.";

    public Result EnsureCreatureHasEnoughEnergy(
        CreaturePerspective ctx,
        CombatActionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(choice);

        var spellCost = choice.SpellRef.EnergyCost;
        var actorEnergy = ctx.Actor.Energy;

        if (actorEnergy < spellCost)
            return Result.Fail(DOM_D301_NOT_ENOUGH_ENERGY);

        return Result.Ok();
    }
}
