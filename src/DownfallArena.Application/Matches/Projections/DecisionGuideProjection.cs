using DownfallArena.Application.Agents;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// Reads only a seat's visible board and uses independent rolls. Looking at advice cannot change a match
/// or discover an opponent's undeclared action (ADR 0078).
/// </summary>
public sealed class DecisionGuideProjection(IGameResources resources, RuleSet rules)
{
    public IReadOnlyList<SpellGuidance> Build(PlayerBoardState board, CreatureId? creature)
    {
        ArgumentNullException.ThrowIfNull(board);
        var actor = board.Allies.FirstOrDefault(ally => ally.Id == creature);
        if (actor is null)
        {
            return [];
        }

        var creatures = board.Allies.Concat(board.Enemies).ToList();
        return actor.KnownSpells.OrderBy(id => id.Value, StringComparer.Ordinal)
            .Select(id => Spell(board, actor, creatures, id)).ToList();
    }

    private SpellGuidance Spell(PlayerBoardState board, CreatureSnapshot actor, List<CreatureSnapshot> creatures, SpellId id)
    {
        var spell = resources.GetSpell(id);
        var intent = new CombatIntent(actor.Id, id);
        var check = IntentRules.ValidateIntent(board.Slot, intent, creatures, resources);
        var slot = board.Timeline.FirstOrDefault(one => one.Creature == actor.Id);
        var speed = slot?.Speed ?? board.SpeedChoices.FirstOrDefault(one => one.Creature == actor.Id)?.Speed;
        var position = board.Timeline.ToList().FindIndex(one => one.Creature == actor.Id);
        var targets = new List<TargetGuidance>();
        IReadOnlyList<EffectOutcome> caster = [];
        if (check.IsSuccess)
        {
            foreach (var target in TargetingRules.LegalTargets(actor, spell, creatures).Candidates)
            {
                var action = CombatAction.Bind(intent, [target]);
                var plain = ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.NotCritical, speed ?? Speed.Standard);
                var critical = ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.Critical, speed ?? Speed.Standard);
                targets.Add(new TargetGuidance(target, [.. plain.Outcomes.Where(effect => !effect.OnCaster)], [.. critical.Outcomes.Where(effect => !effect.OnCaster)]));
                caster = [.. plain.Outcomes.Where(effect => effect.OnCaster)];
            }
        }

        return new SpellGuidance(
            id, spell.Stats.Cost.Value, actor.Energy.Value - spell.Stats.Cost.Value,
            check.IsFailure ? check.Error.Message : null,
            ResolutionRules.CriticalChanceOf(actor, spell, Speed.Quick),
            ResolutionRules.CriticalChanceOf(actor, spell, Speed.Standard),
            speed, position < 0 ? null : position + 1, targets, caster);
    }
}
