using DownfallArena.Domain.Matches.Rounds;

namespace DownfallArena.Domain.Tests.Matches.Rounds;

public sealed class RoundFlowTests
{
    [Fact]
    public void The_flow_lists_every_sub_phase_once_in_play_order()
    {
        RoundFlow.Steps.ShouldBe(Enum.GetValues<RoundSubPhase>());
        RoundFlow.First.ShouldBe(RoundSubPhase.EnergyGain);
        RoundFlow.Last.ShouldBe(RoundSubPhase.Finalization);
    }

    [Theory]
    [InlineData(RoundSubPhase.EnergyGain, RoundPhase.StartOfRound)]
    [InlineData(RoundSubPhase.OngoingEffects, RoundPhase.StartOfRound)]
    [InlineData(RoundSubPhase.Evolution, RoundPhase.Planning)]
    [InlineData(RoundSubPhase.TurnOrderResolution, RoundPhase.Planning)]
    [InlineData(RoundSubPhase.IntentSelection, RoundPhase.Combat)]
    [InlineData(RoundSubPhase.ActionResolution, RoundPhase.Combat)]
    [InlineData(RoundSubPhase.Cleanup, RoundPhase.EndOfRound)]
    [InlineData(RoundSubPhase.Finalization, RoundPhase.EndOfRound)]
    public void Every_sub_phase_belongs_to_its_phase(RoundSubPhase subPhase, RoundPhase phase)
    {
        RoundFlow.PhaseOf(subPhase).ShouldBe(phase);
    }

    [Fact]
    public void After_walks_forward_and_stops_at_the_end()
    {
        RoundFlow.After(RoundSubPhase.EnergyGain).ShouldBe(RoundSubPhase.OngoingEffects);
        RoundFlow.After(RoundSubPhase.TurnOrderResolution).ShouldBe(RoundSubPhase.IntentSelection);
        RoundFlow.After(RoundSubPhase.Finalization).ShouldBeNull();
    }
}
