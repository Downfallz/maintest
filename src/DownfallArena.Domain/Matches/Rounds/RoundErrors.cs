using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rounds;

public static class RoundErrors
{
    public static readonly DomainError EvolutionNotOpen = new("Round.EvolutionNotOpen", "Evolution choices are not accepted in the current sub-phase.");

    public static readonly DomainError EvolutionAlreadySubmitted = new("Round.EvolutionAlreadySubmitted", "This evolution choice was already submitted.");

    public static readonly DomainError EvolutionAlreadyPassed = new("Round.EvolutionAlreadyPassed", "This player already passed their evolution picks for the round.");

    public static readonly DomainError SpeedNotOpen = new("Round.SpeedNotOpen", "Speed choices are not accepted in the current sub-phase.");

    public static readonly DomainError SpeedAlreadyChosen = new("Round.SpeedAlreadyChosen", "A speed was already chosen for this creature.");

    public static readonly DomainError IntentsNotOpen = new("Round.IntentsNotOpen", "Combat intents are not accepted in the current sub-phase.");

    public static readonly DomainError IntentAlreadySubmitted = new("Round.IntentAlreadySubmitted", "An intent was already submitted for this creature.");

    public static readonly DomainError TargetingNotOpen = new("Round.TargetingNotOpen", "Combat actions are not accepted in the current sub-phase.");

    public static readonly DomainError NothingLeftToReveal = new("Round.NothingLeftToReveal", "Every intent of the timeline has been revealed and targeted.");

    public static readonly DomainError NotThisCreaturesTurn = new("Round.NotThisCreaturesTurn", "The next intent to reveal belongs to another creature.");

    public static readonly DomainError ResolutionNotOpen = new("Round.ResolutionNotOpen", "No combat action is waiting for resolution in the current sub-phase.");

    public static readonly DomainError ActionDoesNotMatchIntent = new("Round.ActionDoesNotMatchIntent", "The action must use the spell declared in the intent.");
}
