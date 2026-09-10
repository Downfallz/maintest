using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.Infrastructure.Resources.Schema;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// Turns a consolidated <see cref="GameSchema"/> into domain <see cref="GameResources"/>, reporting every
/// malformed value at once through <see cref="InvalidGameContentException"/>.
/// </summary>
public static class GameSchemaMapper
{
    private const string AmountField = "amount";

    public static GameResources ToGameResources(GameSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var problems = new List<string>();
        var spells = new List<Spell>();
        var creatures = new List<CreatureDefinition>();
        var trees = new List<TalentTree>();

        foreach (var dto in schema.Spells)
        {
            if (MapSpell(dto, problems) is { } spell)
            {
                spells.Add(spell);
            }
        }

        foreach (var dto in schema.Creatures)
        {
            if (MapCreature(dto, problems) is { } creature)
            {
                creatures.Add(creature);
            }
        }

        foreach (var dto in schema.TalentTrees)
        {
            if (MapTalentTree(dto, problems) is { } tree)
            {
                trees.Add(tree);
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidGameContentException(problems);
        }

        var version = string.IsNullOrWhiteSpace(schema.ContentHash) ? "unhashed" : schema.ContentHash;
        return GameResources.Create(version, creatures, spells, trees);
    }

    private static Spell? MapSpell(SpellDto dto, List<string> problems)
    {
        var context = $"spell '{dto.Id}'";
        var id = ParseId<SpellId>(dto.Id, context, problems);
        var type = ParseEnum<SpellType>(dto.SpellType, "spellType", context, problems);
        var creatureClass = ParseEnum<CreatureClass>(dto.CreatureClass, "creatureClass", context, problems);
        var targeting = dto.Targeting is null
            ? Problem<TargetingSpec>(problems, $"{context}: 'targeting' is required.")
            : MapTargeting(dto.Targeting, context, problems);
        var effects = dto.Effects.Select(effect => MapEffect(effect, context, problems)).OfType<Effect>().ToList();

        if (id is null || type is null || creatureClass is null || targeting is null || effects.Count != dto.Effects.Count)
        {
            return null;
        }

        return Guard(
            () => Spell.Create(
                id,
                dto.Name,
                type.Value,
                creatureClass.Value,
                new SpellStats(Initiative.Of(dto.Initiative), Energy.Of(dto.EnergyCost), CriticalChance.Of(dto.CriticalChance)),
                targeting,
                effects),
            context,
            problems);
    }

    private static TargetingSpec? MapTargeting(TargetingDto dto, string context, List<string> problems)
    {
        var origin = ParseEnum<TargetOrigin>(dto.Origin, "targeting.origin", context, problems);
        var scope = ParseEnum<TargetScope>(dto.Scope, "targeting.scope", context, problems);
        if (origin is null || scope is null)
        {
            return null;
        }

        if (scope == TargetScope.SingleTarget)
        {
            return dto.MaxTargets is null or 1
                ? TargetingSpec.SingleTarget(origin.Value)
                : Problem<TargetingSpec>(problems, $"{context}: a single-target spell cannot have 'maxTargets' other than 1.");
        }

        return Guard(() => TargetingSpec.Multi(origin.Value, dto.MaxTargets), context, problems);
    }

    /// <summary>
    /// One authored effect. The kinds are grouped by the shape their fields take, not by what they do: the
    /// taxonomy is closed (ADR 0012) and a new effect joins one of these families rather than adding a shape.
    /// </summary>
    private static Effect? MapEffect(EffectDto dto, string context, List<string> problems)
    {
        var effectContext = $"{context}, effect '{dto.Kind}'";

        return dto.Kind.ToUpperInvariant() switch
        {
            "DAMAGE" => Instant(dto.Amount, AmountField, effectContext, problems, Damage.Of),
            "HEAL" => Instant(dto.Amount, AmountField, effectContext, problems, Heal.Of),
            "ENERGYGAIN" => Instant(dto.Amount, AmountField, effectContext, problems, EnergyGain.Of),
            "BLEED" => PerRound(dto, effectContext, problems, Bleed.Of),
            "REGENERATION" => PerRound(dto, effectContext, problems, Regeneration.Of),
            "ENERGYREGENERATION" => PerRound(dto, effectContext, problems, EnergyRegeneration.Of),
            "STUN" => ForRounds(dto, effectContext, problems, Stun.For),
            "DEFENSEBUFF" => WhileLasting(dto, effectContext, problems, DefenseBuff.Of),
            "INITIATIVEDEBUFF" => WhileLasting(dto, effectContext, problems, InitiativeDebuff.Of),
            _ => Problem<Effect>(problems, $"{context}: unknown effect kind '{dto.Kind}'. See data/README.md for the supported kinds."),
        };
    }

    /// <summary>Damage, Heal, EnergyGain: an amount, applied once.</summary>
    private static Effect? Instant(int? amount, string field, string context, List<string> problems, Func<int, Effect> create) =>
        Require(amount, field, context, problems) is { } value ? Guard(() => create(value), context, problems) : null;

    /// <summary>Bleed and Regeneration: an amount every round, for a number of rounds.</summary>
    private static Effect? PerRound(EffectDto dto, string context, List<string> problems, Func<int, int, StackingPolicy, Effect> create)
    {
        var stacking = Stacking(dto, context, problems, StackingPolicy.Refresh);
        if (Rounds(dto, context, problems) is not { } rounds || Require(dto.AmountPerRound, "amountPerRound", context, problems) is not { } amount)
        {
            return null;
        }

        return Guard(() => create(amount, rounds, stacking), context, problems);
    }

    /// <summary>Stun: a number of rounds and nothing else.</summary>
    private static Effect? ForRounds(EffectDto dto, string context, List<string> problems, Func<int, StackingPolicy, Effect> create)
    {
        var stacking = Stacking(dto, context, problems, StackingPolicy.Refresh);
        return Rounds(dto, context, problems) is { } rounds ? Guard(() => create(rounds, stacking), context, problems) : null;
    }

    /// <summary>DefenseBuff and InitiativeDebuff: an amount for a duration, which may be permanent.</summary>
    private static Effect? WhileLasting(EffectDto dto, string context, List<string> problems, Func<int, Duration, StackingPolicy, Effect> create)
    {
        var stacking = Stacking(dto, context, problems, StackingPolicy.Stack);
        if (Lasting(dto, context, problems) is not { } duration || Require(dto.Amount, AmountField, context, problems) is not { } amount)
        {
            return null;
        }

        return Guard(() => create(amount, duration, stacking), context, problems);
    }

    /// <summary>
    /// The stacking policy the content asked for, or the family's own default when it said nothing. A value
    /// that does not parse is a reported problem and falls back too, so the rest of the effect is still checked.
    /// </summary>
    private static StackingPolicy Stacking(EffectDto dto, string context, List<string> problems, StackingPolicy fallback) =>
        dto.Stacking is null ? fallback : ParseEnum<StackingPolicy>(dto.Stacking, "stacking", context, problems) ?? fallback;

    private static int? Rounds(EffectDto dto, string context, List<string> problems)
    {
        if (dto.Permanent)
        {
            problems.Add($"{context}: this effect cannot be permanent.");
            return null;
        }

        return Require(dto.DurationRounds, "durationRounds", context, problems);
    }

    private static Duration? Lasting(EffectDto dto, string context, List<string> problems)
    {
        if (dto.Permanent)
        {
            return Duration.Permanent;
        }

        if (Require(dto.DurationRounds, "durationRounds", context, problems) is not { } rounds)
        {
            return null;
        }

        if (rounds < 1)
        {
            problems.Add($"{context}: 'durationRounds' must be at least 1.");
            return null;
        }

        return Duration.OfRounds(rounds);
    }

    private static CreatureDefinition? MapCreature(CreatureDefinitionDto dto, List<string> problems)
    {
        var context = $"creature '{dto.Id}'";
        var id = ParseId<CreatureDefinitionId>(dto.Id, context, problems);
        var creatureClass = ParseEnum<CreatureClass>(dto.CreatureClass, "creatureClass", context, problems);
        var talentTree = ParseId<TalentTreeId>(dto.TalentTreeId, context, problems);
        var startingSpells = dto.StartingSpellIds.Select(text => ParseId<SpellId>(text, context, problems)).OfType<SpellId>().ToList();

        if (id is null || creatureClass is null || talentTree is null || startingSpells.Count != dto.StartingSpellIds.Count)
        {
            return null;
        }

        return Guard(
            () => CreatureDefinition.Create(
                id,
                dto.Name,
                creatureClass.Value,
                new CreatureStats(
                    Health.Of(dto.BaseHealth),
                    Energy.Of(dto.BaseEnergy),
                    Defense.Of(dto.BaseDefense),
                    Initiative.Of(dto.BaseInitiative),
                    CriticalChance.Of(dto.BaseCriticalChance)),
                talentTree,
                startingSpells),
            context,
            problems);
    }

    private static TalentTree? MapTalentTree(TalentTreeDto dto, List<string> problems)
    {
        var context = $"talent tree '{dto.Id}'";
        var id = ParseId<TalentTreeId>(dto.Id, context, problems);
        var root = dto.Root is null
            ? Problem<TalentNode>(problems, $"{context}: 'root' is required.")
            : MapNode(dto.Root, context, problems);

        return id is null || root is null ? null : Guard(() => TalentTree.Create(id, dto.Name, root), context, problems);
    }

    private static TalentNode? MapNode(TalentNodeDto dto, string context, List<string> problems)
    {
        var nodeContext = $"{context}, node '{dto.Code}'";
        var prerequisites = MapPrerequisites(dto.Prerequisites, nodeContext, problems);
        var spells = dto.Spells
            .Select(spell => ParseId<SpellId>(spell.Id, nodeContext, problems) is { } spellId
                ? new TalentSpell(spellId, MapPrerequisites(spell.Prerequisites, $"{nodeContext}, spell '{spell.Id}'", problems))
                : null)
            .OfType<TalentSpell>()
            .ToList();
        var children = dto.Children.Select(child => MapNode(child, context, problems)).OfType<TalentNode>().ToList();

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            problems.Add($"{context}: every node needs a 'code'.");
            return null;
        }

        return spells.Count != dto.Spells.Count || children.Count != dto.Children.Count
            ? null
            : new TalentNode(dto.Code, dto.Name, prerequisites, spells, children);
    }

