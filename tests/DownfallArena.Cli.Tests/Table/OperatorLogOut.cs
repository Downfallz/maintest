using DownfallArena.Application.Admin;
using DownfallArena.Application.Admin.Ports;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>An operator log whose store is out, as one whose storage account does not answer.</summary>
internal sealed class OperatorLogOut : IOperatorLog
{
    public Task RecordAsync(OperatorAction action, CancellationToken cancellationToken = default) => throw new IOException("The log is out.");

    public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) => throw new IOException("The log is out.");

    public Task<OperatorAction?> FindAsync(string id, CancellationToken cancellationToken = default) => throw new IOException("The log is out.");
}
