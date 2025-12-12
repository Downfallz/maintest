using AutoFixture.Xunit2;
using DA.Game.Domain.Tests; // MatchAutoDataAttribute
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Shared.Contracts.Resources.Spells;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace DA.Game.Domain2.Tests.Matches.Policies.Combat;

public sealed class UnlockedPolicyV1Tests
{
    private const string DX01_ERROR_MESSAGE =
        "DX01 - Actor cannot perform this combat action as this spell is locked.";

    // ------------------------------------------------------------
    // SUCCESS: spell Id is in KnownSpellIds
    // ------------------------------------------------------------
    [Theory]
    [MatchAutoData]
    public void EnsureCreatureHasUnlockedSpell_WhenSpellIsKnown_ShouldReturnOk(
        CreaturePerspective ctx,
        Spell spell,
        UnlockedPolicyV1 sut)
    {
        // Arrange
        // On force le Spell.Id pour matcher un des KnownSpellIds du contexte
        var anyKnownId = ctx.Actor.KnownSpellIds.First();

        var spellWithKnownId = spell with
        {
            Id = anyKnownId
        };

        // Act
        var result = sut.EnsureCreatureHasUnlockedSpell(ctx, spellWithKnownId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
        result.IsInvariant.Should().BeFalse();
    }

    // ------------------------------------------------------------
    // FAILURE: spell Id NOT in KnownSpellIds
    // ------------------------------------------------------------
    [Theory]
    [MatchAutoData]
    public void EnsureCreatureHasUnlockedSpell_WhenSpellIsNotKnown_ShouldReturnFailure(
        CreaturePerspective ctx,
        Spell spell,
        UnlockedPolicyV1 sut)
    {
        // Arrange
        // On génère un SpellId qui n'est PAS dans KnownSpellIds
        SpellId lockedId;
        do
        {
            lockedId = SpellId.New($"spell:{Guid.NewGuid():N}:v1");
        }
        while (ctx.Actor.KnownSpellIds.Contains(lockedId));

        var lockedSpell = spell with
        {
            Id = lockedId
        };

        // Act
        var result = sut.EnsureCreatureHasUnlockedSpell(ctx, lockedSpell);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DX01_ERROR_MESSAGE);
        result.IsInvariant.Should().BeFalse();
    }

    // ------------------------------------------------------------
    // GUARD: ctx null
    // ------------------------------------------------------------
    [Theory]
    [MatchAutoData]
    public void EnsureCreatureHasUnlockedSpell_WhenContextIsNull_ShouldThrowArgumentNullException(
        Spell spell,
        UnlockedPolicyV1 sut)
    {
        // Act
        var act = () => sut.EnsureCreatureHasUnlockedSpell(null!, spell);

        // Assert
        act.Should()
           .Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("ctx");
    }

    // ------------------------------------------------------------
    // GUARD: spell null
    // ------------------------------------------------------------
    [Theory]
    [MatchAutoData]
    public void EnsureCreatureHasUnlockedSpell_WhenSpellIsNull_ShouldThrowArgumentNullException(
        CreaturePerspective ctx,
        UnlockedPolicyV1 sut)
    {
        // Act
        var act = () => sut.EnsureCreatureHasUnlockedSpell(ctx, null!);

        // Assert
        act.Should()
           .Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("spell");
    }
}
