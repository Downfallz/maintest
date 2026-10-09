using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Checks chosen targets against a spell's targeting spec and lists the legal candidates for a spell.
/// </summary>
public static class TargetingRules
{
    public static TargetingReport Check(
        CreatureSnapshot actor,
        Spell spell,
        IReadOnlyList<CreatureId> targets,
        IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(creatures);

        // The agents check every target set of every spell they read, nearly all of them legal: those are told
        // apart first, without building the list a failure needs.
        if (IsClean(actor, spell.Targeting, targets, creatures))
        {
            return TargetingReport.Clean;
        }

        var failures = GlobalFailures(actor, spell.Targeting, targets).ToList();
        foreach (var targetId in targets.Distinct())
        {
            failures.AddRange(TargetFailures(actor, spell.Targeting, targetId, creatures));
        }

        return failures.Count == 0 ? TargetingReport.Clean : new TargetingReport(failures);
    }

    public static LegalTargets LegalTargets(CreatureSnapshot actor, Spell spell, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(creatures);

        var spec = spell.Targeting;
        var living = creatures.Where(creature => creature.IsAlive);
        IReadOnlyList<CreatureId> candidates = spec.Origin switch
        {
            TargetOrigin.Self => actor.IsAlive ? [actor.Id] : [],
            TargetOrigin.Ally => [.. living.Where(creature => creature.Owner == actor.Owner).Select(creature => creature.Id)],
            TargetOrigin.Enemy => [.. living.Where(creature => creature.Owner != actor.Owner).Select(creature => creature.Id)],
            _ => [.. living.Select(creature => creature.Id)],
        };

        var max = spec.Scope == TargetScope.SingleTarget ? 1 : Math.Min(spec.MaxTargets ?? candidates.Count, candidates.Count);
        return new LegalTargets(1, Math.Max(1, max), candidates);
    }

    /// <summary>Whether <see cref="GlobalFailures"/> and every target's <see cref="TargetFailures"/> would find nothing.</summary>
    private static bool IsClean(CreatureSnapshot actor, TargetingSpec spec, IReadOnlyList<CreatureId> targets, IReadOnlyList<CreatureSnapshot> creatures)
    {
        if (targets.Count == 0
            || (spec.Scope == TargetScope.SingleTarget && targets.Count > 1)
            || (spec.MaxTargets is { } max && targets.Count > max))
        {
            return false;
        }

        for (var index = 0; index < targets.Count; index++)
        {
            if (Repeats(targets, index) || !IsCleanTarget(actor, spec, targets[index], creatures))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether the target at <paramref name="index"/> was already named before it.</summary>
    private static bool Repeats(IReadOnlyList<CreatureId> targets, int index)
    {
        for (var earlier = 0; earlier < index; earlier++)
        {
            if (targets[earlier] == targets[index])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether one target would draw no failure: <see cref="TargetFailures"/> and the self-only rule.</summary>
    private static bool IsCleanTarget(CreatureSnapshot actor, TargetingSpec spec, CreatureId targetId, IReadOnlyList<CreatureSnapshot> creatures)
    {
        if (spec.Origin == TargetOrigin.Self && targetId != actor.Id)
        {
            return false;
        }

        CreatureSnapshot? target = null;
        foreach (var creature in creatures)
        {
            if (creature.Id == targetId)
            {
                target = creature;
                break;
            }
        }

        return target is { IsDead: false }
            && !(spec.Origin == TargetOrigin.Ally && target.Owner != actor.Owner)
            && !(spec.Origin == TargetOrigin.Enemy && target.Owner == actor.Owner);
    }

    private static IEnumerable<TargetingFailure> GlobalFailures(CreatureSnapshot actor, TargetingSpec spec, IReadOnlyList<CreatureId> targets)
    {
        if (targets.Count == 0)
        {
            yield return new TargetingFailure(null, CombatErrors.NoTargets);
        }

        if (spec.Scope == TargetScope.SingleTarget && targets.Count > 1)
        {
            yield return new TargetingFailure(null, CombatErrors.ExactlyOneTarget);
        }
        else if (spec.MaxTargets is { } max && targets.Count > max)
        {
            yield return new TargetingFailure(null, CombatErrors.TooManyTargets);
        }

        if (targets.Distinct().Count() != targets.Count)
        {
            yield return new TargetingFailure(null, CombatErrors.DuplicateTargets);
        }

        if (spec.Origin == TargetOrigin.Self && targets.Any(target => target != actor.Id))
        {
            yield return new TargetingFailure(null, CombatErrors.SelfOnly);
        }
    }

    private static IEnumerable<TargetingFailure> TargetFailures(CreatureSnapshot actor, TargetingSpec spec, CreatureId targetId, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var target = creatures.FirstOrDefault(candidate => candidate.Id == targetId);
        if (target is null)
        {
            yield return new TargetingFailure(targetId, CombatErrors.UnknownTarget);
            yield break;
        }

        if (target.IsDead)
        {
            yield return new TargetingFailure(targetId, CombatErrors.TargetDead);
        }

        if (spec.Origin == TargetOrigin.Ally && target.Owner != actor.Owner)
        {
            yield return new TargetingFailure(targetId, CombatErrors.AlliesOnly);
        }

        if (spec.Origin == TargetOrigin.Enemy && target.Owner == actor.Owner)
        {
            yield return new TargetingFailure(targetId, CombatErrors.EnemiesOnly);
        }
    }
}
