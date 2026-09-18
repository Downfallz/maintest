using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// Where in a playtest a note was taken: which session, which match, which seat, and where the match had got
/// to. Every kind of <see cref="PlaytestNote" /> carries the same five, so they travel as one thing rather
/// than as five arguments in the same order at every call site.
/// </summary>
/// <remarks>
/// It is a parameter and not a property of the note: <c>notes.jsonl</c> writes the five flat, beside the kind
/// and what the kind needs, and a reader of that file should not have to walk into an object to find out which
/// round a line is about (<c>docs/learning/artifacts.md</c>).
/// </remarks>
/// <param name="SessionId">
/// The session, which is the name of its run directory. A plain string rather than a typed identifier: a
/// session is a directory the host named, not something the domain has an identity for.
/// </param>
public sealed record NotePlace(string SessionId, MatchId MatchId, PlayerSlot Slot, int? Round, RoundSubPhase? SubPhase);
