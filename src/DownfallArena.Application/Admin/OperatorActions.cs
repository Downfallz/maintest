using DownfallArena.Application.Admin.Ports;

namespace DownfallArena.Application.Admin;

/// <summary>
/// What operators did (admin plan, A6): an action noted as it is taken, and the kept ones read twenty a page, newest
/// first, for the Activity tab. The names an action is noted under are stable, since the tab reads them.
/// </summary>
public sealed class OperatorActions(IOperatorLog log, TimeProvider clock)
{
    public const int PerPage = 20;

    public const string OpenTable = "table.open";
    public const string CloseTable = "table.close";
    public const string Reinvite = "table.reinvite";
    public const string VoidMatch = "table.void";
    public const string DeleteSessions = "sessions.delete";
    public const string ExportSessions = "sessions.export";
    public const string SignOut = "player.sign-out";
    public const string Rename = "player.rename";
    public const string DeletePlayer = "player.delete";
    public const string Suspend = "player.suspend";
    public const string Restore = "player.restore";
    public const string RunPass = "jobs.tend";
    public const string Requeue = "turns.requeue";
    public const string Drop = "turns.drop";
    public const string SaveSettings = "settings.save";
    public const string CutScenario = "scenario.cut";
    public const string RemoveScenario = "scenario.remove";
    public const string ListScenario = "scenario.list";
    public const string UnlistScenario = "scenario.unlist";
    public const string RevealHidden = "table.reveal";
    public const string OweTurn = "table.turn";

    /// <summary>Notes what an operator did, now.</summary>
    public async Task<OperatorAction> RecordAsync(string by, string action, string? target, string? detail, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        var at = clock.GetUtcNow();
        var noted = new OperatorAction(OperatorAction.NewId(at), at, by, action, target, detail);
        await log.RecordAsync(noted, cancellationToken);
        return noted;
    }

    /// <summary>A page of the kept actions, newest first, counted from one; a page past the last is the last.</summary>
    public async Task<OperatorActionPage> PageAsync(int page, CancellationToken cancellationToken = default)
    {
        var ids = await log.IdsAsync(cancellationToken);
        var pages = Math.Max(1, (ids.Count + PerPage - 1) / PerPage);
        var shown = Math.Clamp(page, 1, pages);
        var read = await Task.WhenAll(ids.Skip((shown - 1) * PerPage).Take(PerPage).Select(id => log.FindAsync(id, cancellationToken)));
        return new OperatorActionPage([.. read.OfType<OperatorAction>()], ids.Count, shown, pages);
    }
}
