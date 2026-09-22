namespace DownfallArena.Infrastructure.Resources.Authoring;

/// <summary>
/// The four kinds of authored content, one folder each under the content directory (ADR 0009).
/// <para>
/// A package joined them rather than staying derived from the talent tree: a pick buys one, its prerequisites
/// are the only eligibility rule, and the file the engine loads is the file an author edits (ADR 0057).
/// </para>
/// </summary>
public enum ContentKind
{
    Creature,
    Spell,
    TalentTree,
    Tier,
}
