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

    private static OngoingEffectsApplied Ongoing(IReadOnlyList<BleedTick> bleeds, IReadOnlyList<RegenerationTick> regenerations) =>
        new(MatchId.New(), RoundId.First, bleeds, regenerations);

    private static async Task<string> LogAsync(OngoingEffectsApplied applied)
    {
        var writer = new StringWriter();
        await new ConsoleMatchLog(writer).HandleAsync(applied, TestContext.Current.CancellationToken);
        return writer.ToString();
    }
}
