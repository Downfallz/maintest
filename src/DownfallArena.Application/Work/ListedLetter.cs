namespace DownfallArena.Application.Work;

/// <summary>
/// A dead letter as the Jobs tab lists it: the queue's own id for it, what it says, and the table its item owed a turn
/// at, when its item is one. <see cref="Letter" /> is null for an item the dead-letter queue holds that is not a letter.
/// </summary>
public sealed record ListedLetter(string Id, DeadLetter? Letter, string? Table, string Raw);
