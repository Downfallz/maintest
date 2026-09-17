using DownfallArena.Application.Learning;

namespace DownfallArena.Application.Catalogue;

/// <summary>
/// The catalogue a match is playing: every spell as a card, the talent trees as bands, and the pair that says
/// which game this is — the content hash and the rule set.
/// </summary>
/// <remarks>
/// It is the same pair a print sheet carries and an artifact stamps. A session is reproducible against a hash
/// <em>and</em> a rule set, or it is not reproducible (ADR 0054), and a deck and a screen that disagree about
/// either are a playtest of neither.
/// </remarks>
public sealed record CatalogueView(string ContentHash, RuleSetStamp Rules, IReadOnlyList<CardFace> Cards, IReadOnlyList<TalentBand> Trees, RoundShape Round);
