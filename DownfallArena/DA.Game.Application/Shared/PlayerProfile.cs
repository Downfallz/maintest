using AutoMapper;
using DA.Game.Domain2.Match.Entities;
using DA.Game.Domain2.Match.ValueObjects;

namespace DA.Game.Application.Shared;
public class PlayerProfile : Profile
{
    public PlayerProfile()
    {
        CreateMap<Player, PlayerRef>()
                .ConstructUsing(p => PlayerRef.Create(p.Id, p.Kind, p.Name));
    }
}
