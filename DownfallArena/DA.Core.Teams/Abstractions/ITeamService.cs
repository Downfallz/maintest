using DA.Core.Domain.Base.Teams;

namespace DA.Core.Teams.Abstractions
{
    public interface ITeamService
    {
        Team InitializeNewTeam();
    }
}