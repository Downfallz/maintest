using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Shared.Policies.MatchPhase;

public sealed class MatchPhasePolicyV1 : IMatchPhasePolicy
{


    public Result EnsureCanSubmitCombatAction(Matches.Aggregates.Match match)
    {
        if (match.State != MatchState.Started)
            return Result.Fail("Invalid match phase.");

        if (match.CurrentRound is null)
            return Result.Fail("No active round.");

        return Result.Ok();
    }

    public Result EnsureCanResolveNextAction(Matches.Aggregates.Match match)
    {
        if (match.State != MatchState.Started)
            return Result.Fail("Invalid match phase.");

        if (match.CurrentRound is null)
            return Result.Fail("No active round.");

        return Result.Ok();
    }



    public Result EnsureCanSubmitEvolutionChoice(Matches.Aggregates.Match match)
    {
        return EnsureBasicGuard(match);
    }

    public Result EnsureCanSubmitSpeedChoice(Matches.Aggregates.Match match)
    {
        return EnsureBasicGuard(match);
    }

    private Result EnsureBasicGuard(Matches.Aggregates.Match match)
    {
        if (match.State != MatchState.Started)
            return Result.Fail("Invalid match phase.");

        if (match.CurrentRound is null)
            return Result.Fail("No active round.");

        if (match.Player1Team is null || match.Player2Team is null)
            return Result.Fail("Teams are not initialized.");

        return Result.Ok();
    }
}
