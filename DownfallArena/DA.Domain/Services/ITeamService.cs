using DA.Game.Domain.Models.GameFlowEngine;

namespace DA.Game.Domain.Services.GameFlowEngine
{
    public interface ITeamService
    {
        Team InitializeNewTeam();
    }
}