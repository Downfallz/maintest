using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Policies.Planning;
using DA.Game.Domain2.Matches.Services.Planning;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.RuleSets;

public sealed class PlanningRules(ICombatTimelineBuilderService combatTimelineBuilderService,
    IInitiativePolicy initiativePlicy)
{
    public CombatTimeline BuildTimeline(
        Team team1,
        IReadOnlyCollection<SpeedChoice> p1Speed,
        Team team2,
        IReadOnlyCollection<SpeedChoice> p2Speed)
    {
       return combatTimelineBuilderService.BuildFromSpeedChoices(team1, p1Speed, team2, p2Speed);
    }
}
