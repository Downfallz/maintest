using DA.Game.Shared.Contracts.Catalog.Enums;
using DA.Game.Shared.Resources.Spells.Effects;

namespace DA.Game.Domain2.Catalog.ValueObjects.Spells;

public sealed record Bleed : Effect, IOverTimeEffect
{
    public int AmountPerTick { get; }
    public int DurationRounds { get; }

    private Bleed(int amountPerTick, int durationRounds, TargetingSpec targeting)
        : base(targeting)
    {
        AmountPerTick = amountPerTick;
        DurationRounds = durationRounds;
    }

    public static Bleed Of(int amountPerTick, int durationRounds, TargetingSpec targeting)
    {
        var res = Validate((amountPerTick > 0, "Bleed amount per tick must be > 0."),
                            (durationRounds > 0, "Bleed duration must be > 0 rounds."),
                            (targeting is not null, "Targeting required."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);
        // (Optionnel) Politique de bon sens : Bleed ne s’applique pas sur Self par défaut.
        // À déplacer dans un validator si tu préfères centraliser.
        // Require(targeting.Origin != TargetOrigin.Self, "Bleed on self is not allowed.");

        return new Bleed(amountPerTick, durationRounds, targeting);
    }

    /// <summary>Convenience: mono-cible ennemi, 1 tick/round (le cas le plus courant).</summary>
    public static Bleed EnemySingle(int amountPerTick, int durationRounds)
        => Of(amountPerTick, durationRounds, TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.Single, 1));

    /// <summary>Change le ciblage en gardant la charge utile (immutabilité).</summary>
    public Bleed WithTargeting(TargetingSpec targeting)
        => Of(AmountPerTick, DurationRounds, targeting);
}
