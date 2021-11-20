using System.Collections.Generic;
using System.Threading.Tasks;
using DA.Game.Domain.Models;

namespace DA.AI.CharAction;

public interface IBestCharacterActionChoicePicker
{
    Task<BestPick> GetCharActionBestPick(Battle battle, Character charToPlay, List<Character> aliveCharacters, List<Character> aliveEnemies);
}