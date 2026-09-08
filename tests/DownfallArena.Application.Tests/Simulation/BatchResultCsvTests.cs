using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Simulation;

public sealed class BatchResultCsvTests
{
    [Fact]
    public void A_batch_renders_a_header_and_one_line_per_match()
    {
        var matchId = MatchId.From(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        var results = new List<MatchResult>
        {
            new(0, 42, matchId, new MatchOutcome(PlayerSlot.Player2, MatchEndReason.Elimination), 7, 0, 13),
            new(1, 43, matchId, new MatchOutcome(null, MatchEndReason.RoundCap), 30, 9, 9),
        };
        var batch = new BatchResult(Scenario(), results, SimulationSummary.Of(results));
        using var writer = new StringWriter();

        BatchResultCsv.Write(batch, writer);

        writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ShouldBe(
        [
            "index,seed,match_id,winner,reason,rounds,player1_remaining_health,player2_remaining_health",
            "0,42,11111111-2222-3333-4444-555555555555,Player2,Elimination,7,0,13",
            "1,43,11111111-2222-3333-4444-555555555555,Draw,RoundCap,30,9,9",
        ]);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var batch = new BatchResult(Scenario(), [], SimulationSummary.Of([]));

        Should.Throw<ArgumentNullException>(() => BatchResultCsv.Write(null!, new StringWriter()));
        Should.Throw<ArgumentNullException>(() => BatchResultCsv.Write(batch, null!));
        Should.Throw<ArgumentNullException>(() => BatchResultCsv.Line(null!));
    }

    private static SimulationScenario Scenario() => new()
    {
        RuleSet = RuleSet.Default,
        Player1Roster = [],
        Player2Roster = [],
        Matches = 2,
    };
}
