using System.Globalization;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli;

/// <summary>
/// A human at the console: every decision is a numbered menu built from the player's options.
/// </summary>
internal sealed class ConsoleAgent(TextReader input, TextWriter output) : IPlayerAgent
{
    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ShowBoard(board);
        output.WriteLine($"Evolution: {options.RemainingPicks} pick(s) left.");
        List<EvolutionChoice> choices = [.. options.Creatures.SelectMany(creature => creature.UnlockableSpells.Select(spell => new EvolutionChoice(creature.Creature, spell)))];
        var picked = Pick("Unlock", [.. choices.Select(choice => $"creature {choice.Creature}: {choice.Spell.Value}")], allowNone: "pass");
        return picked is { } index ? EvolutionDecision.Unlock(choices[index]) : EvolutionDecision.Pass;
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
    {
        var index = Pick($"Speed of creature {creature}", ["Quick", "Standard"]);
        return index == 0 ? Speed.Quick : Speed.Standard;
    }

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        ArgumentNullException.ThrowIfNull(intentOption);

        ShowBoard(board);
        var index = Pick($"Intent of creature {intentOption.Creature}", [.. intentOption.CastableSpells.Select(spell => spell.Value)]);
        return intentOption.CastableSpells[index ?? 0];
    }

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var legal = options.LegalTargets;
        if (!legal.IsCastable)
        {
            output.WriteLine($"Creature {options.Actor}: {options.Spell.Value} has no legal target and fizzles.");
            return [];
        }

        var targets = new List<CreatureId>();
        while (targets.Count < legal.MaxTargets)
        {
            List<CreatureId> remaining = [.. legal.Candidates.Where(candidate => !targets.Contains(candidate))];
            var allowNone = targets.Count >= legal.MinTargets ? "done" : null;
            var index = Pick($"Target {targets.Count + 1} of {options.Spell.Value} by creature {options.Actor}", [.. remaining.Select(candidate => $"creature {candidate}")], allowNone);
            if (index is null)
            {
                break;
            }

            targets.Add(remaining[index.Value]);
        }

        return targets;
    }

    private void ShowBoard(PlayerBoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);

        output.WriteLine($"Round {board.RoundNumber} / {board.SubPhase}");
        output.WriteLine("  Allies:  " + string.Join(" | ", board.Allies.Select(Describe)));
        output.WriteLine("  Enemies: " + string.Join(" | ", board.Enemies.Select(Describe)));
    }

    private static string Describe(CreatureSnapshot creature) =>
        $"#{creature.Id} {creature.Name} HP {creature.Health}/{creature.MaxHealth} EN {creature.Energy}" + (creature.IsStunned ? " (stunned)" : string.Empty);

    /// <summary>
    /// Shows a numbered menu and returns the chosen index, or <c>null</c> when the optional "none" entry was chosen.
    /// </summary>
    private int? Pick(string title, List<string> entries, string? allowNone = null)
    {
        while (true)
        {
            ShowMenu(title, entries, allowNone);
            var line = input.ReadLine() ?? throw new InvalidOperationException("The input ended before the match did.");
            if (TryChoose(line, entries.Count, allowNone is not null, out var choice))
            {
                return choice;
            }

            output.WriteLine("Please enter one of the listed numbers.");
        }
    }

    private void ShowMenu(string title, List<string> entries, string? allowNone)
    {
        output.WriteLine(title + ":");
        for (var index = 0; index < entries.Count; index++)
        {
            output.WriteLine($"  {index + 1}. {entries[index]}");
        }

        if (allowNone is not null)
        {
            output.WriteLine($"  0. {allowNone}");
        }

        output.Write("> ");
    }

    /// <summary>
    /// Reads a menu answer: an entry number gives its index, 0 gives <c>null</c> when a "none" entry is offered.
    /// </summary>
    private static bool TryChoose(string line, int count, bool noneAllowed, out int? choice)
    {
        choice = null;
        if (!int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        if (number == 0)
        {
            return noneAllowed;
        }

        if (number < 1 || number > count)
        {
            return false;
        }

        choice = number - 1;
        return true;
    }
}
