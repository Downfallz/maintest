namespace DA.Game.Application.Matches.DTOs;

public sealed record TurnCursorDto
{
    public int Index { get; init; }
    public bool IsEnd { get; init; }
}

