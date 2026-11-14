using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Infrastructure.Matches.Scenarios;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The match scenarios the operator cut (docs/tabletop/scenarios-plan.md, S4): one <c>scenario.json</c> a scenario,
/// under an id that sorts by when it was cut, in a store beside the recordings. The content each names is kept
/// beside them under its hash (ADR 0163), so a scenario does not carry it. Whether the operator listed a scenario for
/// signed-in players (S6) is this host's, not the scenario's: it is kept beside it in <see cref="ListingFile" />, never in
/// the <c>scenario.json</c> a scenario is shared as.
/// </summary>
internal sealed class ScenarioStore(IArtifactStore store, TimeProvider clock)
{
    public const string File = "scenario.json";

    public const string ListingFile = "listing.json";

    private const string Prefix = "sc-";

    /// <summary>A scenario kept: its id, what it is, and whether the operator listed it under Practice.</summary>
    internal sealed record Kept(string Id, ScenarioDocument Document, bool Listed);

    /// <summary>Keeps a scenario under a new id, which it returns.</summary>
    public async Task<string> PutAsync(ScenarioDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var id = $"{Prefix}{clock.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
        using var text = JsonDocument.Parse(ScenarioFile.Write(document));
        await store.Writer(id).WriteJsonAsync(File, text.RootElement, cancellationToken);
        return id;
    }

    /// <summary>The scenario kept under an id; none for an id that names none, or a file that is not one any more.</summary>
    public async Task<ScenarioDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Names(id))
        {
            return null;
        }

        var text = await store.Reader(id).ReadTextAsync(File, cancellationToken);
        if (text is null)
        {
            return null;
        }

        try
        {
            return ScenarioFile.Read(text);
        }
        catch (InvalidDataException failure)
        {
            Console.WriteLine($"  Scenario {id} cannot be read: {failure.Message}");
            return null;
        }
    }

    /// <summary>Every scenario kept, newest first, each with its id and whether it is listed.</summary>
    public async Task<IReadOnlyList<Kept>> ListAsync(CancellationToken cancellationToken = default)
    {
        var kept = new List<Kept>();
        foreach (var id in (await store.RunsAsync(cancellationToken)).Where(Names).OrderByDescending(one => one, StringComparer.Ordinal))
        {
            if (await GetAsync(id, cancellationToken) is { } document)
            {
                kept.Add(new Kept(id, document, await ListedAsync(id, cancellationToken)));
            }
        }

        return kept;
    }

    /// <summary>Whether the operator listed a scenario for signed-in players; a scenario is cut unlisted.</summary>
    public async Task<bool> ListedAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Names(id) || await store.Reader(id).ReadTextAsync(ListingFile, cancellationToken) is not { } text)
        {
            return false;
        }

        try
        {
            using var listing = JsonDocument.Parse(text);
            return listing.RootElement.TryGetProperty("listed", out var listed) && listed.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Lists a kept scenario for signed-in players, or takes it off the list: false when no scenario is kept under the id.</summary>
    public async Task<bool> SetListedAsync(string id, bool listed, CancellationToken cancellationToken = default)
    {
        if (await GetAsync(id, cancellationToken) is null)
        {
            return false;
        }

        using var listing = JsonDocument.Parse(listed ? """{"listed":true}""" : """{"listed":false}""");
        await store.Writer(id).WriteJsonAsync(ListingFile, listing.RootElement, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        Names(id) && await store.DeleteAsync(id, cancellationToken);

    /// <summary>Whether text is a scenario's id, which also keeps a request from naming anything else in the store.</summary>
    public static bool Names(string? id) =>
        id is { Length: > 3 and < 64 } && id.StartsWith(Prefix, StringComparison.Ordinal) && id.All(one => char.IsAsciiLetterOrDigit(one) || one == '-');
}
