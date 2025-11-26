using AutoMapper;
using DA.Game.Application.Matches.DTOs;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.MatchVo;
using DA.Game.Domain2.Matches.ValueObjects.RoundVo;
using DA.Game.Domain2.Matches.ValueObjects.SpeedVo;

namespace DA.Game.Application.Matches.Mapping;

public class MatchesProfile : Profile
{
    public MatchesProfile()
    {
        CreateMap<TurnCursor, TurnCursorDto>().ReverseMap();
        CreateMap<CombatTimeline, CombatTimelineDto>().ReverseMap();
        CreateMap<ActivationSlot, ActivationSlotDto>().ReverseMap();
        CreateMap<CombatCreature, CombatCharacterDto>().ReverseMap();
        CreateMap<Team, TeamDto>().ReverseMap();
        CreateMap<SpellUnlockChoice, SpellUnlockChoiceDto>().ReverseMap();
        CreateMap<SpeedChoice, SpeedChoiceDto>().ReverseMap();
        CreateMap<CombatActionChoice, CombatActionChoiceDto>().ReverseMap();
        CreateMap<CharacterSnapshot, CharacterStatusDto>().ReverseMap();
        CreateMap<MatchLifecycle, MatchLifecycleDto>().ReverseMap();
        CreateMap<Round, RoundDto>().ReverseMap();
        CreateMap<Match, MatchDto>().ReverseMap();
    }
}
