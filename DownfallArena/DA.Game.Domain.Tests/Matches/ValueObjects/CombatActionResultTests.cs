using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Ids;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Tests.Matches.ValueObjects;

public class CombatActionResultTests
{
    [Fact]
    public void Constructor_WhenInstantEffectsIsNull_ShouldSetEmptyList()
    {
        // Arrange
        var choice = new CombatActionChoice(
            ActorId: new CreatureId(1),
            SpellRef: null!,
            TargetIds: Array.Empty<CreatureId>()
        );

        // Act
        var result = new CombatActionResult(
            choice,
            instantEffects: null,
            critical: CritComputationResult.Normal(0.0, 0.0)
        );

        // Assert
        result.InstantEffects.Should().NotBeNull();
        result.InstantEffects.Should().BeEmpty();
    }
}