    private static TalentPrerequisites MapPrerequisites(PrerequisitesDto? dto, string context, List<string> problems)
    {
        if (dto is null)
        {
            return TalentPrerequisites.None;
        }

        var allOf = dto.AllOf.Select(text => ParseId<SpellId>(text, context, problems)).OfType<SpellId>();
        var anyOf = dto.AnyOf.Select(text => ParseId<SpellId>(text, context, problems)).OfType<SpellId>();
        return TalentPrerequisites.Of(allOf, anyOf);
    }

    private static TId? ParseId<TId>(string text, string context, List<string> problems)
        where TId : VersionedId, IVersionedId<TId>
    {
        if (VersionedId.TryParse<TId>(text, out var id))
        {
            return id;
        }

        problems.Add($"{context}: '{text}' is not a valid {TId.Kind} id (expected '{TId.Kind}:<name>:v<number>').");
        return null;
    }

    private static TEnum? ParseEnum<TEnum>(string text, string field, string context, List<string> problems)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) && Enum.IsDefined(value))
        {
            return value;
        }

        problems.Add($"{context}: '{text}' is not a valid '{field}'. Expected one of {string.Join(", ", Enum.GetNames<TEnum>())}.");
        return null;
    }

    private static int? Require(int? value, string field, string context, List<string> problems)
    {
        if (value is null)
        {
            problems.Add($"{context}: '{field}' is required.");
        }

        return value;
    }

    private static T? Guard<T>(Func<T> create, string context, List<string> problems)
        where T : class
    {
        try
        {
            return create();
        }
        catch (ArgumentException exception)
        {
            problems.Add($"{context}: {exception.Message}");
            return null;
        }
    }

    private static T? Problem<T>(List<string> problems, string problem)
        where T : class
    {
        problems.Add(problem);
        return null;
    }
}
