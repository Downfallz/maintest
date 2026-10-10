using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Schema;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// Reads authored content from a directory, resolves aliases, validates everything, and produces the consolidated,
/// hashed <see cref="GameSchema"/> (ADR 0009). Also writes and loads that schema.
/// </summary>
public static class GameSchemaBuilder
{
    public const string CreaturesFolder = "Creatures";
    public const string SpellsFolder = "Spells";
    public const string TalentTreesFolder = "TalentTrees";

    public const string TiersFolder = "Tiers";
    public const string AliasesFile = "aliases.json";

    public static GameSchema Build(string dataDirectory) => Build(dataDirectory, notes: null);

    /// <summary>
    /// Builds the schema, adding to <paramref name="notes"/> what the authoring-only <c>enabled</c> switch left
    /// out (ADR 0015). Notes are not problems: the build succeeds with them.
    /// </summary>
    public static GameSchema Build(string dataDirectory, ICollection<string>? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        if (!Directory.Exists(dataDirectory))
        {
            throw new DirectoryNotFoundException($"Content directory '{dataDirectory}' does not exist.");
        }

        var problems = new List<string>();
        var aliases = LoadAliases(Path.Combine(dataDirectory, AliasesFile), problems);
        var creatures = LoadAll<CreatureDefinitionDto>(Path.Combine(dataDirectory, CreaturesFolder), problems);
        var spells = LoadAll<SpellDto>(Path.Combine(dataDirectory, SpellsFolder), problems);
        var trees = LoadAll<TalentTreeDto>(Path.Combine(dataDirectory, TalentTreesFolder), problems);
        var tiers = LoadAll<TierDto>(Path.Combine(dataDirectory, TiersFolder), problems, optional: true);
        ThrowIfAny(problems);

        var resolver = new AliasResolver(aliases);
        var schema = new GameSchema
        {
            Creatures = [.. creatures.Select(creature => Canonical(creature, resolver, problems)).OrderBy(creature => creature.Id, StringComparer.Ordinal)],
            Spells = [.. spells.Select(spell => Canonical(spell, resolver, problems)).OrderBy(spell => spell.Id, StringComparer.Ordinal)],
            TalentTrees = [.. trees.Select(tree => Canonical(tree, resolver, problems)).OrderBy(tree => tree.Id, StringComparer.Ordinal)],
            Tiers = [.. tiers.Select(tier => Canonical(tier, resolver, problems)).OrderBy(tier => tier.Id, StringComparer.Ordinal)],
            Aliases = new SortedDictionary<string, string>(aliases, StringComparer.Ordinal),
        };
        ThrowIfAny(problems);

        schema = DisabledContent.Remove(schema, notes, problems);
        ThrowIfAny(problems);

        CheckTheDie(schema, problems);
        ThrowIfAny(problems);

        schema = Versioned(schema);
        schema = schema with { ContentHash = ComputeHash(schema) };
        GameSchemaMapper.ToGameResources(schema);
        return schema;
    }

