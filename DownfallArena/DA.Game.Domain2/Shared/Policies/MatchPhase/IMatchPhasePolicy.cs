using DA.Game.Shared.Utilities;
namespace DA.Game.Domain2.Shared.Policies.MatchPhase;

public interface IMatchPhasePolicy
{
    Result EnsureCanSubmitEvolutionChoice(Matches.Aggregates.Match match);
    Result EnsureCanSubmitSpeedChoice(Matches.Aggregates.Match match);
    Result EnsureCanSubmitCombatAction(Matches.Aggregates.Match match);
}
