using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed class TargetingPolicyV1 : ITargetingPolicy
{
    // -------------------------------
    // DOMAIN FAILURES (D4xx)
    // -------------------------------
    private const string DOM_D401_TARGET_NOT_FOUND =
        "D401 - Target not found in match.";

    private const string DOM_D402_NO_TARGETS_PROVIDED =
        "D402 - This spell requires at least one target.";

    private const string DOM_D403_TOO_MANY_TARGETS =
        "D403 - Too many targets provided for this spell.";

    private const string DOM_D404_SELF_TARGET_REQUIRED =
        "D404 - Self-target spell must target the acting creature only.";

    private const string DOM_D405_ALLIES_ONLY =
        "D405 - All targets must be allies.";

    private const string DOM_D406_ENEMIES_ONLY =
        "D406 - All targets must be enemies.";

    private const string DOM_D407_SINGLE_TARGET_REQUIRED =
        "D407 - This spell must target exactly one creature.";

    private const string DOM_D408_AT_LEAST_ONE_TARGET_REQUIRED =
        "D408 - This spell must target at least one creature.";

    private const string DOM_D409_TARGET_DEAD =
        "D409 - Target creature is dead and cannot be targeted.";

    public Result<TargetingCheckReport> EnsureCombatActionHasValidTargets(
        CreaturePerspective ctx,
        CombatActionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(choice);

        var spell = choice.SpellRef;
        var targeting = spell.TargetingSpec;

        var targetIds = choice.TargetIds ?? Array.Empty<CreatureId>();
        var failures = new List<TargetingFailure>();

        var creaturesById = ctx.Creatures.ToDictionary(c => c.CharacterId);

        // 1) Existence des cibles
        foreach (var targetId in targetIds)
        {
            if (!creaturesById.ContainsKey(targetId))
            {
                failures.Add(new TargetingFailure(
                    TargetId: targetId,
                    ErrorCode: "D401",
                    Message: DOM_D401_TARGET_NOT_FOUND));
            }
        }

        // Ne garder que les cibles existantes
        var targets = targetIds
            .Where(id => creaturesById.TryGetValue(id, out _))
            .Select(id => creaturesById[id])
            .ToList();

        // 2) Cibles mortes → une erreur par target morte
        var deadTargets = targets
            .Where(t => t.IsDead)
            .Select(t => t.CharacterId)
            .ToList();

        foreach (var id in deadTargets)
        {
            failures.Add(new TargetingFailure(
                TargetId: id,
                ErrorCode: "D409",
                Message: DOM_D409_TARGET_DEAD));
        }

        // 3) Pas de cibles alors que le sort en exige
        var requiresAtLeastOneTarget =
            targeting.MaxTargets is null || targeting.MaxTargets > 0;

        if (requiresAtLeastOneTarget && targets.Count == 0)
        {
            failures.Add(new TargetingFailure(
                TargetId: null,
                ErrorCode: "D402",
                Message: DOM_D402_NO_TARGETS_PROVIDED));
        }

        // 4) Trop de cibles (on regarde le nombre de TargetIds fournis)
        if (targeting.MaxTargets is not null &&
            targetIds.Count > targeting.MaxTargets.Value)
        {
            failures.Add(new TargetingFailure(
                TargetId: null,
                ErrorCode: "D403",
                Message: DOM_D403_TOO_MANY_TARGETS));
        }

        // 5) Origin: Self / Ally / Enemy / Any
        switch (targeting.Origin)
        {
            case TargetOrigin.Self:
                {
                    if (targets.Count != 1 || targets[0].CharacterId != ctx.Actor.CharacterId)
                    {
                        failures.Add(new TargetingFailure(
                            TargetId: null,
                            ErrorCode: "D404",
                            Message: DOM_D404_SELF_TARGET_REQUIRED));
                    }
                    break;
                }

            case TargetOrigin.Ally:
                {
                    var invalidAllies = targets
                        .Where(t => t.OwnerSlot != ctx.Actor.OwnerSlot)
                        .Select(t => t.CharacterId)
                        .ToList();

                    foreach (var id in invalidAllies)
                    {
                        failures.Add(new TargetingFailure(
                            TargetId: id,
                            ErrorCode: "D405",
                            Message: DOM_D405_ALLIES_ONLY));
                    }
                    break;
                }

            case TargetOrigin.Enemy:
                {
                    var invalidEnemies = targets
                        .Where(t => t.OwnerSlot == ctx.Actor.OwnerSlot)
                        .Select(t => t.CharacterId)
                        .ToList();

                    foreach (var id in invalidEnemies)
                    {
                        failures.Add(new TargetingFailure(
                            TargetId: id,
                            ErrorCode: "D406",
                            Message: DOM_D406_ENEMIES_ONLY));
                    }
                    break;
                }

            case TargetOrigin.Any:
            default:
                break;
        }

        // 6) Scope
        switch (targeting.Scope)
        {
            case TargetScope.SingleTarget:
                if (targetIds.Count != 1)
                {
                    failures.Add(new TargetingFailure(
                        TargetId: null,
                        ErrorCode: "D407",
                        Message: DOM_D407_SINGLE_TARGET_REQUIRED));
                }
                break;

            case TargetScope.Multi:
                if (targetIds.Count < 1)
                {
                    failures.Add(new TargetingFailure(
                        TargetId: null,
                        ErrorCode: "D408",
                        Message: DOM_D408_AT_LEAST_ONE_TARGET_REQUIRED));
                }
                break;

            default:
                break;
        }

        // 7) Return
        if (failures.Count == 0)
        {
            var reportOk = new TargetingCheckReport(Array.Empty<TargetingFailure>());
            return Result<TargetingCheckReport>.Ok(reportOk);
        }

        var failureReport = new TargetingCheckReport(failures);

        return Result<TargetingCheckReport>.Ok(failureReport);
    }
}
