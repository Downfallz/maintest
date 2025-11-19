using DA.Game.Domain2.Catalog.Interfaces;
using DA.Game.Domain2.Shared.Resources.Enums;

namespace DA.Game.Domain2.Catalog.ValueObjects.Spells;

public sealed record Damage : Effect, IInstantEffect
{
    public int Amount { get; }

    private Damage(int amount, TargetingSpec targeting) : base(targeting) => Amount = amount;

    public static Damage Of(int amount, TargetingSpec targeting)
    {
        var res = Validate((amount > 0, "Damage amount must be > 0."),
                            (targeting is not null, "Targeting required."));

        // Optionnel : policy de bon sens (les dégâts sur Self ne sont pas autorisés par défaut)
        // -> à déplacer dans une ValidationPolicy si tu préfères centraliser.
        return new Damage(amount, targeting);
    }

    public static Damage SingleTargetEnemy(int amount)
        => Of(amount, TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.Single, 1));

    public Damage WithTargeting(TargetingSpec targeting) => Of(Amount, targeting);
}
