using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.RuleSets;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Planning;

namespace DA.Game.Domain2.Matches.Services.Planning;

public interface ICombatTimelineBuilderService
{
    CombatTimeline BuildFromSpeedChoices(
        Team team1,
        IReadOnlyCollection<SpeedChoice> p1Speed,
        Team team2,
        IReadOnlyCollection<SpeedChoice> p2Speed);
}
