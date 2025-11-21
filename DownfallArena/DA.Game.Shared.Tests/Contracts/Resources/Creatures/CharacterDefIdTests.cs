using DA.Game.Shared.Contracts.Resources.Creatures;

namespace DA.Game.Shared.Tests.Contracts.Resources.Creatures;

public class CharacterDefIdTests
{
    [Fact]
    public void GivenValidId_WhenCreating_ThenStoresValue()
    {
        var id = "creature:warrior:v1";

        var result = CharacterDefId.New(id);

        Assert.Equal(id, result.Value);
    }

    [Fact]
    public void GivenCharacterDefId_WhenToString_ThenReturnsValue()
    {
        var id = CharacterDefId.New("creature:mage:v2");

        Assert.Equal("creature:mage:v2", id.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void GivenEmptyId_WhenCreating_ThenThrows(string value)
    {
        Assert.Throws<ArgumentException>(() => CharacterDefId.New(value));
    }

    [Theory]
    [InlineData("creature:warrior:1")]      // missing v
    [InlineData("creature:warrior:v")]      // no version number
    [InlineData("creature::v1")]            // empty name
    [InlineData(":warrior:v1")]             // empty category
    [InlineData("creature:warrior:v-1")]    // invalid version
    [InlineData("creature/warrior/v1")]     // invalid separators
    public void GivenInvalidFormat_WhenCreating_ThenThrows(string value)
    {
        Assert.Throws<FormatException>(() => CharacterDefId.New(value));
    }

    [Fact]
    public void GivenValidId_WhenParsing_ThenExtractsParts()
    {
        var id = CharacterDefId.New("creature:rogue:v3");

        var (category, name, version) = id.Parse();

        Assert.Equal("creature", category);
        Assert.Equal("rogue", name);
        Assert.Equal(3, version);
    }
}
