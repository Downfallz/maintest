using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.Combat;

/// <summary>
/// Targeting policy V1: assumes the action already carries a single TargetId.
/// </summary>
public sealed class TargetingPolicyV1 : ITargetingPolicy
{

    public Result EnsureCombatActionHasValidTargets(CreaturePerspective ctx, CombatActionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(choice);

        var spell = choice.SpellRef;           // ou SkillRef selon ton modèle
        var targeting = spell.TargetingSpec;        // TargetingSpec
        if (choice.TargetIds is not null)
        {
            foreach (var targetId in choice.TargetIds)
            {
                if (!ctx.Creatures.Any(c => c.CharacterId == targetId))
                    return Result.Fail($"Target not found in match: {targetId}");
            }
        }
        var targets = (choice.TargetIds ?? [])
            .Select(id => ctx.Creatures.Single(c => c.CharacterId == id))
            .ToList();

        // 1) Aucun target fourni alors que le sort en exige ?
        if ((targets is null || targets.Count == 0) &&
            (targeting.MaxTargets is null || targeting.MaxTargets > 0))
        {
            return Result.Fail("This spell requires at least one target.");
        }

        // 2) Trop de cibles
        if (targeting.MaxTargets is not null &&
            targets!.Count > targeting.MaxTargets.Value)
        {
            return Result.Fail($"Too many targets (max {targeting.MaxTargets}).");
        }

        // 3) Vérifier allié / ennemi / self selon TargetOrigin
        switch (targeting.Origin)
        {
            case TargetOrigin.Self:
                if (targets!.Count != 1 || targets[0].CharacterId != ctx.Actor.CharacterId)
                    return Result.Fail("Self-target spell must target the acting creature only.");
                break;

            case TargetOrigin.Ally:
                if (targets!.Any(t => t.OwnerSlot != ctx.Actor.OwnerSlot))
                    return Result.Fail("All targets must be allies.");
                break;

            case TargetOrigin.Enemy:
                if (targets!.Any(t => t.OwnerSlot == ctx.Actor.OwnerSlot))
                    return Result.Fail("All targets must be enemies.");
                break;

            case TargetOrigin.Any:
            default:
                // rien de spécial ici
                break;
        }


        // 5) Option: scope (Single, All, etc.) si tu as un TargetScope
        // ex: si Scope == AllEnemies => targets doivent contenir *tous* les ennemis vivants, etc.
        switch (targeting.Scope)
        {
            case TargetScope.SingleTarget:
                if (targets!.Count != 1)
                    return Result.Fail("This spell must target exactly one creature.");
                break;

            case TargetScope.Multi:
                // À toi de décider si Multi = 1..N ou forcément >= 2.
                // Ici j’impose au moins 1, MaxTargets continue de gérer le plafond.
                if (targets!.Count < 1)
                    return Result.Fail("This spell must target at least one creature.");
                break;
        }
        return Result.Ok();
    }
}


