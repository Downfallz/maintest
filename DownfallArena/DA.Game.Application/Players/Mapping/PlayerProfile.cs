using AutoMapper;
using DA.Game.Domain2.Players.Entities;
using DA.Game.Shared;

namespace DA.Game.Application.Players.Mapping;
public class PlayerProfile : Profile
{
    public PlayerProfile()
    {
        CreateMap<Player, PlayerRef>()
                .ConstructUsing(p => PlayerRef.Create(p.Id, p.Kind, p.Name));
    }
}
