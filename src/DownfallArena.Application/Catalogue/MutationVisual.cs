namespace DownfallArena.Application.Catalogue;

/// <summary>
/// How a package shows on the creature that bought it (ADR 0133): the family whose body it belongs to, and the
/// accent it draws on that body, which a root package has none of. Presentation authored beside the content;
/// the stage is the package's own level.
/// </summary>
public sealed record MutationVisual(string Family, string? Motif);
