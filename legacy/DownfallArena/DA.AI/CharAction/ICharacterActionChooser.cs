using System.Collections.Generic;
using System.Threading.Tasks;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;

namespace DA.AI.CharAction;

public interface ICharacterActionChooser
{
    Task<CharacterActionChoice> GetCharActionChoice(Battle battle, Character charToPlay, List<Character> aliveCharacters, List<Character> aliveEnemies);
}