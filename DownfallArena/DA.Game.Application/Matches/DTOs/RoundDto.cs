using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Application.Matches.DTOs;


public sealed record RoundDto(
    RoundId Id,
    int Number,
    RoundPhase Phase,
    CombatTimelineDto Timeline,
    TurnCursorDto Cursor,
    IReadOnlyCollection<SpellUnlockChoiceDto> Player1EvolutionChoices,
    IReadOnlyCollection<SpellUnlockChoiceDto> Player2EvolutionChoices,
    IReadOnlyCollection<SpeedChoiceDto> Player1SpeedChoices,
    IReadOnlyCollection<SpeedChoiceDto> Player2SpeedChoices,
    bool IsCombatOver,
    bool IsEvolutionPhaseComplete,
    bool IsSpeedChoicePhaseComplete
);
