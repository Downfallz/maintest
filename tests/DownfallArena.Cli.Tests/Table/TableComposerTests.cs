using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Cli.Table;
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

    /// <summary>A store whose writes under <c>traces/</c> fail, as a storage outage would fail the first checkpoint.</summary>
    private sealed class TracesRefused(IArtifactStore inner) : IArtifactStore
    {
        public IArtifactWriter Writer(string run) => new Refusing(inner.Writer(run));

        public IArtifactReader Reader(string run) => inner.Reader(run);

        public string LocationOf(string run) => inner.LocationOf(run);

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
