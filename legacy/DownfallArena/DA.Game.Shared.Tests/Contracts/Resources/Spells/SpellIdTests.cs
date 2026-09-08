using DA.Game.Shared.Contracts.Resources.Spells;

namespace DA.Game.Shared.Tests.Contracts.Resources.Spells;

public class SpellIdTests
{
    [Fact]
    public void GivenValidId_WhenCreating_ThenStoresValue()
    {
        var id = "spell:fire_bolt:v1";

        var result = SpellId.New(id);

        Assert.Equal(id, result.Name);
    }

    [Fact]
    public void GivenSpellId_WhenToString_ThenReturnsValue()
    {
        var id = SpellId.New("spell:ice_spear:v2");

        Assert.Equal("spell:ice_spear:v2", id.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GivenEmptyId_WhenCreating_ThenThrows(string value)
    {
        Assert.Throws<ArgumentException>(() => SpellId.New(value));
    }

    [Theory]
    [InlineData("fire_bolt:v1")]           // missing 'spell:'
    [InlineData("spell:fire_bolt:1")]      // missing 'v'
    [InlineData("spell:fire_bolt:v")]      // no number
    [InlineData("spell::v1")]              // empty name
    [InlineData(":fire_bolt:v1")]          // empty category (should always be spell)
    [InlineData("spell/fire_bolt/v1")]     // wrong separators
    [InlineData("spell:fire:bolt:v1")]     // too many segments
    public void GivenInvalidFormat_WhenCreating_ThenThrows(string value)
    {
        Assert.Throws<FormatException>(() => SpellId.New(value));
    }

    [Fact]
    public void GivenValidId_WhenParsing_ThenExtractsParts()
    {
        var id = SpellId.New("spell:heal_major:v3");

        var (category, name, version) = id.Parse();

        Assert.Equal("spell", category);
        Assert.Equal("heal_major", name);
        Assert.Equal(3, version);
    }

    [Fact]
    public void GivenTwoEqualSpellIds_WhenComparing_ThenTheyAreEqual()
    {
        var a = SpellId.New("spell:slash:v1");
        var b = SpellId.New("spell:slash:v1");

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void GivenTwoDifferentSpellIds_WhenComparing_ThenTheyAreNotEqual()
    {
        var a = SpellId.New("spell:slash:v1");
        var b = SpellId.New("spell:slash:v2");

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }
}
