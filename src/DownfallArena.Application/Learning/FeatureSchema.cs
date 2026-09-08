using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning;

/// <summary>
/// The layout of an observation for one content and rule set: which feature sits at which index. Published
/// versions are immutable (<c>docs/learning/features.md</c>); any change to the layout rules is a new version, and
/// the <see cref="Id"/> adds a fingerprint of the concrete layout so two schemas of the same version but different
/// content or rule set never pass for each other.
/// </summary>
public sealed class FeatureSchema
{
    public const string CurrentVersion = "features:v1";

    /// <summary>The largest team size a schema supports: target masks hold one bit per board slot in an <c>int</c>.</summary>
    public const int MaxTeamSize = BoardSlots.MaxTeamSize;

    /// <summary>The condition kinds of the closed effect taxonomy (ADR 0012), in feature order.</summary>
    public static IReadOnlyList<string> ConditionKinds { get; } = ["Bleed", "Stun", "DefenseBuff", "InitiativeDebuff"];

    public static IReadOnlyList<string> GlobalFeatures { get; } =
        ["round_fraction", "phase", "sub_phase", "reveal_progress", "revealed_enemy_actions"];

    /// <summary>The features that open every creature block, before the condition pairs, spell bits, and node bits.</summary>
    public static IReadOnlyList<string> CreatureFeatures { get; } =
        ["alive", "health_fraction", "energy", "stunned", "defense", "initiative"];

    private static readonly Dictionary<string, int> ConditionKindIndexes =
        ConditionKinds.Select((kind, index) => (kind, index)).ToDictionary(pair => pair.kind, pair => pair.index);

    private readonly Dictionary<string, int> _indexes;
    private readonly Dictionary<SpellId, int> _spellIndexes;
    private readonly Dictionary<string, int> _nodeIndexes;

    private FeatureSchema(int teamSize, int roundCap, IReadOnlyList<SpellId> spells, IReadOnlyList<string> talentNodes)
    {
        Version = CurrentVersion;
        TeamSize = teamSize;
        RoundCap = roundCap;
        Spells = spells;
        TalentNodes = talentNodes;
        _spellIndexes = spells.Select((spell, index) => (spell, index)).ToDictionary(pair => pair.spell, pair => pair.index);
        _nodeIndexes = talentNodes.Select((node, index) => (node, index)).ToDictionary(pair => pair.node, pair => pair.index);
        FeatureNames = [.. GlobalFeatures, .. CreatureBlocks(teamSize, spells, talentNodes)];
        _indexes = FeatureNames.Select((name, index) => (name, index)).ToDictionary(pair => pair.name, pair => pair.index);
        Id = $"{Version}+{Fingerprint(FeatureNames, roundCap)}";
    }

    /// <summary>The version of the layout rules this schema follows.</summary>
    public string Version { get; }

    /// <summary>
    /// The version plus a fingerprint of the concrete layout (every feature name and the round cap), the string
    /// observations and artifacts carry: same id, same vector length, same meaning at every index.
    /// </summary>
    public string Id { get; }

    public int TeamSize { get; }

    public int RoundCap { get; }

    /// <summary>Every spell of the content, sorted by id, in feature order.</summary>
    public IReadOnlyList<SpellId> Spells { get; }

    /// <summary>Every talent node of the content as "tree/node", sorted, in feature order.</summary>
    public IReadOnlyList<string> TalentNodes { get; }

    public IReadOnlyList<string> FeatureNames { get; }

    public int Length => FeatureNames.Count;

    public int CreatureLength => CreatureFeatures.Count + (2 * ConditionKinds.Count) + Spells.Count + TalentNodes.Count;

    public static FeatureSchema Build(IGameResources resources, RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ruleSet.TeamSize, MaxTeamSize);

        var spells = resources.Spells.Select(spell => spell.Id).OrderBy(spell => spell.Value, StringComparer.Ordinal).ToList();
        var nodes = resources.TalentTrees
            .SelectMany(tree => tree.Nodes.Select(node => NodeKey(tree.Id, node.Code)))
            .OrderBy(node => node, StringComparer.Ordinal)
            .ToList();
        return new FeatureSchema(ruleSet.TeamSize, ruleSet.RoundCap, spells, nodes);
    }

    public static string NodeKey(TalentTreeId tree, string nodeCode)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return $"{tree.Value}/{nodeCode}";
    }

    /// <summary>The index of a condition kind among <see cref="ConditionKinds"/>, or -1 when it is not published.</summary>
    public static int ConditionKindIndex(string kind) => ConditionKindIndexes.GetValueOrDefault(kind, -1);

    public int IndexOf(string featureName) =>
        _indexes.TryGetValue(featureName, out var index)
            ? index
            : throw new ArgumentOutOfRangeException(nameof(featureName), featureName, "Unknown feature.");

    /// <summary>The index of a spell among the content's spells, or -1 when the spell is not in the content.</summary>
    public int SpellIndex(SpellId spell) => _spellIndexes.GetValueOrDefault(spell, -1);

    public int TalentNodeIndex(string nodeKey) => _nodeIndexes.GetValueOrDefault(nodeKey, -1);

    /// <summary>The first index of the block of the creature at a board slot (own slots first, then enemies).</summary>
    public int CreatureOffset(int boardSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(boardSlot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(boardSlot, 2 * TeamSize);
        return GlobalFeatures.Count + (boardSlot * CreatureLength);
    }

    private static string Fingerprint(IReadOnlyList<string> featureNames, int roundCap)
    {
        var layout = string.Join('\n', featureNames) + "\n" + roundCap.ToString(CultureInfo.InvariantCulture);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(layout)))[..12];
    }

    private static IEnumerable<string> CreatureBlocks(int teamSize, IReadOnlyList<SpellId> spells, IReadOnlyList<string> nodes)
    {
        foreach (var side in new[] { "own", "enemy" })
        {
            for (var slot = 0; slot < teamSize; slot++)
            {
                foreach (var name in CreatureBlock(spells, nodes))
                {
                    yield return $"{side}{slot}_{name}";
                }
            }
        }
    }

    private static IEnumerable<string> CreatureBlock(IReadOnlyList<SpellId> spells, IReadOnlyList<string> nodes)
    {
        foreach (var name in CreatureFeatures)
        {
            yield return name;
        }

        foreach (var kind in ConditionKinds)
        {
            yield return $"{kind}_amount";
            yield return $"{kind}_remaining";
        }

        foreach (var spell in spells)
        {
            yield return $"knows_{spell.Value}";
        }

        foreach (var node in nodes)
        {
            yield return $"node_{node}";
        }
    }
}
