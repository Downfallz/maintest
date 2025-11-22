using DA.Game.Shared.Utilities;
namespace DA.Game.Domain2.Matches.Policies.MatchPhase;

public interface IMatchPhasePolicy
{
    Result EnsureCanSubmitEvolutionChoice(Aggregates.Match match);
    Result EnsureCanSubmitSpeedChoice(Aggregates.Match match);
    Result EnsureCanSubmitCombatAction(Aggregates.Match match);
}
