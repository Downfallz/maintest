using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// What a Tutorial says at one question (ADR 0187): the question named by its kind, round and creature, two lines
/// at most, the Concept cards it opens and what it points at. It shows by the question asked, never by an exact
/// board.
/// </summary>
/// <param name="Kind">The kind of question it shows at, one the lesson teaches.</param>
/// <param name="Round">The round the question is asked in, as the board numbers it.</param>
/// <param name="Creature">The creature asked about; none for a question asked of the player, or for any creature.</param>
/// <param name="Says">What the coach card says: one or two lines.</param>
/// <param name="Concepts">The Concept cards it opens, by id (<see cref="ConceptCards" />).</param>
/// <param name="PointsAt">The creature or the control it points at, if any.</param>
public sealed record LessonStep(PlayerOptionsKind Kind, int Round, CreatureId? Creature, IReadOnlyList<string> Says, IReadOnlyList<string> Concepts, LessonPointer? PointsAt = null)
{
    /// <summary>Whether the step shows at a question asked of the person's seat in a round.</summary>
    public bool ShowsAt(SeatQuestion asked, int round)
    {
        ArgumentNullException.ThrowIfNull(asked);
        return asked.Kind == Kind && round == Round && (Creature is null || asked.Creature == Creature);
    }
}
