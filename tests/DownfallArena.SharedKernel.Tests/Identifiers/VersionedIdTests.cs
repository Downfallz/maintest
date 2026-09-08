using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.SharedKernel.Tests.Identifiers;

public sealed class VersionedIdTests
{
    [Fact]
    public void A_spell_id_parses_its_kind_name_and_version()
    {
        var id = SpellId.Parse("spell:heavy_strike:v2");

        id.Value.ShouldBe("spell:heavy_strike:v2");
        id.Name.ShouldBe("heavy_strike");
        id.Version.ShouldBe(2);
        id.ToString().ShouldBe("spell:heavy_strike:v2");
    }

    [Fact]
    public void A_creature_definition_id_requires_the_creature_kind()
    {
        CreatureDefinitionId.Parse("creature:main:v1").Name.ShouldBe("main");

        Should.Throw<FormatException>(() => CreatureDefinitionId.Parse("spell:main:v1"));
    }

    [Fact]
    public void A_talent_tree_id_requires_the_talent_tree_kind()
    {
        TalentTreeId.Parse("talent-tree:base_creature:v1").Version.ShouldBe(1);

        Should.Throw<FormatException>(() => TalentTreeId.Parse("creature:base_creature:v1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pummel")]
    [InlineData("spell:pummel")]
    [InlineData("spell:pummel:1")]
    [InlineData("spell:pummel:v")]
    [InlineData("spell:pum mel:v1")]
    [InlineData("creature:pummel:v1")]
    [InlineData("spell:pummel:v1:extra")]
    public void Malformed_spell_ids_are_rejected(string text)
    {
        SpellId.TryParse(text, out var id).ShouldBeFalse();
        id.ShouldBeNull();
        Should.Throw<FormatException>(() => SpellId.Parse(text));
    }

    [Fact]
    public void A_null_text_is_not_a_spell_id()
    {
        SpellId.TryParse(null, out var id).ShouldBeFalse();
        id.ShouldBeNull();
    }

    [Fact]
    public void A_version_too_large_to_be_an_integer_is_rejected()
    {
        SpellId.TryParse("spell:pummel:v99999999999", out _).ShouldBeFalse();
    }

    [Fact]
    public void Ids_with_the_same_value_are_equal()
    {
        SpellId.Parse("spell:pummel:v1").ShouldBe(SpellId.Parse("spell:pummel:v1"));
        SpellId.Parse("spell:pummel:v1").ShouldNotBe(SpellId.Parse("spell:pummel:v2"));
    }

    [Fact]
    public void Ids_of_different_kinds_are_never_equal_even_with_the_same_name()
    {
        object spell = SpellId.Parse("spell:main:v1");
        object creature = CreatureDefinitionId.Parse("creature:main:v1");

        spell.Equals(creature).ShouldBeFalse();
    }
}
