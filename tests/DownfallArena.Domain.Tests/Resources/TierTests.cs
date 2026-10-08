using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Resources;

public sealed class TierTests
{
    private static readonly TierId Brute = TierId.Parse("tier:brute:v1");
    private static readonly TierId Marauder = TierId.Parse("tier:marauder:v1");
    private static readonly SpellId Pummel = SpellId.Parse("spell:pummel:v1");
    private static readonly SpellId Guard = SpellId.Parse("spell:guard:v1");

    [Fact]
    public void A_tier_keeps_its_package_and_its_one_bonus()
    {
        var tier = Tier.Create(Marauder, "Marauder", 2, [Brute], [Pummel, Guard], Initiative.Of(3));

        tier.Id.ShouldBe(Marauder);
        tier.Name.ShouldBe("Marauder");
        tier.Level.ShouldBe(2);
        tier.Prerequisites.ShouldBe([Brute]);
        tier.Spells.ShouldBe([Pummel, Guard]);
        tier.InitiativeBonus.ShouldBe(Initiative.Of(3));
    }

    /// <summary>
    /// A package nobody would buy is a package that should not exist. The check matters because the migration
    /// splits authored nodes, and a split that drops every spell on one side would otherwise pass quietly.
    /// </summary>
    [Fact]
    public void A_tier_that_teaches_nothing_is_refused()
    {
        Should.Throw<ArgumentException>(() => Tier.Create(Brute, "Brute", 1, [], [], Initiative.Of(1)));
    }

    /// <summary>ADR 0100: a capstone teaches nothing and is bought for its passive, which is something to buy.</summary>
    [Fact]
    public void A_tier_that_teaches_nothing_but_gives_a_passive_is_a_package()
    {
        var tier = Tier.Create(Brute, "Titan", 4, [], [], Initiative.Of(0), [Marauder], Passive.Of(stunImmunity: true));

        tier.Spells.ShouldBeEmpty();
        tier.Passive.StunImmunity.ShouldBeTrue();
        tier.AnyOf.ShouldBe([Marauder]);
        Should.Throw<ArgumentException>(() => Tier.Create(Brute, "Brute", 1, [], [], Initiative.Of(1), passive: Passive.None));
    }

    /// <summary>ADR 0100: an any-of list opens the tier with one of its packages; the all-of list still needs every one.</summary>
    [Fact]
    public void An_any_of_prerequisite_opens_with_one_of_its_packages_beside_every_required_one()
    {
        var gladiator = TierId.Parse("tier:gladiator:v1");
        var capstone = Tier.Create(TierId.Parse("tier:titan:v1"), "Titan", 4, [Brute], [], Initiative.Of(0), [Marauder, gladiator], Passive.Of(stunImmunity: true));

        capstone.IsOpenTo(new HashSet<TierId> { Brute, gladiator }.Contains).ShouldBeTrue();
        capstone.IsOpenTo(new HashSet<TierId> { Brute }.Contains).ShouldBeFalse("none of the any-of list is owned");
        capstone.IsOpenTo(new HashSet<TierId> { Marauder }.Contains).ShouldBeFalse("the required one is not owned");
        Tier.Create(Brute, "Brute", 1, [], [Pummel], Initiative.Of(1)).IsOpenTo(_ => false).ShouldBeTrue("a tier naming nothing is open to anyone");
    }

    [Fact]
    public void An_any_of_list_that_names_the_tier_repeats_itself_or_repeats_a_required_one_is_refused()
    {
        Should.Throw<ArgumentException>(() => Tier.Create(Marauder, "Marauder", 2, [], [Pummel], Initiative.Of(1), [Marauder]));
        Should.Throw<ArgumentException>(() => Tier.Create(Marauder, "Marauder", 2, [], [Pummel], Initiative.Of(1), [Brute, Brute]));
        Should.Throw<ArgumentException>(() => Tier.Create(Marauder, "Marauder", 2, [Brute], [Pummel], Initiative.Of(1), [Brute]));
    }

    [Fact]
    public void Passives_combine_an_immunity_once_and_their_amounts_added()
    {
        var held = Passive.Of(stunImmunity: true, upkeepEnergy: 1).With(Passive.Of(upkeepEnergy: 2, damageBonus: 2)).With(Passive.Of(stunImmunity: true));

        held.ShouldBe(Passive.Of(stunImmunity: true, upkeepEnergy: 3, damageBonus: 2));
        Passive.None.GivesAnything().ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => Passive.Of(upkeepEnergy: -1));
        Should.Throw<ArgumentOutOfRangeException>(() => Passive.Of(damageBonus: -1));
    }

    [Fact]
    public void A_tier_refuses_to_list_a_spell_or_a_prerequisite_twice()
    {
        Should.Throw<ArgumentException>(() => Tier.Create(Brute, "Brute", 1, [], [Pummel, Pummel], Initiative.Of(1)));
        Should.Throw<ArgumentException>(() => Tier.Create(Marauder, "Marauder", 2, [Brute, Brute], [Pummel], Initiative.Of(1)));
    }

    [Fact]
    public void A_tier_cannot_require_itself()
    {
        Should.Throw<ArgumentException>(() => Tier.Create(Brute, "Brute", 1, [Brute], [Pummel], Initiative.Of(1)));
    }

    [Fact]
    public void A_tier_sits_at_a_level_of_at_least_one()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Tier.Create(Brute, "Brute", 0, [], [Pummel], Initiative.Of(1)));
    }

    /// <summary>
    /// The reason <see cref="TierId"/> exists rather than reusing <see cref="SpellId"/>: a purchase and a cast
    /// are different things, and the learning encoders currently reach for a spell index when they mean a
    /// purchase. Two kinds that cannot be mistaken for each other is what stops that.
    /// </summary>
    [Fact]
    public void A_tier_identity_is_not_a_spell_identity()
    {
        TierId.Kind.ShouldBe("tier");
        TierId.TryParse("spell:pummel:v1", out _).ShouldBeFalse();
        SpellId.TryParse("tier:brute:v1", out _).ShouldBeFalse();
    }

    /// <summary>A package worth no initiative is authorable: the migration baseline hands one to Shaman.</summary>
    [Fact]
    public void A_tier_may_be_worth_no_initiative()
    {
        Tier.Create(Brute, "Brute", 1, [], [Pummel], Initiative.Of(0)).InitiativeBonus.ShouldBe(Initiative.Of(0));
    }
}
