using AutoFixture;
using AutoFixture.Xunit2;

namespace DA.Game.Shared.Tests;

internal sealed class GameAutoDataAttribute : AutoDataAttribute
{
    public GameAutoDataAttribute()
        : base(() =>
        {
            var fixture = new Fixture();
            fixture.Customize(new GameResourcesCustomization());
            fixture.Customize(new SpellEffectCustomization());
            return fixture;
        })
    {
    }
}