using AutoMapper;
using DA.Game.Application.Matches.DTOs;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Queries;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.Phases;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using static DA.Game.Domain2.Matches.Aggregates.Match;

namespace DA.Game.Application.Matches.Mapping;

public class MatchesProfile : Profile
{
    public MatchesProfile()
    {
        CreateMap<TurnCursor, TurnCursorDto>().ReverseMap();
        CreateMap<CombatTimeline, CombatTimelineDto>().ReverseMap();
        CreateMap<ActivationSlot, ActivationSlotDto>().ForMember(d => d.PlayerSlot, opt => opt.MapFrom(s => s.Owner))
    .ForMember(d => d.InitiativeValue, opt => opt.MapFrom(s => s.Initiative.Value))
    .ForMember(d => d.CreatureId, opt => opt.MapFrom(s=>s.CreatureId));
        CreateMap<CombatCreature, CombatCharacterDto>().ReverseMap();
        CreateMap<Team, TeamDto>().ReverseMap();
        CreateMap<SpellUnlockChoice, SpellUnlockChoiceDto>().ReverseMap();
        CreateMap<SpeedChoice, SpeedChoiceDto>().ReverseMap();
        CreateMap<CombatActionChoice, CombatActionChoiceDto>().ReverseMap();
        CreateMap<CombatActionIntent, CombatIntentDto>().ReverseMap();
        CreateMap<CreatureSnapshot, CharacterStatusDto>().ReverseMap();
        CreateMap<MatchLifecycle, MatchLifecycleDto>().ReverseMap();
        CreateMap<Round, RoundDto>().ReverseMap();
        CreateMap<Match, MatchDto>().ReverseMap();
        CreateMap<PlayerUnlockableSpells, PlayerUnlockableSpellsView>().ReverseMap();
        CreateMap<CreatureUnlockableSpells, CreatureUnlockableSpellsView>().ReverseMap();
        CreateMap<PlayerBoardState, PlayerBoardStateView>()
            .ForMember(
                d => d.CombatIntents,
                opt => opt.MapFrom(src => src.CombatIntentsByCreature.Values)
            );
        CreateMap<CreatureSnapshot, CombatCharacterDto>()
           .ForCtorParam(nameof(CombatCharacterDto.Id), opt => opt.MapFrom(s => s.CharacterId))
           .ForCtorParam(nameof(CombatCharacterDto.StartingSpellIds), opt => opt.MapFrom(s => s.KnownSpellIds))

           .ForCtorParam(nameof(CombatCharacterDto.BaseHealth), opt => opt.MapFrom(s => s.BaseHealth.Value))
           .ForCtorParam(nameof(CombatCharacterDto.BaseEnergy), opt => opt.MapFrom(s => s.BaseEnergy.Value))
           .ForCtorParam(nameof(CombatCharacterDto.BaseDefense), opt => opt.MapFrom(s => s.BaseDefense.Value))
           .ForCtorParam(nameof(CombatCharacterDto.BaseInitiative), opt => opt.MapFrom(s => s.BaseInitiative.Value))
           .ForCtorParam(nameof(CombatCharacterDto.BaseCritical), opt => opt.MapFrom(s => s.BaseCritical.Value))

           .ForCtorParam(nameof(CombatCharacterDto.Health), opt => opt.MapFrom(s => s.Health.Value))
           .ForCtorParam(nameof(CombatCharacterDto.Energy), opt => opt.MapFrom(s => s.Energy.Value))

           // Not available in snapshot (defaults for now)
           .ForCtorParam(nameof(CombatCharacterDto.ExtraPoint), opt => opt.MapFrom(_ => 0))
           .ForCtorParam(nameof(CombatCharacterDto.BonusRetaliate), opt => opt.MapFrom(_ => 0))

           .ForCtorParam(nameof(CombatCharacterDto.BonusDefense), opt => opt.MapFrom(s => s.BonusDefense.Value))
           .ForCtorParam(nameof(CombatCharacterDto.BonusCritical), opt => opt.MapFrom(s => s.BonusCritical.Value))

           .ForCtorParam(nameof(CombatCharacterDto.CurrentInitiative), opt => opt.MapFrom(s => s.Initiative.Value))

           .ForCtorParam(nameof(CombatCharacterDto.IsStunned), opt => opt.MapFrom(s => s.IsStunned))
           .ForCtorParam(nameof(CombatCharacterDto.IsAlive), opt => opt.MapFrom(s => s.IsAlive))
           .ForCtorParam(nameof(CombatCharacterDto.IsDead), opt => opt.MapFrom(s => s.IsDead))
            .ForCtorParam(nameof(CombatCharacterDto.KnownSpellIds), opt => opt.MapFrom(s => s.KnownSpellIds));
        CreateMap<PlayerOptions, PlayerOptionsView>();

        CreateMap<SpeedOptions, SpeedOptionsView>();
        CreateMap<EvolutionOptions, EvolutionOptionsView>();
        CreateMap<CombatPlanningOptions, CombatPlanningOptionsView>();
        CreateMap<CombatActionOptions, CombatActionOptionsView>();
        CreateMap<CombatStepOutcome, CombatStepOutcomeView>();
    }
}
