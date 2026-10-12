using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>What composing a table leaves behind when it cannot finish.</summary>
public sealed class TableComposerTests
{
    /// <summary>
    /// A table whose first trace cannot be written has already started its match. Nothing holds it, so no
    /// sweep would ever reach it: composing lets go of it itself, or every opening during a storage outage
    /// would leave a match in the host's memory.
    /// </summary>
    [Fact]
    public async Task A_table_that_fails_to_open_leaves_no_match_behind()
    {
        using var hosted = new HostedTables(directory => new TracesRefused(new FileArtifactStore(directory)));
        var repository = (InMemoryMatchRepository)hosted.Services.GetRequiredService<IMatchRepository>();

        await Should.ThrowAsync<IOException>(() => hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken));

        for (var attempt = 0; attempt < 300 && repository.Count > 0; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        repository.Count.ShouldBe(0, "the match of a table that never opened should have been forgotten");
    }

    /// <summary>
    /// A table plays the host's rule set with the declaration its request named (ADR 0110), and the host's own
    /// when it named none, whichever that is. Nothing else of the rule set moves.
    /// </summary>
    [Fact]
    public void A_table_plays_the_declaration_its_request_names_and_the_host_s_otherwise()
    {
        using var hosted = new HostedTables();
        var atActivation = HostedTables.Rules.WithCombatDeclaration(CombatDeclaration.AtActivation);
        var host = new TableComposer(hosted.Services, atActivation, store: null);

        hosted.Composer.RulesFor(HostedTables.Bots with { Declaration = CombatDeclaration.AtActivation }).ShouldBe(atActivation);
        hosted.Composer.RulesFor(HostedTables.Bots).ShouldBe(HostedTables.Rules);
        host.RulesFor(HostedTables.Bots).ShouldBe(atActivation);
        host.RulesFor(HostedTables.Bots with { Declaration = CombatDeclaration.HiddenIntent }).ShouldBe(HostedTables.Rules);
    }

    /// <summary>
    /// Until the page can ask for an action (plan PR 6), a person seated at a table that chooses spells at the
    /// slot would wait on a screen offering nothing, so such a table is refused before it opens; two bots are not.
    /// </summary>
    [Fact]
    public async Task A_table_that_chooses_spells_at_the_slot_seats_no_person_until_the_page_can_ask_for_an_action()
    {
        using var hosted = new HostedTables();
        var host = new TableComposer(hosted.Services, HostedTables.Rules, store: null);

        var refused = await Should.ThrowAsync<ArgumentException>(() => host.ComposeAsync(HostedTables.OnePerson with { Declaration = CombatDeclaration.AtActivation }, TestContext.Current.CancellationToken));
        using var bots = await host.ComposeAsync(HostedTables.Bots with { Declaration = CombatDeclaration.AtActivation }, TestContext.Current.CancellationToken);

        refused.Message.ShouldContain("page");
        bots.Session.Rules.CombatDeclaration.ShouldBe(CombatDeclaration.AtActivation);
    }

    /// <summary>A store whose writes under <c>traces/</c> fail, as a storage outage would fail the first checkpoint.</summary>
    private sealed class TracesRefused(IArtifactStore inner) : IArtifactStore
    {
        public IArtifactWriter Writer(string run) => new Refusing(inner.Writer(run));

        public IArtifactReader Reader(string run) => inner.Reader(run);

        public string LocationOf(string run) => inner.LocationOf(run);

        public Task<IReadOnlyList<string>> RunsAsync(CancellationToken cancellationToken = default) => inner.RunsAsync(cancellationToken);

        public Task<bool> DeleteAsync(string run, CancellationToken cancellationToken = default) => inner.DeleteAsync(run, cancellationToken);

        private sealed class Refusing(IArtifactWriter inner) : IArtifactWriter
        {
            public Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default) =>
                relativePath.StartsWith("traces/", StringComparison.Ordinal)
                    ? Task.FromException(new IOException("The store is out."))
                    : inner.WriteJsonAsync(relativePath, value, cancellationToken);

            public Task StartJsonLinesAsync(string relativePath, CancellationToken cancellationToken = default) =>
                inner.StartJsonLinesAsync(relativePath, cancellationToken);

            public Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default) =>
                inner.AppendJsonLinesAsync(relativePath, values, cancellationToken);
        }
    }
}
