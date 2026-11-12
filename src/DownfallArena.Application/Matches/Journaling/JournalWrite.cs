namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// The last write of a journal as its store keeps it, read in one reading: when it was made, and where the journal
/// ended after it (<see cref="JournalPosition.Mark" />). What a timeout is conditional on (ADR 0169): a journal that
/// no longer ends at that mark was written since, and its time did not run out.
/// </summary>
/// <param name="At">When the journal was last written.</param>
/// <param name="Mark">Where the journal ended then, as the store measures it.</param>
public sealed record JournalWrite(DateTimeOffset At, long Mark);
