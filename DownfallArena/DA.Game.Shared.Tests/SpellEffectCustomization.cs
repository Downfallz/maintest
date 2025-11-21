using AutoFixture;

namespace DA.Game.Shared.Tests;

internal sealed class SpellEffectCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        fixture.Customizations.Add(new EffectSpecimenBuilder());
    }
}