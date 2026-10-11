using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DownfallArena.Application.Learning;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Infrastructure.Learning.Json;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Infrastructure.Learning;

/// <summary>
/// JSON conventions of the learning artifacts (ADR 0013), shared by every file a run writes so the viewer and the
/// Python side read one dialect: camelCase, enums by name, identifiers and stats as plain values, and closed
/// hierarchies (effects, outcomes, domain events) as objects with a <c>kind</c>. Write-only: the engine produces
/// artifacts, it does not read them back.
/// </summary>
public static class ArtifactJson
{
    /// <summary>Indented, for documents a human opens (manifest, trace).</summary>
    public static JsonSerializerOptions DocumentOptions { get; } = Create(indented: true);

    /// <summary>Compact, one value per line (steps, episodes).</summary>
    public static JsonSerializerOptions LineOptions { get; } = Create(indented: false);

    private static JsonSerializerOptions Create(bool indented)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = indented,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitTheDefaultCombatDeclaration } },
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new VersionedIdConverterFactory());
        options.Converters.Add(new CreatureIdConverter());
        options.Converters.Add(new MatchIdConverter());
        options.Converters.Add(new PlayerIdConverter());
        options.Converters.Add(new RoundIdConverter());
        options.Converters.Add(new StatConverterFactory());
        options.Converters.Add(new CriticalChanceConverter());
        options.Converters.Add(new KindedObjectConverterFactory(typeof(Effect), typeof(EffectOutcome), typeof(IDomainEvent)));
        return options;
    }

    /// <summary>
    /// A rule-set stamp names its combat declaration only when it is not today's rule (ADR 0110): the stamps
    /// written before the field existed, and the readers that compare them as written (the Python stamps, the
    /// viewer), then read a default run as the same game they always did.
    /// </summary>
    private static void OmitTheDefaultCombatDeclaration(JsonTypeInfo type)
    {
        if (type.Type != typeof(RuleSetStamp))
        {
            return;
        }

        foreach (var property in type.Properties.Where(property => property.PropertyType == typeof(CombatDeclaration)))
        {
            property.ShouldSerialize = (_, value) => value is not CombatDeclaration.HiddenIntent;
        }
    }
}
