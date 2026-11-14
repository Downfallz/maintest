using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Infrastructure.Matches.Scenarios;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The match scenarios the operator cut (docs/tabletop/scenarios-plan.md, S4): one <c>scenario.json</c> a scenario,
/// under an id that sorts by when it was cut, in a store beside the recordings. The content each names is kept
/// beside them under its hash (ADR 0163), so a scenario does not carry it.
/// </summary>
internal sealed class ScenarioStore(IArtifactStore store, TimeProvider clock)
{
    public const string File = "scenario.json";

    private const string Prefix = "sc-";

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

    /// <summary>Every scenario kept, newest first, each with its id.</summary>
    public async Task<IReadOnlyList<(string Id, ScenarioDocument Document)>> ListAsync(CancellationToken cancellationToken = default)
    {
        var kept = new List<(string, ScenarioDocument)>();
        foreach (var id in (await store.RunsAsync(cancellationToken)).Where(Names).OrderByDescending(one => one, StringComparer.Ordinal))
        {
            if (await GetAsync(id, cancellationToken) is { } document)
            {
                kept.Add((id, document));
            }
        }

        return kept;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        Names(id) && await store.DeleteAsync(id, cancellationToken);

    /// <summary>Whether text is a scenario's id, which also keeps a request from naming anything else in the store.</summary>
    public static bool Names(string? id) =>
        id is { Length: > 3 and < 64 } && id.StartsWith(Prefix, StringComparison.Ordinal) && id.All(one => char.IsAsciiLetterOrDigit(one) || one == '-');
}
