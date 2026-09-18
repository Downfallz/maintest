namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// What a playtest note is about (<c>docs/tabletop/playtest-app.md</c>, 5.3). Two kinds the host writes by
/// itself and three a player produces with one tap, because a note that takes three taps is a note nobody
/// writes.
/// </summary>
public enum NoteKind
{
    /// <summary>A decision was accepted, and how long the player took over it.</summary>
    Decision,

    /// <summary>A decision was refused, and the error the aggregate or the host's pre-check gave.</summary>
    Refused,

    /// <summary>A rule the player had to look up.</summary>
    Lookup,

    /// <summary>A play the player judges was wrong, which is the thing no artifact can infer.</summary>
    Misplay,

    /// <summary>Anything else the player wanted to say, typed on the end screen.</summary>
    Comment,
}
