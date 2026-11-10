using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// A witness that writes down, per seat, every question it sees asked and the answer to it: the board, the
/// options and the decision, as text. Wrapped around the agents of a driven match it is what a recording
/// agent sees there, which is what a stepped match has to show too.
/// </summary>
internal sealed class Noting : IDecisionWitness
{
    private readonly Lock _gate = new();
    private readonly List<(PlayerSlot Slot, string Note)> _notes = [];

    public IPlayerAgent Around(MatchId matchId, PlayerSlot slot, IPlayerAgent decider) => new Seen(this, slot, decider);

    /// <summary>What one seat was asked and answered, in order. Two seats asked at once interleave as they please; a seat's own order is fixed.</summary>
    public IReadOnlyList<string> Of(PlayerSlot slot)
    {
        lock (_gate)
        {
            return [.. _notes.Where(note => note.Slot == slot).Select(note => note.Note)];
        }
    }

    private TAnswer Note<TAnswer>(PlayerSlot slot, string asked, PlayerBoardState board, object? options, TAnswer answer)
    {
        var note = $"{asked} board={Normalized.Json(board)} options={Normalized.Json(options)} answer={Normalized.Json(answer)}";
        lock (_gate)
        {
            _notes.Add((slot, note));
        }

        return answer;
    }

    private sealed class Seen(Noting notes, PlayerSlot slot, IPlayerAgent inner) : IPlayerAgent
    {
        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) =>
            notes.Note(slot, "Evolution", board, options, inner.DecideEvolution(board, options));

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) =>
            notes.Note(slot, "Speed", board, creature, inner.DecideSpeed(board, creature));

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) =>
            notes.Note(slot, "TieOrder", board, options, inner.DecideTieOrder(board, options));

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) =>
            notes.Note(slot, "Intent", board, intentOption, inner.DecideIntent(board, intentOption));

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) =>
            notes.Note(slot, "Target", board, options, inner.DecideTargets(board, options));

        public ChosenAction DecideAction(PlayerBoardState board, ActionOptions options) =>
            notes.Note(slot, "Action", board, options, inner.DecideAction(board, options));
    }
}
