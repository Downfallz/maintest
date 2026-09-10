using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests;

/// <summary>
/// The narration a spectator reads. A round where health moved and the log said nothing is the failure worth
/// guarding: the start of a round can heal, damage, both, or neither (ADR 0019).
/// </summary>
public sealed class ConsoleMatchLogTests
{
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Two = CreatureId.From(2);

    [Fact]
    public async Task A_round_that_only_healed_is_narrated_rather_than_passed_over()
    {
        var printed = await LogAsync(Ongoing(bleeds: [], regenerations: [new RegenerationTick(One, 4)]));

        printed.ShouldContain("Regenerations:");
        printed.ShouldContain("creature 1 heals 4");
    }

    [Fact]
    public async Task A_round_that_healed_and_bled_narrates_both_in_the_order_they_happened()
    {
        var printed = await LogAsync(Ongoing(bleeds: [new BleedTick(Two, 3)], regenerations: [new RegenerationTick(One, 4)]));

        printed.ShouldContain("creature 1 heals 4");
        printed.ShouldContain("creature 2 takes 3");
        printed.IndexOf("Regenerations:", StringComparison.Ordinal)
            .ShouldBeLessThan(printed.IndexOf("Bleeds:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_round_where_nothing_ticked_says_nothing()
    {
        (await LogAsync(Ongoing(bleeds: [], regenerations: []))).ShouldBeEmpty();
    }

    /// <summary>
    /// An energy regeneration moves no health, so a round where one ticks alone would print nothing at all unless the
    /// log knows about it -- and the energy it gave is the whole reason the next round looks different.
    /// </summary>
    [Fact]
    public async Task A_round_where_only_an_energyRegeneration_ticked_still_narrates_the_energy_it_gave()
    {
        var printed = await LogAsync(Ongoing(bleeds: [], regenerations: [], energyRegenerations: [new EnergyRegenerationTick(One, 2)]));

        printed.ShouldContain("creature 1 gains 2 energy");
    }

    private static OngoingEffectsApplied Ongoing(
        IReadOnlyList<BleedTick> bleeds,
        IReadOnlyList<RegenerationTick> regenerations,
        IReadOnlyList<EnergyRegenerationTick>? energyRegenerations = null) =>
        new(MatchId.New(), RoundId.First, energyRegenerations ?? [], regenerations, bleeds);

    private static async Task<string> LogAsync(OngoingEffectsApplied applied)
    {
        var writer = new StringWriter();
        await new ConsoleMatchLog(writer).HandleAsync(applied, TestContext.Current.CancellationToken);
        return writer.ToString();
    }
}
