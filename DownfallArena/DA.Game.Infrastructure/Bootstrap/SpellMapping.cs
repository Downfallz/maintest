using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Json;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Stats;
namespace DA.Game.Infrastructure.Bootstrap;

public static class SpellMapping
{

    public static Effect ToRef(this EffectDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.Kind switch
        {
            EffectKind.Bleed => dto.ToBleedRef(),
            EffectKind.Damage => dto.ToDamageRef(),
            // EffectKind.Heal   => dto.ToHealDomain(),
            _ => throw new NotSupportedException(
                $"Unsupported effect kind '{dto.Kind}'.")
        };
    }


    private static Bleed ToBleedRef(this EffectDto dto)
    {
        // helpers pour éviter les null
        static int Req(int? value, string name)
            => value ?? throw new InvalidOperationException(
                $"Bleed requires '{name}'.");

        var amountPerTick = Req(dto.AmountPerTick, nameof(dto.AmountPerTick));
        var durationRounds = Req(dto.DurationRounds, nameof(dto.DurationRounds));

        return Bleed.Of(
            amountPerTick,
            durationRounds
        );
    }
    private static Damage ToDamageRef(this EffectDto dto)
    {
        // helpers pour éviter les null
        static int Req(int? value, string name)
            => value ?? throw new InvalidOperationException(
                $"Damage requires '{name}'.");

        var amount = Req(dto.Amount, nameof(dto.Amount));

        return Damage.Of(amount
        );
    }
    // ---------- Targeting

    public static TargetingSpec ToRef(this TargetingSpecDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return TargetingSpec.Of(dto.Origin, dto.Scope, dto.MaxTargets);
    }


    public static Spell ToRef(this SpellDefinitionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new Spell(SpellId.New(dto.Id)
            , dto.Name
            , dto.SpellType
            , dto.CreatureClass
            , Initiative.Of(dto.Initiative)
            , Energy.Of(dto.EnergyCost)
            , CriticalChance.Of(dto.CriticalChance)
            , dto.Targeting.ToRef()
            , dto.Effects.Select(e => e.ToRef()).ToArray()
        );

    }
}
