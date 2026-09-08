using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using DA.Game.Domain2.Tests.Customizations;
using DA.Game.Shared.Tests;

namespace DA.Game.Domain.Tests;

public sealed class MatchAutoDataAttribute : AutoDataAttribute
{
    public MatchAutoDataAttribute()
        : base(() =>
        {
            var fixture = new Fixture()
                .Customize(new AutoMoqCustomization
                {
                    ConfigureMembers = true
                });

            // Shared-level customizations
            fixture.Customize(new GameResourcesCustomization());
            fixture.Customize(new SpellEffectCustomization());

            // Domain2-level customizations
            fixture.Customize(new CreaturePerspectiveCustomization());

            return fixture;
        })
    {
    }
}
