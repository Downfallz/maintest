using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>Why a match could not be played from its journal.</summary>
public static class JournalErrors
{
    /// <summary>The code of <see cref="Diverged" />, whose message names the line.</summary>
    public const string DivergedCode = "Journal.Diverged";

    /// <summary>
    /// Another writer took the match further between this one's reading and its writing. Nothing of this
    /// writer's was kept: read the match again and decide again.
    /// </summary>
    public static readonly DomainError Moved = new("Journal.Moved", "The match moved on while this decision was on its way. Read it again.");

    /// <summary>
    /// A seat cannot be told to change hands from the top of a round the match has left: whoever sat there has
    /// played it.
    /// </summary>
    public static readonly DomainError RoundReached = new("Journal.RoundReached", "The match has already played that round. A seat changes hands from the top of a later one.");

    /// <summary>A journal that holds a timeout (ADR 0169), replayed by something made without the command that times a match out.</summary>
    public static readonly DomainError CannotTimeOut = new("Journal.CannotTimeOut", "This replay cannot time a match out, and the journal holds a timeout.");

    /// <summary>
    /// A line does not answer what the rebuilt match asks: the content, the rules or the engine changed under
    /// the journal. Nothing is guessed at, and the journal is left as it is.
    /// </summary>
    public static DomainError Diverged(int line, DomainError refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new(DivergedCode, $"Line {line} of the journal does not answer the match being rebuilt ({refusal.Code}): the content, the rules or the engine changed under it.");
    }
}
