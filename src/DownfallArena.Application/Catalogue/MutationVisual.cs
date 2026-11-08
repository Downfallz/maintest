namespace DownfallArena.Application.Catalogue;

/// <summary>
/// How a package shows on the creature that bought it (ADR 0133): the family whose body it belongs to, and the
/// accent it draws on that body, which a root package has none of. Presentation authored beside the content;
/// the stage is the package's own level.
/// </summary>
/// <param name="Family">The body the package belongs to.</param>
/// <param name="Motif">The accent it draws on that body; none for a root.</param>
/// <param name="Artwork">The evolution illustration it draws when it is the one a creature shows (ADR 0151), by
/// the id the page's registry knows it by; none for a capstone, which never changes the image.</param>
/// <param name="Effect">The effect a capstone lends the creature, by the id the page knows it by (ADR 0151).</param>
public sealed record MutationVisual(string Family, string? Motif, string? Artwork = null, string? Effect = null);
