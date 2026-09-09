using System.Globalization;
using DownfallArena.Application.Evaluation;

namespace DownfallArena.Cli;

/// <summary>
/// Prints an evaluation as one table: a line per agent, then the shared numbers, the spell usage, and which
/// spells the winning side was holding.
/// </summary>
internal static class EvaluationConsole
{
    /// <summary>How many sides must have declared a spell before its score is worth reading.</summary>
    private const int EnoughSides = 8;

    public static void Print(EvaluationResult evaluation, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine($"{"Agent",-24} {"Wins",5} {"Win rate",9} {"95% interval",18} {"Score",6} {"Health",7} {"Entropy",8} {"Fizzles",8} {"Crits",6}");
        foreach (var report in new[] { evaluation.AgentA, evaluation.AgentB })
        {
            writer.WriteLine(string.Join(
                ' ',
                report.Agent.PadRight(24),
                report.Wins.ToString(CultureInfo.InvariantCulture).PadLeft(5),
                Percent(report.WinRate.Mean).PadLeft(9),
                $"{Percent(report.WinRate.Low)} to {Percent(report.WinRate.High)}".PadLeft(18),
                report.Score.Mean.ToString("F3", CultureInfo.InvariantCulture).PadLeft(6),
                report.AverageRemainingHealth.ToString("F1", CultureInfo.InvariantCulture).PadLeft(7),
                report.SpellEntropy.ToString("F2", CultureInfo.InvariantCulture).PadLeft(8),
                Percent(report.FizzleRate).PadLeft(8),
                Percent(report.CriticalRate).PadLeft(6)));
        }

        writer.WriteLine($"Matches {evaluation.Matches}, draws {evaluation.Draws} ({Percent(evaluation.DrawRate)}), rounds {evaluation.AverageRounds.ToString("F1", CultureInfo.InvariantCulture)} on average, {Percent(evaluation.RoundCapShare)} ended by the round cap.");
        if (evaluation.SelfPlay)
        {
            writer.WriteLine($"Both agents are {evaluation.AgentA.Agent}: an agent is seeded from the match seed and the slot, so the mirrored pass replays the same matches. Each side above is the total of both players and the even split is arithmetic, not a result. The spell table below is still meaningful — it is about the content — and counts the replay once, so its sides are independent.");
        }

        writer.WriteLine($"Spells of {evaluation.AgentA.Agent}: {Usage(evaluation.AgentA)}");
        writer.WriteLine($"Spells of {evaluation.AgentB.Agent}: {Usage(evaluation.AgentB)}");
        PrintSpellOutcomes(evaluation, writer);
    }

    /// <summary>
    /// Which spells the winning side was holding. Sides with too small a sample are counted but not ranked,
    /// because a spell three sides declared can score 1.00 on noise.
    /// </summary>
    private static void PrintSpellOutcomes(EvaluationResult evaluation, TextWriter writer)
    {
        if (evaluation.SpellOutcomes.Count == 0)
        {
            return;
        }

        var ranked = evaluation.SpellOutcomes.Where(outcome => outcome.Sides >= EnoughSides).ToList();
        writer.WriteLine();
        if (ranked.Count == 0)
        {
            // Saying nothing here would read as the table not existing, when the run was simply too short.
            writer.WriteLine($"No spell was declared by {EnoughSides} sides or more, so none of the {evaluation.SpellOutcomes.Count} spell(s) seen is worth ranking. Play more seeds.");
            return;
        }

        writer.WriteLine($"Spells by the outcome of the sides that declared them ({EnoughSides} sides or more; one half is no signal):");
        writer.WriteLine($"{"Spell",-32} {"Share",7} {"Sides",6} {"Won",5} {"Lost",5} {"Drawn",6} {"Casts/side",11}");
        foreach (var outcome in ranked)
        {
            writer.WriteLine(string.Join(
                ' ',
                outcome.Spell.PadRight(32),
                Percent(outcome.Score).PadLeft(7),
                outcome.Sides.ToString(CultureInfo.InvariantCulture).PadLeft(6),
                outcome.Wins.ToString(CultureInfo.InvariantCulture).PadLeft(5),
                outcome.Losses.ToString(CultureInfo.InvariantCulture).PadLeft(5),
                outcome.Draws.ToString(CultureInfo.InvariantCulture).PadLeft(6),
                outcome.IntentsPerSide.ToString("F1", CultureInfo.InvariantCulture).PadLeft(11)));
        }

        var quiet = evaluation.SpellOutcomes.Count - ranked.Count;
        if (quiet > 0)
        {
            writer.WriteLine($"{quiet} spell(s) declared by fewer than {EnoughSides} sides are not listed: too little to read.");
        }
    }

    private static string Percent(double value) => value.ToString("P1", CultureInfo.InvariantCulture);

    private static string Usage(AgentReport report) =>
        report.SpellUsage.Count == 0
            ? "none"
            : string.Join(", ", report.SpellUsage.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key} {pair.Value}"));
}
