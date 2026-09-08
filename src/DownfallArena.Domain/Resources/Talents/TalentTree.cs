using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Resources.Talents;

/// <summary>
/// The tree of spells a creature can unlock through evolution.
/// </summary>
public sealed class TalentTree
{
    private TalentTree(TalentTreeId id, string name, TalentNode root)
    {
        Id = id;
        Name = name;
        Root = root;
    }

    public TalentTreeId Id { get; }

    public string Name { get; }

    public TalentNode Root { get; }

    public IEnumerable<TalentNode> Nodes => Root.SelfAndDescendants();

    public IEnumerable<TalentSpell> Spells => Nodes.SelectMany(node => node.Spells);

    public static TalentTree Create(TalentTreeId id, string name, TalentNode root)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(root);
        return new TalentTree(id, name, root);
    }
}
