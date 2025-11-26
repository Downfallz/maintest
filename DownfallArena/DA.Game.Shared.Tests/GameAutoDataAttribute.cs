using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;

namespace DA.Game.Shared.Tests;

public sealed class GameAutoDataAttribute : AutoDataAttribute
{
    public GameAutoDataAttribute()
        : base(() =>
        {
            var fixture = new Fixture()
            .Customize(new AutoMoqCustomization
             {
                 ConfigureMembers = true
             });
            fixture.Customize(new GameResourcesCustomization());
            fixture.Customize(new SpellEffectCustomization());
            return fixture;
        })
    {
    }
}