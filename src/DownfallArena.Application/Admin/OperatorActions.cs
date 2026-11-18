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
    public const string FeedbackStatus = "feedback.status";
    public const string FeedbackConvert = "feedback.convert";
    public const string FeedbackSpam = "feedback.spam";
    public const string FeedbackMute = "feedback.mute";
    public const string FeedbackUnmute = "feedback.unmute";
    public const string FeedbackMerge = "feedback.merge";
    public const string FeedbackUnmerge = "feedback.unmerge";
    public const string FeedbackReproduce = "feedback.reproduce";
    public const string FeedbackDownload = "feedback.download";
    public const string FeedbackFile = "feedback.file";

    private readonly Lock _gate = new();
    private DateTimeOffset _lastStamp = DateTimeOffset.MinValue;

    /// <summary>Notes what an operator did, now.</summary>
    public async Task<OperatorAction> RecordAsync(string by, string action, string? target, string? detail, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        var at = clock.GetUtcNow();
        var noted = new OperatorAction(OperatorAction.NewId(Stamp(at)), at, by, action, target, detail);
        await log.RecordAsync(noted, cancellationToken);
        return noted;
    }

    /// <summary>
    /// The time an action's id carries: when it was taken, or a millisecond past the id this instance gave last
    /// (a host builds one, <c>TableHost.OpenNotesAsync</c>, and every panel shares it). Ids sort newest first, and
    /// two actions of one request share an instant: without this, their random tails would order them, and the
    /// Activity tab could show a mute below the spam mark that earned it (#674). <see cref="OperatorAction.At" />
    /// keeps the clock's own time. Between processes, their clocks order them.
    /// </summary>
    private DateTimeOffset Stamp(DateTimeOffset at)
    {
        var milliseconds = at.AddTicks(-(at.Ticks % TimeSpan.TicksPerMillisecond));
        lock (_gate)
        {
            _lastStamp = milliseconds > _lastStamp ? milliseconds : _lastStamp.AddMilliseconds(1);
            return _lastStamp;
        }
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
