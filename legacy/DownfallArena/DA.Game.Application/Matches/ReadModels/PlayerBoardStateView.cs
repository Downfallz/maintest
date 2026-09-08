using DA.Game.Application.Matches.DTOs;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.ReadModels;

public sealed record PlayerBoardStateView
{
    public required MatchId MatchId { get; init; }
    public required PlayerSlot Slot { get; init; }
    public required MatchState MatchState { get; init; }

    public RoundPhase? RoundPhase { get; init; }
    public RoundSubPhase? RoundSubPhase { get; init; }

    public required IReadOnlyList<CombatCharacterDto> FriendlyCreatures { get; init; }
    public required IReadOnlyList<CombatCharacterDto> EnemyCreatures { get; init; }

    public IReadOnlyCollection<SpellUnlockChoiceDto> EvolutionChoices { get; init; } = Array.Empty<SpellUnlockChoiceDto>();
    public IReadOnlyCollection<SpeedChoiceDto> SpeedChoices { get; init; } = Array.Empty<SpeedChoiceDto>();
    public IReadOnlyList<CombatIntentDto> CombatIntents { get; init; }
        = Array.Empty<CombatIntentDto>();


    public CombatTimelineDto? Timeline { get; init; }
    public TurnCursorDto? RevealCursor { get; init; }
    public TurnCursorDto? ResolveCursor { get; init; }
}
