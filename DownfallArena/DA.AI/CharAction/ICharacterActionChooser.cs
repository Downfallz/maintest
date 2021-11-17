using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;

namespace DA.AI.CharAction;

public interface ICharacterActionChooser
{
    CharacterActionChoice GetCharActionChoice(Battle battle, Character charToPlay, List<Character> aliveCharacters, List<Character> aliveEnemies);
}