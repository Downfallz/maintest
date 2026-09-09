using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Domain.Tests.Resources;

public sealed class EffectTests
{
    [Fact]
    public void Instant_effects_carry_a_positive_amount()
    {
        Damage.Of(3).Amount.ShouldBe(3);
        Heal.Of(2).Amount.ShouldBe(2);
        EnergyGain.Of(1).Amount.ShouldBe(1);

        Should.Throw<ArgumentOutOfRangeException>(() => Damage.Of(0));
        Should.Throw<ArgumentOutOfRangeException>(() => Heal.Of(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => EnergyGain.Of(0));
    }

    [Fact]
    public void Bleed_and_stun_last_a_number_of_rounds_and_refresh_by_default()
    {
        var bleed = Bleed.Of(2, rounds: 3);
        var stun = Stun.For(1);

        bleed.AmountPerRound.ShouldBe(2);
        bleed.Duration.ShouldBe(Duration.OfRounds(3));
        bleed.Stacking.ShouldBe(StackingPolicy.Refresh);
        stun.Duration.Rounds.ShouldBe(1);
        stun.Stacking.ShouldBe(StackingPolicy.Refresh);

        Should.Throw<ArgumentOutOfRangeException>(() => Bleed.Of(0, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => Bleed.Of(1, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => Stun.For(0));
    }

    /// <summary>ADR 0019: the healing counterpart of a bleed, and the same shape.</summary>
    [Fact]
    public void Regeneration_lasts_a_number_of_rounds_and_refreshes_by_default()
    {
        var regeneration = Regeneration.Of(2, rounds: 3);

        regeneration.AmountPerRound.ShouldBe(2);
        regeneration.Duration.ShouldBe(Duration.OfRounds(3));
        regeneration.Stacking.ShouldBe(StackingPolicy.Refresh);
        Regeneration.Of(1, 2, StackingPolicy.Stack).Stacking.ShouldBe(StackingPolicy.Stack);

        Should.Throw<ArgumentOutOfRangeException>(() => Regeneration.Of(0, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => Regeneration.Of(1, 0));
    }

    [Fact]
    public void Buffs_and_debuffs_can_be_permanent_and_stack_by_default()
    {
        var buff = DefenseBuff.Of(2, Duration.Permanent);
        var debuff = InitiativeDebuff.Of(1, Duration.OfRounds(2), StackingPolicy.Ignore);

        buff.Amount.ShouldBe(2);
        buff.Duration.IsPermanent.ShouldBeTrue();
        buff.Stacking.ShouldBe(StackingPolicy.Stack);
        debuff.Duration.IsPermanent.ShouldBeFalse();
        debuff.Stacking.ShouldBe(StackingPolicy.Ignore);

        Should.Throw<ArgumentOutOfRangeException>(() => DefenseBuff.Of(0, Duration.Permanent));
        Should.Throw<ArgumentOutOfRangeException>(() => InitiativeDebuff.Of(0, Duration.Permanent));
    }

    [Fact]
    public void Effects_are_compared_by_value()
    {
        Damage.Of(3).ShouldBe(Damage.Of(3));
        Damage.Of(3).ShouldNotBe(Damage.Of(4));
        Bleed.Of(1, 2).ShouldBe(Bleed.Of(1, 2));
        ((object)Damage.Of(1)).Equals(Heal.Of(1)).ShouldBeFalse();
    }

    [Fact]
    public void A_duration_renders_readably()
    {
        Duration.OfRounds(2).ToString().ShouldBe("2 rounds");
        Duration.Permanent.ToString().ShouldBe("permanent");
        Should.Throw<ArgumentOutOfRangeException>(() => Duration.OfRounds(0));
    }
}
