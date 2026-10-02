using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

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

    [Fact]
    public async Task The_timeline_a_tie_order_produced_is_narrated()
    {
        var timeline = CombatTimeline.Of([new ActivationSlot(PlayerSlot.Player1, Two, Speed.Standard, Initiative.Of(5)), new ActivationSlot(PlayerSlot.Player1, One, Speed.Standard, Initiative.Of(5))]);
        var writer = new StringWriter();

        await new ConsoleMatchLog(writer).HandleAsync(new TiesOrdered(MatchId.New(), RoundId.First, timeline), TestContext.Current.CancellationToken);

        writer.ToString().ShouldContain("Ties ordered: 2 (Standard), 1 (Standard)");
    }

    /// <summary>
    /// In `human` mode a seat reads this log, so a face-down pick and a pass both print nothing: a line for one
    /// and not the other would tell them apart (ADR 0089). The reveal is the first the log says of either.
    /// </summary>
    [Fact]
    public async Task A_face_down_pick_and_a_pass_print_nothing_until_the_reveal_names_every_purchase()
    {
        var choice = new EvolutionChoice(Two, TierId.Parse("tier:guard:v1"));
        var writer = new StringWriter();
        var log = new ConsoleMatchLog(writer);

        await log.HandleAsync(new EvolutionChoiceSubmitted(MatchId.New(), RoundId.First, PlayerSlot.Player2, choice), TestContext.Current.CancellationToken);
        await log.HandleAsync(new EvolutionPassed(MatchId.New(), RoundId.First, PlayerSlot.Player2), TestContext.Current.CancellationToken);

        writer.ToString().ShouldBeEmpty();

        await log.HandleAsync(new PurchasesRevealed(MatchId.New(), RoundId.First, [choice]), TestContext.Current.CancellationToken);

        writer.ToString().ShouldContain("Purchases revealed: creature 2 buys tier:guard:v1.");
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
