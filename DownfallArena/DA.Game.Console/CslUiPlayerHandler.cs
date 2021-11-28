using DA.Game;
using DA.Game.Domain.Services;
using DA.Game.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using DA.Game.Domain;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.CombatMechanic.Enum;
using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DA.Csl
{
    public class CslUiPlayerHandler : BasePlayerHandler
    {
        private List<string> LatestMessages { get; set; } = new List<string>();

        private Table BuildTeam(List<Character> characters)
        {
            // Create a table
            var myTeam = new Table();

            List<IRenderable> characterStats = new List<IRenderable>();
            foreach (var character in characters)
            {
                // Add some columns
                myTeam.AddColumn(character.Name).Centered();
                characterStats.Add(new Markup($"{character.Health}/{character.BaseHealth}"));
            }

            // Add some rows
            myTeam.AddRow(characterStats);

            return myTeam;
        }

        private void WriteDashboard()
        {
            AnsiConsole.Clear();
            // Create a table
            var myTeam = BuildTeam(MyAliveCharacters);
            var enemyTeam = BuildTeam(MyEnemies);

            // Create a table
            var parent = new Table();

            // Add some columns
            parent.AddColumn("Team One");
            parent.AddColumn("Team Two");

            parent.AddRow(myTeam, enemyTeam);
            // Render the table to the console
            AnsiConsole.Write(parent);

            AnsiConsole.Write(new Rule("Console"));
            WriteConsole();
            AnsiConsole.Write(new Rule("Play"));
        }

        private void WriteConsole()
        {
            foreach (var s in LatestMessages.TakeLast(15))
            {
                AnsiConsole.MarkupLine(s);
            }
        }
        public CslUiPlayerHandler(IBattleController battleService) : base(battleService) { }

        public override void EvaluateCharacterToPlay(object sender, CharacterTurnInitializedEventArgs e)
        {
            WriteDashboard();
            Character characterToPlay = MyAliveCharacters.SingleOrDefault(x => x.Id == e.CharacterId);
            
            if (characterToPlay != null)
            {
                AnsiConsole.MarkupLine($"[b]{characterToPlay.Name} (Team {characterToPlay.TeamNumber})'s turn.[/]");
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
                    Spell tal = characterToPlay.CharacterTalentStats.UnlockedSpells[i];
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

                Spell spellToPlay = characterToPlay.CharacterTalentStats.UnlockedSpells[numberPicked];

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
            else
            {
                Character enemyToPlay = MyEnemies.SingleOrDefault(x => x.Id == e.CharacterId);
                LatestMessages.Add($"[b]{enemyToPlay.Name} (Team {enemyToPlay.TeamNumber})'s turn.[/]");
            }
        }

        public override void SpellUnlock(object sender, EventArgs e)
        {
            

            LatestMessages.Add($"Round {Battle.CurrentRound.RoundNumber}");
            foreach (var cond in Battle.CurrentRound.RoundStartConditions)
            {
                LatestMessages.Add($"Condition {cond.StatModifierResult.TotalEffectiveValue} {cond.StatModifierResult.Effect.StatType} on {cond.StatModifierResult.TargetCharacterTeam} {cond.StatModifierResult.TargetCharacterName} |{cond.StatModifierResult.PostEffectStatsValue}|      - (rounds left: {cond.RoundsLeft})");
            }
            WriteDashboard();
            
            List<SpellUnlockChoice> choices = new List<SpellUnlockChoice>();
            AnsiConsole.MarkupLine($"[bold yellow on blue]Round {Battle.CurrentRound.RoundNumber}[/]");
            AnsiConsole.MarkupLine("[bold yellow on blue]Phase: Spell Unlock[/]");
            AnsiConsole.MarkupLine("[cyan]Please choose your spell unlocks for 2 characters.[/]");
            List<Guid> picked = new List<Guid>();
            for (int i = 0; i < 2; i++)
            {
                var pickedCharacter = AnsiConsole.Prompt(
                    new SelectionPrompt<Character>()
                        .Title("[yellow]Available characters:[/]")
                        .PageSize(10)
                        .AddChoices(MyAliveCharacters.Where(x => !picked.Contains(x.Id))));

                picked.Add(pickedCharacter.Id);

                List<Spell> possibleList = pickedCharacter.TalentTreeStructure.Root.GetNextChildrenToUnlock()
                    .Select(x => x.Spell).ToList();

                var pickedSpell = AnsiConsole.Prompt(
                    new SelectionPrompt<Spell>()
                        .Title("[yellow]List of available spell to unlock:[/]")
                        .PageSize(10)
                        .AddChoices(possibleList));
                
                choices.Add(new SpellUnlockChoice()
                {
                    CharacterId = pickedCharacter.Id,
                    Spell = pickedSpell
                });

                if (MyAliveCharacters.Count <= 1)
                    break;
            }

            BattleEngine.ChooseSpellToUnlock(Battle, Indicator, choices);
        }

        public override void SpeedChoose(object sender, EventArgs e)
        {
            WriteDashboard();
            List<SpeedChoice> choices = new List<SpeedChoice>();

            AnsiConsole.MarkupLine("[cyan]Choose your speed for every of your characters.[/]");

            int count = 1;
            foreach (Character c in MyAliveCharacters)
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
