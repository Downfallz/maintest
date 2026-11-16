using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches;

public static class MatchErrors
{
    public static readonly DomainError AlreadyStarted = new("Match.AlreadyStarted", "The match no longer accepts players.");

    public static readonly DomainError PlayerAlreadyJoined = new("Match.PlayerAlreadyJoined", "This player is already in the match.");

    public static readonly DomainError WrongTeamSize = new("Match.WrongTeamSize", "The roster must have exactly the team size of the rule set.");

    public static readonly DomainError UnknownCreatureDefinition = new("Match.UnknownCreatureDefinition", "The roster names a creature definition that is not in the game resources.");

    public static readonly DomainError NotInProgress = new("Match.NotInProgress", "The match has not started or has already ended.");

    public static readonly DomainError NobodyTimedOut = new("Match.NobodyTimedOut", "A match times out for at least one player who let their time to answer run out.");
}
