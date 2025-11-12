using DA.Game.Domain2.Matches.Resources;

namespace DA.Game.Application.Matches.Ports;
public interface IGameResourcesProvider
{
    GameResources Get();
}