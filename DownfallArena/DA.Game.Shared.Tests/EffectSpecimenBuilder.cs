using AutoFixture;
using AutoFixture.Kernel;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Tests;

public sealed class EffectSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is Type t && t == typeof(IEffect))
        {
            // 0 = Bleed, 1 = Damage
            var choice = Math.Abs(context.Create<int>()) % 2;

            return choice == 0
                ? CreateBleed(context)
                : CreateDamage(context);
        }

        return new NoSpecimen();
    }

    private static Bleed CreateBleed(ISpecimenContext context)
    {
        // amountPerTick ∈ [1, 5]
        var amountPerTick = Math.Abs(context.Create<int>()) % 5 + 1;

        // durationRounds ∈ [1, 4]
        var durationRounds = Math.Abs(context.Create<int>()) % 4 + 1;

        return Bleed.Of(amountPerTick, durationRounds);
    }

    private static Damage CreateDamage(ISpecimenContext context)
    {
        // amount ∈ [1, 10]
        var amount = Math.Abs(context.Create<int>()) % 10 + 1;

        return Damage.Of(amount);
    }
}


