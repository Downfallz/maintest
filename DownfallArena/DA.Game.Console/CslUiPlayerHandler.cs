using DA.Game;
using DA.Game.Domain.Services;
using DA.Game.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
            myTeam.Border = TableBorder.MinimalHeavyHead;

            List<IRenderable> characterStats = new List<IRenderable>();
            List<IRenderable> spellList = new List<IRenderable>();
            List<IRenderable> playSpeed = new List<IRenderable>();
            foreach (var character in characters)
            {
                // Add some columns
                myTeam.AddColumn(character.Name).Width(80).Centered();
                var sb = new StringBuilder();
                sb.AppendLine($"[lime]{ character.Health}[/][white]/{ character.BaseHealth}[/]");
                sb.AppendLine($"[blue]{ character.Energy}[/]");
                sb.AppendLine($"[gold1]{ character.Initiative}[/]");
                sb.AppendLine($"[darkorange3_1]{ character.ExtraPoint}[/]");
                sb.AppendLine($"[cyan]{ character.IsStunned}[/]");
                var charStatsPanel = new Panel(sb.ToString()).Padding(0, 0, 15, 0);
                charStatsPanel.Border = BoxBorder.Rounded;

                characterStats.Add(charStatsPanel);

                var sbSpell = new StringBuilder();
                foreach (var s in character.CharacterTalentStats.UnlockedSpells)
                {
                    sbSpell.AppendLine($"{s.Name}");
                }


                var spellPanel = new Panel(sbSpell.ToString());
                charStatsPanel.Border = BoxBorder.Rounded;
                spellList.Add(spellPanel);

                var sp = Battle.CurrentRound.AllSpeedChoice.SingleOrDefault(x => x.CharacterId == character.Id)?.Speed;
                var speedPanel = new Markup((sp == null ? "" : sp.ToString()) ?? string.Empty);
                playSpeed.Add(speedPanel);
            }

            // Add some rows
            myTeam.AddRow(characterStats);
            myTeam.AddRow(spellList);
            myTeam.AddRow(playSpeed);
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

            Character characterToPlay = MyAliveCharacters.SingleOrDefault(x => x.Id == e.CharacterId);

            if (characterToPlay != null)
            {
                WriteDashboard();
                AnsiConsole.MarkupLine($"[bold yellow on blue]Round {Battle.CurrentRound.RoundNumber}[/]");
                AnsiConsole.MarkupLine("[bold yellow on blue]Phase: Action[/]");
                AnsiConsole.MarkupLine($"[cyan]{characterToPlay.Name} (Team {characterToPlay.TeamNumber})'s turn.[/]");
                AnsiConsole.MarkupLine("");

                var selectPrompt = new SelectionPrompt<Spell>()
                    .Title("[yellow]Spells:[/]")
                    .PageSize(10)
                    .AddChoices(
                        characterToPlay.CharacterTalentStats.UnlockedSpells.Where(x =>
                            x.EnergyCost <= characterToPlay.Energy));

                var pickedSpell = AnsiConsole.Prompt(selectPrompt);

                List<Guid> targets = new List<Guid>();
                if (pickedSpell.SpellType == SpellType.Defensive)
                {
                    int possibleTargetsCount = MyAliveCharacters.Count;
                    int? spellTargetCount = pickedSpell.NbTargets;

                    Console.WriteLine($"Spell can target {spellTargetCount} out of {possibleTargetsCount} possible targets.");

                    List<Character> pickedTargets;
                    do
                    {
                        pickedTargets = AnsiConsole.Prompt(new MultiSelectionPrompt<Character>()
                            .Title($"[yellow]Spell can target {spellTargetCount} out of {possibleTargetsCount} possible targets:[/]")
                            .PageSize(10).Required()
                            .AddChoices(MyAliveCharacters));
                    } while (pickedTargets.Count != spellTargetCount || pickedTargets.Count != possibleTargetsCount );

                    targets = pickedTargets.Select(x => x.Id).ToList();
                }
                else if (pickedSpell.SpellType == SpellType.Offensive)
                {
                    int possibleTargetsCount = MyEnemies.Count;
                    int? spellTargetCount = pickedSpell.NbTargets;

                    Console.WriteLine($"Spell can target {spellTargetCount} out of {possibleTargetsCount} possible targets.");

                    List<Character> pickedTargets;
                    do
                    {
                        pickedTargets = AnsiConsole.Prompt(new MultiSelectionPrompt<Character>()
                            .Title($"[yellow]Spell can target {spellTargetCount} out of {possibleTargetsCount} possible targets:[/]")
                            .PageSize(10).Required()
                            .AddChoices(MyEnemies));
                    } while (pickedTargets.Count > spellTargetCount);

                    targets = pickedTargets.Select(x => x.Id).ToList();
                }

                BattleEngine.PlayAndResolveCharacterAction(Battle, new CharacterActionChoice()
                {
                    CharacterId = e.CharacterId,
                    Spell = pickedSpell,
                    Targets = targets
                });
            }
            else
            {
                Character enemyToPlay = MyEnemies.SingleOrDefault(x => x.Id == e.CharacterId);
                LatestMessages.Add($"[b]{enemyToPlay.Name} (Team {enemyToPlay.TeamNumber})'s turn.[/]");
            }
        }

        public override void CharacterPlayed(object sender, CharacterPlayedEventArgs e)
        {
            LatestMessages.Add($"{e.SpellResolverResult?.SourceCharInfo.Name} (Team {e.SpellResolverResult?.SourceCharInfo.Team}) played {e.SpellResolverResult?.Spell.Name} on {string.Join(",", e.SpellResolverResult?.TargetsCharInfo.Select(x => x.Name).ToList() ?? new List<string>())}");
        }

        public override void SpellUnlock(object sender, EventArgs e)
        {
            LatestMessages.Add($"Round {Battle.CurrentRound.RoundNumber}");
            foreach (var cond in Battle.CurrentRound.RoundStartConditions)
            {
                LatestMessages.Add($"Condition {cond?.StatModifierResult?.TotalEffectiveValue} {cond?.StatModifierResult?.Effect.StatType} on {cond?.StatModifierResult?.TargetCharacterTeam} {cond?.StatModifierResult?.TargetCharacterName} |{cond?.StatModifierResult?.PostEffectStatsValue}|      - (rounds left: {cond?.RoundsLeft})");
            }
            WriteDashboard();

            List<SpellUnlockChoice> choices = new List<SpellUnlockChoice>();
            AnsiConsole.MarkupLine($"[bold yellow on blue]Round {Battle.CurrentRound.RoundNumber}[/]");
            AnsiConsole.MarkupLine("[bold yellow on blue]Phase: Spell Unlock[/]");
            AnsiConsole.MarkupLine("[cyan]Please choose your spell unlocks for 2 characters.[/]");
            AnsiConsole.MarkupLine("");
            List<Guid> picked = new List<Guid>();
            for (int i = 0; i < 2; i++)
            {
                var selectPrompt = new SelectionPrompt<Character>()
                    .Title("[yellow]Available characters:[/]")
                    .PageSize(10)
                    .AddChoices(MyAliveCharacters.Where(x => !picked.Contains(x.Id)));
                selectPrompt.Converter = x => x.Name;

                var pickedCharacter = AnsiConsole.Prompt(selectPrompt);

                picked.Add(pickedCharacter.Id);

                List<Spell> possibleList = pickedCharacter.TalentTreeStructure.Root.GetNextChildrenToUnlock()
                    .Select(x => x.Spell).ToList();

                var pickedSpell = AnsiConsole.Prompt(
                    new SelectionPrompt<Spell>()
                        .Title($"[yellow]Spells for Team {pickedCharacter.TeamNumber} {pickedCharacter.Name}:[/]")
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
            List<SpeedChoice> choices = new List<SpeedChoice>();

            foreach (Character c in MyAliveCharacters)
            {
                WriteDashboard();
                AnsiConsole.MarkupLine($"[bold yellow on blue]Round {Battle.CurrentRound.RoundNumber}[/]");
                AnsiConsole.MarkupLine("[bold yellow on blue]Phase: Choose Speed[/]");
                AnsiConsole.MarkupLine("[cyan]Choose your speed for every of your characters.[/]");
                AnsiConsole.MarkupLine("");
                var pickedSpeed = AnsiConsole.Prompt(
                    new SelectionPrompt<Speed>()
                        .Title($"[yellow]Speed for Team {c.TeamNumber} {c.Name}:[/]")
                        .PageSize(10)
                        .AddChoices(Enum.GetValues(typeof(Speed)).Cast<Speed>()));

                choices.Add(new SpeedChoice()
                {
                    CharacterId = c.Id,
                    Speed = pickedSpeed
                });
            }

            BattleEngine.ChooseSpeed(Battle, Indicator, choices);
        }
    }
}
