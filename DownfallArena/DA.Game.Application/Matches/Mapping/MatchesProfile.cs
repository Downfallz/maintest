using AutoMapper;
using DA.Game.Application.Matches.DTOs;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects;

namespace DA.Game.Application.Matches.Mapping;
public class MatchesProfile : Profile
{
    public MatchesProfile()
    {
        CreateMap<TurnCursor, TurnCursorDto>();
        CreateMap<CombatTimeline, CombatTimelineDto>();
        CreateMap<ActivationSlot, ActivationSlotDto>();
        CreateMap<CombatCharacter, CombatCharacterDto>();
        CreateMap<Team, TeamDto>();
        CreateMap<SpellUnlockChoice, SpellUnlockChoiceDto>();
        CreateMap<SpeedChoice, SpeedChoiceDto>();
        CreateMap<CharacterStatus, CharacterStatusDto>();
        CreateMap<MatchLifecycle, MatchLifecycleDto>();
        CreateMap<Round, RoundDto>();
        CreateMap<Match, MatchDto>();
    }
}
