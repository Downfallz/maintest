using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record CombatTimeline
{
    public IReadOnlyList<ActivationSlot> Slots { get; }

    private CombatTimeline(IReadOnlyList<ActivationSlot> slots) => Slots = slots;
    public static CombatTimeline Empty => new(Array.Empty<ActivationSlot>());

    public static CombatTimeline FromTeams(Team team1, Team team2, RuleSet rules)
    {
        // construit l’ordre d’activation selon vitesse et alternance
        throw new NotImplementedException();
    }

    public ActivationSlot? NextAfter(int index)
        => index + 1 < Slots.Count ? Slots[index + 1] : null;

    public bool AllDead()
        => Slots.All(s => !s.Character.IsAlive);
}
