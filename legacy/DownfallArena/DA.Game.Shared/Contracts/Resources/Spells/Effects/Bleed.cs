using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Contracts.Resources.Spells.Effects;

public sealed record Bleed : Effect, IOverTimeEffect
{
    public int AmountPerTick { get; }
    public int DurationRounds { get; }

    private Bleed(int amountPerTick, int durationRounds)
        : base(EffectKind.Bleed)
    {
        AmountPerTick = amountPerTick;
        DurationRounds = durationRounds;
    }

    public static Bleed Of(int amountPerTick, int durationRounds)
    {
        var res = Validate((amountPerTick > 0, "Bleed amount per tick must be > 0."),
                            (durationRounds > 0, "Bleed duration must be > 0 rounds."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        // At this point, targeting is guaranteed not to be null.
        return new Bleed(amountPerTick, durationRounds);
    }
}
