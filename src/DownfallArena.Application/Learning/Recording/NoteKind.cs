namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// What a playtest note is about (<c>docs/tabletop/playtest-app.md</c>, 5.3). Three kinds the host writes by
/// itself and three a player produces with one tap, because a note that takes three taps is a note nobody
/// writes.
/// </summary>
public enum NoteKind
{
    /// <summary>A decision was accepted, and how long the player took over it.</summary>
    Decision,

    /// <summary>A decision was refused, and the error the aggregate or the host's pre-check gave.</summary>
    Refused,

    /// <summary>
    /// The pilot asked a seat to change hands: who held it, who is to take it, and the round it happens at
    /// the top of.
    /// </summary>
    /// <remarks>
    /// It records the asking rather than the landing, and the two are different moments: a swap names a round
    /// the match has not reached, so it lands later and may never land at all if the match ends first. What
    /// actually happened is on the steps, where <see cref="StepRecord.DecidedBy" /> names whoever decided
    /// each one; this note is the operator's action, beside their lookups and misplays.
    /// </remarks>
    Seat,

    /// <summary>A rule the player had to look up.</summary>
    Lookup,

    /// <summary>A play the player judges was wrong, which is the thing no artifact can infer.</summary>
    Misplay,

    /// <summary>Anything else the player wanted to say, typed on the end screen.</summary>
    Comment,
}
