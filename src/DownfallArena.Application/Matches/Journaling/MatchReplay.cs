using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// Builds a match again from how it was created and what its journal holds (ADR 0159): created under the same
/// id from the same seed, then handed every decision in order and conceded where it was, with nobody asked
/// anything.
/// </summary>
/// <remarks>
/// The aggregate is rebuilt by the commands that built it the first time, through the same gates: no memento,
/// no way to make a match the rules did not produce. Nothing is written and nobody is told; the events are
/// raised again, so what remembers them forgets the match first (<see cref="IMatchMemory" />). A seat changing
/// hands is passed over: every decision is in the journal whoever took it.
/// </remarks>
public sealed class MatchReplay(
    ICommandHandler<CreateMatch, Result<MatchId>> create,
    ICommandHandler<JoinMatch, Result<PlayerSlot>> join,
    ICommandHandler<Concede, Result> concede,
    MatchStepper stepper,
    IMatchRepository matches,
    IEnumerable<IMatchMemory> memories)
{
    private readonly IReadOnlyList<IMatchMemory> _memories = [.. memories];

    /// <summary>
    /// The match as the entries leave it, and the answers it is still holding. Refused with
    /// <see cref="JournalErrors.Diverged" /> at the first entry the rebuilt match does not take.
    /// </summary>
    public async Task<Result<HeldAnswers>> RebuildAsync(JournaledMatch match, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(entries);

        await matches.ForgetAsync(match.MatchId, cancellationToken);
        foreach (var memory in _memories)
        {
            memory.Forget(match.MatchId);
        }

        var created = await create.HandleAsync(new CreateMatch(match.Rules, match.Seed, match.MatchId), cancellationToken);
        if (created.IsFailure)
        {
            return Result.Failure<HeldAnswers>(created.Error);
        }

        foreach (var _ in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
        {
            var joined = await join.HandleAsync(new JoinMatch(match.MatchId, PlayerId.New(), match.Roster), cancellationToken);
            if (joined.IsFailure)
            {
                return Result.Failure<HeldAnswers>(joined.Error);
            }
        }

        var held = HeldAnswers.None;
        for (var line = 0; line < entries.Count; line++)
        {
            switch (entries[line])
            {
                case DecisionEntry decided:
                    var step = await stepper.SubmitAsync(match.MatchId, MatchSeats.People, held, decided.Decision, cancellationToken);
                    if (step.IsFailure)
                    {
                        return Result.Failure<HeldAnswers>(JournalErrors.Diverged(line + 1, step.Error));
                    }

                    held = step.Value.Held;
                    break;
                case ConcessionEntry conceded:
                    var given = await concede.HandleAsync(new Concede(match.MatchId, conceded.Slot), cancellationToken);
                    if (given.IsFailure)
                    {
                        return Result.Failure<HeldAnswers>(JournalErrors.Diverged(line + 1, given.Error));
                    }

                    held = HeldAnswers.None;
                    break;
                default:
                    break;
            }
        }

        return Result.Success(held);
    }
}
