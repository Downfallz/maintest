using DA.Game.Domain.Models;
using DA.Game.Domain.Services;
using DA.Game.Domain.Services.TalentsManagement;

namespace DA.Game
{
    public class TeamService : ITeamService
    {
        private readonly ICharacterDevelopmentService _characterService;

        public TeamService(ICharacterDevelopmentService characterService)
        {
            _characterService = characterService;
        }

        public Team InitializeNewTeam()
        {
            Team team = new Team();
            Character char1 = _characterService.InitializeNewCharacter();
            char1.Name = "Creature 1";
            Character char2 = _characterService.InitializeNewCharacter();
            char2.Name = "Creature 2";
            Character char3 = _characterService.InitializeNewCharacter();
            char3.Name = "Creature 3";

            team.Characters.Add(char1);
            team.Characters.Add(char2);
            team.Characters.Add(char3);

            return team;
        }
    }
}