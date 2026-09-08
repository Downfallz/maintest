using System.Globalization;
using DownfallArena.Application.Evaluation;

namespace DownfallArena.Cli;

/// <summary>
/// Prints an evaluation as one table: a line per agent, then the shared numbers and the spell usage.
/// </summary>
internal static class EvaluationConsole
{
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
        writer.WriteLine($"Spells of {evaluation.AgentA.Agent}: {Usage(evaluation.AgentA)}");
        writer.WriteLine($"Spells of {evaluation.AgentB.Agent}: {Usage(evaluation.AgentB)}");
    }

    private static string Percent(double value) => value.ToString("P1", CultureInfo.InvariantCulture);

    private static string Usage(AgentReport report) =>
        report.SpellUsage.Count == 0
            ? "none"
            : string.Join(", ", report.SpellUsage.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key} {pair.Value}"));
}
