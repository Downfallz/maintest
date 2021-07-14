using DA.Game;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services;
using DA.Game.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.Csl
{
    public class CslUiPlayerHandler : BasePlayerHandler
    {
        public CslUiPlayerHandler(IBattleEngine battleService) : base(battleService) { }

        public override void EvaluateCharacterToPlay(object sender, CharacterTurnInitializedEventArgs e)
        {
            Game.Domain.Models.GameFlowEngine.Character characterToPlay = MyAliveCharacters.SingleOrDefault(x => x.Id == e.CharacterId);
            if (characterToPlay != null)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Select your spell and target for character {characterToPlay.Name}");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"    {characterToPlay}");


                int spellCount = characterToPlay.CharacterTalentStats.UnlockedSpells.Count;
                List<int> availables = new List<int>();
                int numberPicked = 99;


                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"    Available spells to play:");
                for (int i = 0; i < spellCount; i++)
                {
                    Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Spell tal = characterToPlay.CharacterTalentStats.UnlockedSpells[i];
                    if (characterToPlay.CharacterTalentStats.UnlockedSpells.Where(x => x.EnergyCost <= characterToPlay.Energy).ToList().Contains(tal))
                    {
                        availables.Add(i);
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    Console.WriteLine($"{i} - {tal}");
                }

                while (!availables.Contains(numberPicked))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Pick a spell");
                    string readNumber = Console.ReadLine();
                    int.TryParse(readNumber, out numberPicked);
                }

                Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Spell spellToPlay = characterToPlay.CharacterTalentStats.UnlockedSpells[numberPicked];

                List<Guid> targets = new List<Guid>();
                if (spellToPlay.SpellType == SpellType.Defensive)
                {
                    int possibleTargetsCount = MyAliveCharacters.Count;
                    int? spellTargetCount = spellToPlay.NbTargets;

                    Console.WriteLine($"Spell can target {spellTargetCount} out of {possibleTargetsCount} possible targets.");
                    for (int i = 0; i < possibleTargetsCount; i++)
                    {
                        Console.WriteLine($"{i} - {MyAliveCharacters[i]}");
                    }

                    for (int i = 0; i < spellTargetCount; i++)
                    {
                        if (i >= possibleTargetsCount)
                        {
                            break;
                        }
                        Console.WriteLine($"Target {i + 1}");
                        string targetNumber = Console.ReadLine();
                        targets.Add(MyAliveCharacters[int.Parse(targetNumber)].Id);
                    }
                }
                else if (spellToPlay.SpellType == SpellType.Offensive)
                {
                    int possibleTargetsCount = MyEnemies.Count;
                    int? spellTargetCount = spellToPlay.NbTargets;

                    Console.WriteLine($"Spell can target {spellTargetCount} out of {possibleTargetsCount} possible targets.");
                    for (int i = 0; i < possibleTargetsCount; i++)
                    {
                        Console.WriteLine($"{i} - {MyEnemies[i]}");
                    }

                    for (int i = 0; i < spellTargetCount; i++)
                    {
                        if (i >= possibleTargetsCount)
                        {
                            break;
                        }
                        Console.WriteLine($"Target {i + 1}");
                        string targetNumber = Console.ReadLine();
                        targets.Add(MyEnemies[int.Parse(targetNumber)].Id);
                    }
                }

                BattleEngine.PlayAndResolveCharacterAction(Battle, new CharacterActionChoice()
                {
                    CharacterId = e.CharacterId,
                    Spell = spellToPlay,
                    Targets = targets
                });
            }
        }

        public override void SpellUnlock(object sender, EventArgs e)
        {
            List<SpellUnlockChoice> choices = new List<SpellUnlockChoice>();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Please choose your spell unlocks for 2 characters.");

            List<int> picked = new List<int>();
            for (int i = 0; i < 2; i++)
            {
                List<int> availables = new List<int>();
                int numberPicked = 99;
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"    Available characters:");
                for (int j = 0; j < MyAliveCharacters.Count; j++)
                {
                    if (!picked.Contains(j))
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine($"{j} - {MyAliveCharacters[j]}");
                        availables.Add(j);
                    }
                }
                while (!availables.Contains(numberPicked))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Pick character #{i + 1}");
                    string readNumber = Console.ReadLine();
                    int.TryParse(readNumber, out numberPicked);
                }

                picked.Add(numberPicked);
                Game.Domain.Models.GameFlowEngine.Character c = MyAliveCharacters[numberPicked];
                Console.ForegroundColor = ConsoleColor.Gray;

                List<Game.Domain.Models.GameFlowEngine.TalentsManagement.TalentNode> possibleList = c.TalentTreeStructure.Root.GetNextChildrenToUnlock();
                int possibleListCount = possibleList.Count;
                List<int> spellAvailables = new List<int>();
                int spellNumberPicked = 99;
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"    List of available spell to unlock");
                for (int j = 0; j < possibleListCount; j++)
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"{j} - {possibleList[j].Spell}");
                    spellAvailables.Add(j);
                }

                while (!spellAvailables.Contains(spellNumberPicked))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Pick a spell");
                    string readNumber = Console.ReadLine();
                    int.TryParse(readNumber, out spellNumberPicked);
                }

                choices.Add(new SpellUnlockChoice()
                {
                    CharacterId = c.Id,
                    Spell = possibleList[spellNumberPicked].Spell
                });
                if (MyAliveCharacters.Count <= 1)
                    break;
            }

            BattleEngine.ChooseSpellToUnlock(Battle, Indicator, choices);
        }

        public override void SpeedChoose(object sender, EventArgs e)
        {
            List<SpeedChoice> choices = new List<SpeedChoice>();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Choose your speed for every of your characters.");
            int count = 1;
            foreach (Game.Domain.Models.GameFlowEngine.Character c in MyAliveCharacters)
            {
                int number = 99;
                while (number != 0 && number != 1)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Choose your speed for character #{count}");
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"    {c}");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"0 - Standard");
                    Console.WriteLine($"1 - Quick");
                    string readNumber = Console.ReadLine();
                    int.TryParse(readNumber, out number);
                }

                choices.Add(new SpeedChoice()
                {
                    CharacterId = c.Id,
                    Speed = number == 0 ? Speed.Standard : Speed.Quick
                });
                count++;
            }

            BattleEngine.ChooseSpeed(Battle, Indicator, choices);
        }
    }
}
