using System.Collections.Concurrent;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The content a table is played on, and the services built over it (ADR 0163): this host's own, or the content an
/// older table began on, kept in the store and played on services built for it alone. The catalogue and the guide
/// are kept per rule set, as a host keeps them for its own content.
/// </summary>
internal sealed class TableContent
{
    private readonly ConcurrentDictionary<RuleSet, CatalogueView> _catalogues = new();
    private readonly ConcurrentDictionary<RuleSet, DecisionGuideProjection> _guides = new();

    /// <param name="stepped">The match's handlers over this content, or none for the host's own, whose are the host's.</param>
    public TableContent(IServiceProvider services, SteppedHost? stepped = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
        Stepped = stepped;
        Resources = services.GetRequiredService<IGameResources>();
    }

    public IServiceProvider Services { get; }

    /// <summary>The match's handlers built over this content; none for the host's own content.</summary>
    public SteppedHost? Stepped { get; }

    public IGameResources Resources { get; }

    /// <summary>The content's hash: what a table's record names it by.</summary>
    public string Version => Resources.Version;

    /// <summary>The cards and the round shape of one rule set, built once per rule set this content is played under.</summary>
    public CatalogueView Catalogue(RuleSet rules) =>
        _catalogues.GetOrAdd(rules, one => Tagged(CatalogueProjection.Build(Resources, one)));

    public DecisionGuideProjection Guide(RuleSet rules) =>
        _guides.GetOrAdd(rules, one => new DecisionGuideProjection(Resources, one));

    /// <summary>
    /// Each package with the words it is known by and how it shows on a creature, and each card with how its cast
    /// is drawn, where the builder published them next to the schema: presentation the table and the battle show,
    /// kept out of the schema so the content hash does not move with them.
    /// </summary>
    private CatalogueView Tagged(CatalogueView catalogue)
    {
        var identities = Services.GetService<PackageIdentities>() ?? PackageIdentities.None;
        var spells = Services.GetService<SpellArt>() ?? SpellArt.None;
        var mutations = Services.GetService<MutationArt>() ?? MutationArt.None;
        return catalogue with
        {
            Packages = [.. catalogue.Packages.Select(package => package with { Tags = identities.Of(package.Id), Visual = mutations.Of(package.Id) })],
            Cards = [.. catalogue.Cards.Select(card => card with { Visual = spells.Of(card.Id) })],
        };
    }
}
