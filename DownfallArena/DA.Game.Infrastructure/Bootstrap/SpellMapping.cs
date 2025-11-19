using DA.Game.Domain2.Catalog.Entities;
using DA.Game.Domain2.Catalog.ValueObjects.Spells;
using DA.Game.Domain2.Catalog.ValueObjects.Stats;
using DA.Game.Shared.Contracts.Catalog.Ids;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Resources.JsonDto;
using DA.Game.Shared.Resources.Spells;
using DA.Game.Shared.Utilities;
namespace DA.Game.Infrastructure.Bootstrap;

public static class SpellMapping
{
    public static Result<SpellDefinition> ToDomain(this SpellDefinitionDto dto, GameSchema schema)
    {
        // validations légères côté DTO
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<SpellDefinition>.Fail("Name is required.");
        if (dto.Effects is null || dto.Effects.Count == 0)
            return Result<SpellDefinition>.Fail("At least one starting spell is required.");


        // construction domaine (VO)
        var id = new SpellId(dto.Id);

        var energy = Energy.Of(dto.EnergyCost);
        var init = Initiative.Of(dto.Initiative);
        var crit = CriticalChance.Of(Percentage01.Of(dto.CriticalChance));

        var effects = dto.Effects.Select(g => g.ToRef());


        var spellDef = SpellDefinition.Create(id,
            dto.Name,
            dto.SpellType,
            dto.CharacterClass,
            init,
            energy,
            crit,
            effects);

        return Result<SpellDefinition>.Ok(spellDef);

    }
    public static Effect ToRef(this EffectDto dto)
      => dto.Kind switch
      {
          EffectKind.Bleed => dto.ToBleedRef(),
          EffectKind.Damage => dto.ToDamageRef(),
          // EffectKind.Heal   => dto.ToHealDomain(),
          _ => throw new NotSupportedException(
              $"Unsupported effect kind '{dto.Kind}'.")
      };

    private static Bleed ToBleedRef(this EffectDto dto)
    {
        if (dto.Targeting is null)
            throw new InvalidOperationException("Bleed requires Targeting.");

        // helpers pour éviter les null
        static int Req(int? value, string name)
            => value ?? throw new InvalidOperationException(
                $"Bleed requires '{name}'.");

        var amountPerTick = Req(dto.AmountPerTick, nameof(dto.AmountPerTick));
        var durationRounds = Req(dto.DurationRounds, nameof(dto.DurationRounds));

        var targeting = dto.Targeting.ToRef();

        return Bleed.Of(
            amountPerTick,
            durationRounds,
            targeting
        );
    }
    private static Damage ToDamageRef(this EffectDto dto)
    {
        if (dto.Targeting is null)
            throw new InvalidOperationException("Damage requires Targeting.");

        // helpers pour éviter les null
        static int Req(int? value, string name)
            => value ?? throw new InvalidOperationException(
                $"Damage requires '{name}'.");

        var amount = Req(dto.Amount, nameof(dto.Amount));

        var targeting = dto.Targeting.ToRef();

        return Damage.Of(amount,
            targeting
        );
    }
    // ---------- Targeting

    public static TargetingSpec ToRef(this TargetingSpecDto dto)
        => TargetingSpec.Of(dto.Origin, dto.Scope, dto.MaxTargets);


    public static SpellRef ToRef(this SpellDefinitionDto dto)
        => new SpellRef(SpellId.New(dto.Id)
            , dto.Name
            , dto.SpellType
            , dto.CharacterClass
            , dto.Initiative
            , dto.EnergyCost
            , dto.CriticalChance
            , dto.Effects.Select(e => e.ToRef()).ToArray()
        );

}
