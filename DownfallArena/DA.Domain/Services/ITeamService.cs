using DA.Game.Domain.Models;

namespace DA.Game.Domain.Services
{
    public interface ITeamService
    {
        Team InitializeNewTeam();
    }
}