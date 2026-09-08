using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Resources;

public sealed class SpellTests
{
    [Fact]
    public void A_spell_keeps_its_definition()
    {
        var spell = Content.Spell("spell:fireball:v2", Damage.Of(4), Bleed.Of(1, 2));

        spell.Id.ShouldBe(SpellId.Parse("spell:fireball:v2"));
        spell.Name.ShouldBe("Strike");
        spell.Type.ShouldBe(SpellType.Offensive);
        spell.CreatureClass.ShouldBe(CreatureClass.Creature);
        spell.Stats.ShouldBe(new SpellStats(Initiative.Of(1), Energy.Of(0), CriticalChance.None));
        spell.Targeting.ShouldBe(TargetingSpec.SingleTarget(TargetOrigin.Enemy));
        spell.Effects.ShouldBe([Damage.Of(4), Bleed.Of(1, 2)]);
    }

    [Fact]
    public void A_spell_needs_a_name_and_at_least_one_effect()
    {
        Should.Throw<ArgumentException>(() => Spell.Create(
            SpellId.Parse("spell:x:v1"), " ", SpellType.Passive, CreatureClass.Creature,
            new SpellStats(Initiative.Of(0), Energy.Of(0), CriticalChance.None), TargetingSpec.SingleTarget(TargetOrigin.Self), [Damage.Of(1)]));

        Should.Throw<ArgumentException>(() => Spell.Create(
            SpellId.Parse("spell:x:v1"), "X", SpellType.Passive, CreatureClass.Creature,
            new SpellStats(Initiative.Of(0), Energy.Of(0), CriticalChance.None), TargetingSpec.SingleTarget(TargetOrigin.Self), []));
    }

    [Fact]
    public void The_effect_list_is_copied()
    {
        var effects = new List<Effect> { Damage.Of(1) };

        var spell = Content.Spell("spell:x:v1", [.. effects]);
        effects.Add(Heal.Of(1));

        spell.Effects.Count.ShouldBe(1);
    }
}
