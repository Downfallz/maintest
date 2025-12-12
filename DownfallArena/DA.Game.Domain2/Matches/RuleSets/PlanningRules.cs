using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Policies.Planning;
using DA.Game.Domain2.Matches.Services.Planning;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Talents;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.RuleSets;

public sealed class PlanningRules(ITalentUnlockService talentUnlockService,
    ICombatTimelineBuilderService combatTimelineBuilderService,
    ISpeedChoicePolicy speedChoicePolicy)
{
    public CombatTimeline BuildTimeline(
        Team team1,
        IReadOnlyCollection<SpeedChoice> p1Speed,
        Team team2,
        IReadOnlyCollection<SpeedChoice> p2Speed)
    {
       return combatTimelineBuilderService.BuildFromSpeedChoices(team1, p1Speed, team2, p2Speed);
    }

    public Result ValidateSpellUnlock(CreaturePerspective creaturePerspective, TalentTree? tree, SpellUnlockChoice choice)
    {
        return talentUnlockService.ValidateSpellUnlock(creaturePerspective, tree, choice);
    }

    public Result ValidateSpeedChoice(CreaturePerspective creaturePerspective, SpeedChoice choice)
    {
        return speedChoicePolicy.EnsureCreatureCanPlaySpeedChoice(creaturePerspective, choice);
    }
}
