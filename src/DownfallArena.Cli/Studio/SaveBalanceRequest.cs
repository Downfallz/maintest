using System.Text.Json;

namespace DownfallArena.Cli.Studio;

/// <summary>The whole balance knobs document, as the page wants it written (ADR 0025).</summary>
internal sealed record SaveBalanceRequest
{
    public JsonElement Balance { get; init; }
}
