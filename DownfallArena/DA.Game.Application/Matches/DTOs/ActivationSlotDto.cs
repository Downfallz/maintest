namespace DA.Game.Application.Matches.DTOs;

using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

public sealed record ActivationSlotDto
{
    public PlayerSlot PlayerSlot { get; init; }
    public CreatureId CreatureId { get; init; }
    public SkillSpeed Speed { get; init; }
    public int InitiativeValue { get; init; }
}
