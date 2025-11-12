using DA.Game.Domain2.Catalog.ValueObjects.Spells.Effects.Base;

namespace DA.Game.Domain2.Catalog.ValueObjects.Spells.Effects;

public sealed record Bleed : Effect, IOverTimeEffect
{
    public int AmountPerTick { get; }
    public int DurationRounds { get; }

    private Bleed(int amountPerTick, int durationRounds, int tickIntervalRounds, TargetingSpec targeting)
        : base(targeting)
    {
        AmountPerTick = amountPerTick;
        DurationRounds = durationRounds;
    }

    public static Bleed Of(int amountPerTick, int durationRounds, TargetingSpec targeting, int tickIntervalRounds = 1)
    {
        var res = Validate((amountPerTick > 0, "Bleed amount per tick must be > 0."),
                            (durationRounds > 0, "Bleed duration must be > 0 rounds."),
                            (tickIntervalRounds > 0, "Tick interval must be > 0."),
                            (targeting is not null, "Targeting required."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);
        // (Optionnel) Politique de bon sens : Bleed ne s’applique pas sur Self par défaut.
        // À déplacer dans un validator si tu préfères centraliser.
        // Require(targeting.Origin != TargetOrigin.Self, "Bleed on self is not allowed.");

        return new Bleed(amountPerTick, durationRounds, tickIntervalRounds, targeting);
    }

    /// <summary>Convenience: mono-cible ennemi, 1 tick/round (le cas le plus courant).</summary>
    public static Bleed EnemySingle(int amountPerTick, int durationRounds, int tickIntervalRounds = 1)
        => Of(amountPerTick, durationRounds, TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.Single, 1), tickIntervalRounds);

    /// <summary>Change le ciblage en gardant la charge utile (immutabilité).</summary>
    public Bleed WithTargeting(TargetingSpec targeting)
        => Of(AmountPerTick, DurationRounds, targeting);
}
