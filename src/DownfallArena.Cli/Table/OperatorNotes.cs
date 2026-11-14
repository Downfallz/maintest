using System.Text.Json;
using DownfallArena.Application.Admin;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What an operator did from the panel, noted in the operator log (admin plan, A6) beside the console line that says
/// it. A log that cannot be written says so on the console and loses that one line, never the action it notes. A host
/// that records nothing has no log, and notes nothing.
/// </summary>
internal sealed class OperatorNotes(OperatorActions? actions)
{
    /// <summary>The notes of a host without an operator log: none.</summary>
    public static OperatorNotes None { get; } = new(null);

    /// <summary>The log the Activity tab reads, or none on a host without one.</summary>
    public OperatorActions? Actions => actions;

    public async Task NoteAsync(string by, string action, string? target, string? detail = null)
    {
        if (actions is null)
        {
            return;
        }

        try
        {
            await actions.RecordAsync(by, action, target, detail);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is JsonException)
        {
            Console.WriteLine($"  The operator log could not keep {action} on {target ?? "the host"} by {by}: {failure.Message}");
        }
    }
}
