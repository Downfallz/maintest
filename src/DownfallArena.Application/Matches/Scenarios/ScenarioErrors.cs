using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>Why a match scenario could not be cut or started.</summary>
public static class ScenarioErrors
{
    /// <summary>
    /// The round asked for is not one the recorded match offers: it never began, the match ended in it, or the
    /// journal stopped rebuilding before it.
    /// </summary>
    public static DomainError RoundUnavailable(int round) =>
        new("Scenario.RoundUnavailable", $"Round {round} cannot be cut: the match never began it, ended in it, or no longer rebuilds up to it.");

    /// <summary>
    /// The match rebuilt from a scenario does not stand where the scenario says its source stood: the engine
    /// changed under the scenario. Nothing is started on a board nobody recorded.
    /// </summary>
    public static readonly DomainError Diverged =
        new("Scenario.Diverged", "The match rebuilt from this scenario does not match the board recorded at its cut: the engine changed under it.");
}
