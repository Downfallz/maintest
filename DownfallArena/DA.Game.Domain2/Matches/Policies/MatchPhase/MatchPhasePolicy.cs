using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.MatchPhase;

public sealed class MatchPhasePolicyV1 : IMatchPhasePolicy
{
    public Result EnsureCanSubmitCombatAction(Aggregates.Match match)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.State != MatchState.Started)
            return Result.Fail("Invalid match phase.");

        if (match.CurrentRound is null)
            return Result.Fail("No active round.");

        return Result.Ok();
    }

    // Made static to address S2325 and CA1822.
    // Added a different error message to address S4144.
    public static Result EnsureCanResolveNextAction(Aggregates.Match match)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.State != MatchState.Started)
            return Result.Fail("Cannot resolve next action: invalid match phase.");

        if (match.CurrentRound is null)
            return Result.Fail("Cannot resolve next action: no active round.");

        return Result.Ok();
    }

    public Result EnsureCanSubmitEvolutionChoice(Aggregates.Match match)
    {
        return EnsureBasicGuard(match);
    }

    public Result EnsureCanSubmitSpeedChoice(Aggregates.Match match)
    {
        return EnsureBasicGuard(match);
    }

    private static Result EnsureBasicGuard(Aggregates.Match match)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.State != MatchState.Started)
            return Result.Fail("Invalid match phase.");

        if (match.CurrentRound is null)
            return Result.Fail("No active round.");

        if (match.Player1Team is null || match.Player2Team is null)
            return Result.Fail("Teams are not initialized.");

        return Result.Ok();
    }
}
