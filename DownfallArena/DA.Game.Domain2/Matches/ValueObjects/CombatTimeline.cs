using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Shared.RuleSets;
using DA.Game.Shared.Contracts.Matches.Enums;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record CombatTimeline
{
    public IReadOnlyList<ActivationSlot> Slots { get; }

    private CombatTimeline(IReadOnlyList<ActivationSlot> slots) => Slots = slots;

    public static CombatTimeline Empty => new(Array.Empty<ActivationSlot>());

    public static CombatTimeline FromSpeedChoices(
        Team team1,
        IReadOnlyCollection<SpeedChoice> p1Speed,
        Team team2,
        IReadOnlyCollection<SpeedChoice> p2Speed,
        RuleSet rules)
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
                    .SingleOrDefault(c => c.Id == choice.CharacterId);

                if (character is null)
                    throw new InvalidOperationException(
                        $"Character {choice.CharacterId} not found in team {owner}.");

                var initiative = character.CurrentInitiative;
                allSlots.Add(new ActivationSlot(
                    owner,
                    character,
                    choice.Speed,
                    initiative
                ));
            }
        }

        AddSlotsForTeam(team1, PlayerSlot.Player1, p1Speed);
        AddSlotsForTeam(team2, PlayerSlot.Player2, p2Speed);

        // Reproduit ton ancienne logique :
        // 1) Quick par Initiative DESC
        // 2) Standard par Initiative DESC
        var quick = allSlots
            .Where(s => s.Speed == Speed.Quick)
            .OrderByDescending(s => s.InitiativeValue);

        var standard = allSlots
            .Where(s => s.Speed == Speed.Standard)
            .OrderByDescending(s => s.InitiativeValue);

        var ordered = quick.Concat(standard).ToArray();

        return new CombatTimeline(ordered);
    }

    public ActivationSlot? NextAfter(int index)
        => index + 1 < Slots.Count ? Slots[index + 1] : null;

    public bool AllDead()
        => Slots.All(s => !s.CombatCharacter.IsAlive); // à adapter selon ton modèle
}