    public static void Write(GameSchema schema, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, GameSchemaJson.SchemaFileName), JsonSerializer.Serialize(schema, GameSchemaJson.WriteOptions));
        File.WriteAllText(Path.Combine(outputDirectory, GameSchemaJson.HashFileName), schema.ContentHash);
    }

    /// <summary>
    /// Loads a consolidated schema, verifies its content hash, and maps it to domain resources.
    /// </summary>
    public static GameResources Load(string schemaPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException($"Game schema '{schemaPath}' does not exist. Run the data builder first.", schemaPath);
        }

        var text = File.ReadAllText(schemaPath);

        // The declared version is read before the document is deserialized, and deliberately: a document from a
        // newer builder is one carrying members this reader does not know, and the strict reader
        // (UnmappedMemberHandling.Disallow) would throw about the member rather than about the version -- the
        // one case the version check exists for. A loose read of one number cannot fail that way.
        VerifyKnownVersion(DeclaredVersion(text, schemaPath), schemaPath);

        var schema = JsonSerializer.Deserialize<GameSchema>(text, GameSchemaJson.ReadOptions)
            ?? throw new InvalidGameContentException($"'{schemaPath}' does not contain a game schema.");

        VerifyVersionCarriesWhatItSays(schema, schemaPath);

        var expected = ComputeHash(schema);
        if (!string.Equals(expected, schema.ContentHash, StringComparison.Ordinal))
        {
            throw new InvalidGameContentException($"'{schemaPath}' has content hash '{schema.ContentHash}' but its content hashes to '{expected}'. Rebuild it with the data builder.");
        }

        return GameSchemaMapper.ToGameResources(schema);
    }

    public static string ComputeHash(GameSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var canonical = JsonSerializer.Serialize(schema with { ContentHash = string.Empty }, GameSchemaJson.HashOptions);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// The document's version and its <c>tiers</c> member decided together, because they are one fact: the
    /// version is the lowest one that can read the document. A catalogue with no packages is emitted exactly as
    /// it was before packages existed -- member absent, version 1, same content hash -- and one that has them
    /// says version 2, because a reader written before the member refuses it as an unknown field. A package that
    /// uses an any-of prerequisite or a passive (ADR 0101) raises it once more, for the same reason.
    /// </summary>
    private static GameSchema Versioned(GameSchema schema) =>
        schema.Tiers is { Count: > 0 } tiers
            ? schema with { SchemaVersion = tiers.Any(tier => tier.UsesCapstoneMembers) ? GameSchema.VersionWithCapstones : GameSchema.VersionWithTiers }
            : schema with { SchemaVersion = GameSchema.VersionWithoutTiers, Tiers = null };

    /// <summary>
    /// The version the document declares, read on its own. A document with no <c>schemaVersion</c> is the
    /// first version, which is what the record's own default says.
    /// </summary>
    private static int DeclaredVersion(string text, string schemaPath)
    {
        using var document = JsonDocument.Parse(text, GameSchemaJson.DocumentOptions);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidGameContentException($"'{schemaPath}' does not contain a game schema.");
        }

        if (!document.RootElement.TryGetProperty("schemaVersion", out var version))
        {
            return GameSchema.VersionWithoutTiers;
        }

        return version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number)
            ? number
            : throw new InvalidGameContentException($"'{schemaPath}' declares a schema version that is not a whole number.");
    }

    /// <summary>
    /// Read before anything else, because a version this engine does not know explains both an unknown member
    /// and a hash mismatch that would otherwise read as corrupted content.
    /// </summary>
    private static void VerifyKnownVersion(int declared, string schemaPath)
    {
        if (declared is GameSchema.VersionWithoutTiers or GameSchema.VersionWithTiers or GameSchema.VersionWithCapstones)
        {
            return;
        }

        // Which side it is on is the whole use of the sentence: an older document is rebuilt from `data/`, a
        // newer one needs a newer engine. Neither is a corrupt file, which is what both would otherwise read as.
        var age = declared <= GameSchema.LastVersionWithASpellInitiative
            ? "It was written by an older builder, before a spell stopped carrying its own initiative (ADR 0059); rebuild it from 'data/'."
            : "It was written by a newer builder.";

        throw new InvalidGameContentException(
            $"'{schemaPath}' declares schema version {declared}; this engine reads "
            + $"{GameSchema.VersionWithoutTiers} to {GameSchema.VersionWithCapstones}. {age}");
    }

    /// <summary>
    /// The pairing, verified both ways so that one content hash has one document: a version that does not
    /// match what the document carries would hash differently for the same catalogue.
    /// </summary>
    private static void VerifyVersionCarriesWhatItSays(GameSchema schema, string schemaPath)
    {
        var carriesTiers = schema.Tiers is { Count: > 0 };
        if (carriesTiers && schema.SchemaVersion == GameSchema.VersionWithoutTiers)
        {
            throw new InvalidGameContentException(
                $"'{schemaPath}' declares schema version {GameSchema.VersionWithoutTiers} but carries evolution packages; "
                + $"a catalogue that uses them is version {GameSchema.VersionWithTiers}. Rebuild it with the data builder.");
        }

        if (!carriesTiers && schema.SchemaVersion != GameSchema.VersionWithoutTiers)
        {
            throw new InvalidGameContentException(
                $"'{schemaPath}' declares schema version {schema.SchemaVersion} but carries no evolution packages; "
                + $"a catalogue without them is version {GameSchema.VersionWithoutTiers}. Rebuild it with the data builder.");
        }

        var carriesCapstones = schema.Tiers?.Any(tier => tier.UsesCapstoneMembers) ?? false;
        if (carriesTiers && carriesCapstones != (schema.SchemaVersion == GameSchema.VersionWithCapstones))
        {
            var expected = carriesCapstones ? GameSchema.VersionWithCapstones : GameSchema.VersionWithTiers;
            throw new InvalidGameContentException(
                $"'{schemaPath}' declares schema version {schema.SchemaVersion}, but a catalogue whose packages "
                + $"{(carriesCapstones ? "use" : "use no")} any-of prerequisites or passives is version {expected}. Rebuild it with the data builder.");
        }
    }

    private static CreatureDefinitionDto Canonical(CreatureDefinitionDto creature, AliasResolver resolver, List<string> problems)
    {
        var context = $"creature '{creature.Id}'";
        return creature with
        {
            Id = resolver.Resolve<CreatureDefinitionId>(creature.Id, context, problems) ?? creature.Id,
            TalentTreeId = resolver.Resolve<TalentTreeId>(creature.TalentTreeId, context, problems) ?? creature.TalentTreeId,
            StartingSpellIds = [.. creature.StartingSpellIds.Select(spell => resolver.Resolve<SpellId>(spell, context, problems) ?? spell)],
        };
    }

    /// <summary>
    /// A spell with its alias resolved, and with "no caster effect" written one way only: an empty list
    /// authored by hand is dropped to null, so it serializes away instead of moving the content hash
    /// (ADR 0031).
    /// </summary>
    private static SpellDto Canonical(SpellDto spell, AliasResolver resolver, List<string> problems) =>
        spell with
        {
            Id = resolver.Resolve<SpellId>(spell.Id, $"spell '{spell.Id}'", problems) ?? spell.Id,
            CasterEffects = spell.CasterEffects is { Count: > 0 } ? spell.CasterEffects : null,
        };

    private static TierDto Canonical(TierDto tier, AliasResolver resolver, List<string> problems)
    {
        var context = $"tier '{tier.Id}'";
        return tier with
        {
            Id = resolver.Resolve<TierId>(tier.Id, context, problems) ?? tier.Id,
            Prerequisites = [.. tier.Prerequisites.Select(id => resolver.Resolve<TierId>(id, context, problems) ?? id)],
            AnyOf = tier.AnyOf is { Count: > 0 } anyOf ? [.. anyOf.Select(id => resolver.Resolve<TierId>(id, context, problems) ?? id)] : null,
            Passive = tier.Passive is { StunImmunity: false, UpkeepEnergy: 0, DamageBonus: 0, Sunder: 0 } ? null : tier.Passive,
            Spells = [.. tier.Spells.Select(id => resolver.Resolve<SpellId>(id, context, problems) ?? id)],
        };
    }

    private static TalentTreeDto Canonical(TalentTreeDto tree, AliasResolver resolver, List<string> problems)
    {
        var context = $"talent tree '{tree.Id}'";
        return tree with
        {
            Id = resolver.Resolve<TalentTreeId>(tree.Id, context, problems) ?? tree.Id,
            Root = tree.Root is null ? null : Canonical(tree.Root, context, resolver, problems),
        };
    }

    private static TalentNodeDto Canonical(TalentNodeDto node, string context, AliasResolver resolver, List<string> problems)
    {
        var nodeContext = $"{context}, node '{node.Code}'";
        return node with
        {
            Prerequisites = Canonical(node.Prerequisites, nodeContext, resolver, problems),
            Spells = [.. node.Spells.Select(spell => spell with
            {
                Id = resolver.Resolve<SpellId>(spell.Id, nodeContext, problems) ?? spell.Id,
                Prerequisites = Canonical(spell.Prerequisites, nodeContext, resolver, problems),
            })],
            Children = [.. node.Children.Select(child => Canonical(child, context, resolver, problems))],
        };
    }

    private static PrerequisitesDto? Canonical(PrerequisitesDto? prerequisites, string context, AliasResolver resolver, List<string> problems) =>
        prerequisites is null
            ? null
            : prerequisites with
            {
                AllOf = [.. prerequisites.AllOf.Select(spell => resolver.Resolve<SpellId>(spell, context, problems) ?? spell)],
                AnyOf = [.. prerequisites.AnyOf.Select(spell => resolver.Resolve<SpellId>(spell, context, problems) ?? spell)],
            };

    private static Dictionary<string, string> LoadAliases(string path, List<string> problems)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), GameSchemaJson.ReadOptions)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException exception)
        {
            problems.Add($"{Path.GetFileName(path)}: {exception.Message}");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Every JSON file under a content folder. A missing folder is a problem unless <paramref name="optional"/>
    /// says otherwise, which only <c>Tiers</c> is while the migration runs: a catalogue authored before tiers
    /// existed has no such folder and is still a catalogue.
    /// <para>
    /// The option goes when a pick is spent on a package rather than on a spell. A catalogue with no packages
    /// has no legal evolution choice at that point, so it stops being playable content and the folder stops
    /// being optional. The marker is mechanical rather than a note to remember: such a catalogue is schema
    /// version 1, so "a version-1 document cannot start a match" is the check to add, in the domain, and the
    /// day it exists this argument has nothing left to allow.
    /// </para>
    /// </summary>
    private static List<T> LoadAll<T>(string directory, List<string> problems, bool optional = false)
        where T : class
    {
        var items = new List<T>();
        if (!Directory.Exists(directory))
        {
            if (!optional)
            {
                problems.Add($"Content folder '{Path.GetFileName(directory)}' is missing.");
            }

            return items;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            try
            {
                var item = JsonSerializer.Deserialize<T>(File.ReadAllText(file), GameSchemaJson.ReadOptions);
                if (item is null)
                {
                    problems.Add($"{Path.GetRelativePath(directory, file)}: the file is empty.");
                    continue;
                }

                items.Add(item);
            }
            catch (JsonException exception)
            {
                problems.Add($"{Path.GetRelativePath(directory, file)}: {exception.Message}");
            }
        }

        return items;
    }

    /// <summary>
    /// The die's grid (ADR 0100): a Spell's critical chance is a whole number of twentieths, and a Creature's own
    /// is zero, so that the threshold a card prints is the whole chance a d20 rolls against. Read once the
    /// disabled content has left, because a Spell out of the build prints no card.
    /// </summary>
    private static void CheckTheDie(GameSchema schema, List<string> problems)
    {
        foreach (var spell in schema.Spells.Where(spell => !IsTwentieth(spell.CriticalChance)))
        {
            problems.Add(
                $"spell '{spell.Id}': a critical chance of {Chance(spell.CriticalChance)} is not a whole number of twentieths, "
                + $"so no d20 threshold prints it (ADR 0100); the nearest is {Chance(NearestTwentieth(spell.CriticalChance))}.");
        }

        foreach (var creature in schema.Creatures.Where(creature => creature.BaseCriticalChance != 0))
        {
            problems.Add(
                $"creature '{creature.Id}': a base critical chance of {Chance(creature.BaseCriticalChance)} is refused; a Creature's own "
                + "chance is zero, so that the chance a card prints is the whole chance rolled (ADR 0042, ADR 0100).");
        }
    }

    private static bool IsTwentieth(double chance) => Math.Abs((chance * 20) - Math.Round(chance * 20)) < 1e-9;

    /// <summary>To the nearest twentieth, a tie rounding up: <c>Math.Round</c> would send 0.025 to 0, not 0.05.</summary>
    private static double NearestTwentieth(double chance) => Math.Floor((chance * 20) + 0.5) / 20;

    private static string Chance(double chance) => chance.ToString("0.###", CultureInfo.InvariantCulture);

    private static void ThrowIfAny(List<string> problems)
    {
        if (problems.Count > 0)
        {
            throw new InvalidGameContentException(problems);
        }
    }
}
