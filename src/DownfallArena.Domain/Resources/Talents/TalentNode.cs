namespace DownfallArena.Domain.Resources.Talents;

/// <summary>
/// A node of a talent tree: a class or specialisation offering spells, gated by prerequisites.
/// </summary>
public sealed record TalentNode(
    string Code,
    string Name,
    TalentPrerequisites Prerequisites,
    IReadOnlyList<TalentSpell> Spells,
    IReadOnlyList<TalentNode> Children)
{
    public IEnumerable<TalentNode> SelfAndDescendants()
    {
        yield return this;

        foreach (var descendant in Children.SelectMany(child => child.SelfAndDescendants()))
        {
            yield return descendant;
        }
    }
}
