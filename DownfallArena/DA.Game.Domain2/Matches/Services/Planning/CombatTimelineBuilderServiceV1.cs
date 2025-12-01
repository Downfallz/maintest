using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.RuleSets;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Domain2.Matches.Services.Planning;

public sealed class CombatTimelineBuilderServiceV1 : ICombatTimelineBuilderService
{
    public CombatTimeline BuildFromSpeedChoices(
        Team team1,
        IReadOnlyCollection<SpeedChoice> p1Speed,
        Team team2,
        IReadOnlyCollection<SpeedChoice> p2Speed)
    {
        ArgumentNullException.ThrowIfNull(team1);
        ArgumentNullException.ThrowIfNull(team2);
        ArgumentNullException.ThrowIfNull(p1Speed);
        ArgumentNullException.ThrowIfNull(p2Speed);

        var allSlots = new List<ActivationSlot>();

        void AddSlotsForTeam(Team team, PlayerSlot owner, IEnumerable<SpeedChoice> choices)
        {
            foreach (var choice in choices)
            {
                var character = team.Characters
                    .SingleOrDefault(c => c.Id == choice.CreatureId);

                if (character is null)
                    throw new InvalidOperationException(
                        $"Character {choice.CreatureId} not found in team {owner}.");

                // You keep your own rule here (base initiative or computed)
                var initiative = character.CurrentInitiative;

                allSlots.Add(new ActivationSlot(
                    owner,
                    character.Id,
                    choice.Speed,
                    initiative
                ));
            }
        }

        AddSlotsForTeam(team1, PlayerSlot.Player1, p1Speed);
        AddSlotsForTeam(team2, PlayerSlot.Player2, p2Speed);

        // 1) Quick by Initiative DESC
        // 2) Standard by Initiative DESC
        var quick = allSlots
            .Where(s => s.Speed == SkillSpeed.Quick)
            .OrderByDescending(s => s.InitiativeValue.Value);

        var standard = allSlots
            .Where(s => s.Speed == SkillSpeed.Standard)
            .OrderByDescending(s => s.InitiativeValue.Value);

        var ordered = quick.Concat(standard).ToArray();

        return CombatTimeline.FromSlots(ordered);
    }
}
