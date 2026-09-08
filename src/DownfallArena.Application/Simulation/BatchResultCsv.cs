using System.Globalization;

namespace DownfallArena.Application.Simulation;

/// <summary>
/// Renders a batch as CSV, one line per match, for spreadsheets and future learning datasets.
/// </summary>
public static class BatchResultCsv
{
    public const string Header = "index,seed,match_id,winner,reason,rounds,player1_remaining_health,player2_remaining_health";

    public static void Write(BatchResult batch, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine(Header);
        foreach (var result in batch.Results)
        {
            writer.WriteLine(Line(result));
        }
    }

    public static string Line(MatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return string.Join(
            ',',
            result.Index.ToString(CultureInfo.InvariantCulture),
            result.Seed.ToString(CultureInfo.InvariantCulture),
            result.MatchId.ToString(),
            result.Outcome.Winner?.ToString() ?? "Draw",
            result.Outcome.Reason.ToString(),
            result.Rounds.ToString(CultureInfo.InvariantCulture),
            result.Player1RemainingHealth.ToString(CultureInfo.InvariantCulture),
            result.Player2RemainingHealth.ToString(CultureInfo.InvariantCulture));
    }
}
