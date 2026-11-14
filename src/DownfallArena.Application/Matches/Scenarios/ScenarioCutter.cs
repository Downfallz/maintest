using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// Cuts a recorded match at the top of a round (docs/tabletop/scenarios-plan.md, stage S1): a match scenario.
/// </summary>
public sealed class ScenarioCutter(ScenarioRounds rounds, MatchReplay replay, MatchStepper stepper)
{
    /// <summary>
    /// The recorded match cut at the top of <paramref name="round" />: the entries before and after the cut, and
    /// both seats' boards there, read off the source rebuilt to that point. Refused with
    /// <see cref="ScenarioErrors.RoundUnavailable" /> for a round <see cref="ScenarioRounds" /> does not offer.
    /// </summary>
    public async Task<Result<ScenarioCut>> CutAsync(JournaledMatch source, IReadOnlyList<IJournalEntry> entries, int round, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entries);

        var reading = await rounds.ReadAsync(source, entries, cancellationToken);
        if (reading.Rounds.FirstOrDefault(one => one.Round == round) is not { } cut)
        {
            return Result.Failure<ScenarioCut>(ScenarioErrors.RoundUnavailable(round));
        }

        var before = entries.Take(cut.CutAt).ToArray();
        var checkpoint = await ScenarioBoards.AtAsync(replay, stepper, source, before, round, cancellationToken);
        return checkpoint.IsFailure
            ? Result.Failure<ScenarioCut>(checkpoint.Error)
            : Result.Success(new ScenarioCut(source, cut, before, [.. entries.Skip(cut.CutAt)], checkpoint.Value));
    }
}
