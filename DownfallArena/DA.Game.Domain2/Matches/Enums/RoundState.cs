namespace DA.Game.Domain2.Match.Enums;

public enum RoundState
{ 
    WaitingForPlayersEvolutionChoices, 
    WaitingForPlayersSpeedChoices,
    WaitingForPlayersCombatActions,
    ResolvingActions,
    Completed
}

