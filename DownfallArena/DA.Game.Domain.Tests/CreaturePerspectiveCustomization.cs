using AutoFixture;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DA.Game.Domain2.Tests.Customizations;

public sealed class CreaturePerspectiveCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<CreaturePerspective>(composer =>
            composer
                .FromFactory(() =>
                {
                    // 1) Create a REAL CombatCreature as actor
                    var actorCreature = fixture.Create<CombatCreature>();
                    var actorSnapshot = CreatureSnapshot.From(actorCreature);
                    var actorId = actorCreature.Id;

                    // 2) Create a couple of other creatures
                    var other1 = CreatureSnapshot.From(fixture.Create<CombatCreature>());
                    var other2 = CreatureSnapshot.From(fixture.Create<CombatCreature>());

                    var creatures = new List<CreatureSnapshot>
                    {
                        actorSnapshot,
                        other1,
                        other2
                    };

                    // 3) No combat actions yet
                    var emptyActions = new ReadOnlyDictionary<CreatureId, CombatActionChoice>(
                        new Dictionary<CreatureId, CombatActionChoice>());

                    // 4) Final perspective
                    return new CreaturePerspective(
                        ActorId: actorId,
                        Creatures: creatures,
                        State: MatchState.Started,
                        Phase: RoundPhase.Combat,
                        Player1Choices: null,
                        Player2Choices: null,
                        Player1SpeedChoices: null,
                        Player2SpeedChoices: null,
                        CombatActionChoices: emptyActions,
                        Timeline: null,
                        RevealCursor: null,
                        ResolveCursor: null
                    );
                })
                // IMPORTANT: do not let AutoFixture overwrite properties afterward
                .OmitAutoProperties());
    }
}
