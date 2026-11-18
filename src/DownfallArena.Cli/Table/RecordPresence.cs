namespace DownfallArena.Cli.Table;

/// <summary>
/// What a side file's keeper knows of the table's record before it writes beside it (#850): kept, gone because the
/// operator deleted the run, or unknown because this process never saw it stored (its writes failed).
/// </summary>
internal enum RecordPresence
{
    /// <summary>The record is there: a side file may be written beside it.</summary>
    Kept,

    /// <summary>The record this process wrote or read is gone: the operator deleted the run.</summary>
    Gone,

    /// <summary>No record, and this process never saw one stored: its writes failed, and nothing says the run was deleted.</summary>
    Unknown,
}
