using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Agents;

public sealed class RandomAgentTests
{
    private static readonly PlayerBoardState Board = PlayerBoardStateProjection.Build(new MatchStore().Started(), PlayerSlot.Player1);

    [Fact]
    public void Evolution_picks_one_offered_creature_and_one_of_its_spells_or_passes_when_nothing_is_offered()
    {
        var agent = new RandomAgent(new TestRandom(3));
        var options = new EvolutionOptions(2, [new EvolutionOption(CreatureId.From(1), [TestContent.Guard]), new EvolutionOption(CreatureId.From(2), [TestContent.Guard, TestContent.Slam])]);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var choice = agent.DecideEvolution(Board, options).Choice.ShouldNotBeNull();
            options.Creatures.Single(option => option.Creature == choice.Creature).UnlockableSpells.ShouldContain(choice.Spell);
        }

        agent.DecideEvolution(Board, new EvolutionOptions(1, [])).IsPass.ShouldBeTrue();
    }

    [Fact]
    public void Speed_uses_both_values()
    {
        var agent = new RandomAgent(new TestRandom(5));

        var speeds = Enumerable.Range(0, 20).Select(_ => agent.DecideSpeed(Board, CreatureId.From(1))).ToHashSet();

        speeds.ShouldBe([Speed.Quick, Speed.Standard], ignoreOrder: true);
    }

    [Fact]
    public void Intent_picks_one_castable_spell()
    {
        var agent = new RandomAgent(new TestRandom(9));
        var option = new IntentOption(CreatureId.From(1), [TestContent.Guard, TestContent.Strike]);

        var picked = Enumerable.Range(0, 20).Select(_ => agent.DecideIntent(Board, option)).ToHashSet();

        picked.ShouldBe([TestContent.Guard, TestContent.Strike], ignoreOrder: true);
    }

    [Fact]
    public void Targets_are_distinct_legal_candidates_within_the_bounds()
    {
        var agent = new RandomAgent(new TestRandom(11));
        var options = new TargetOptions(CreatureId.From(1), TestContent.Slam, new LegalTargets(1, 2, [CreatureId.From(3), CreatureId.From(4), CreatureId.From(5)]));
        var counts = new HashSet<int>();

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var targets = agent.DecideTargets(Board, options);

            targets.Count.ShouldBeInRange(1, 2);
            targets.Distinct().Count().ShouldBe(targets.Count);
            targets.ShouldAllBe(target => options.LegalTargets.Candidates.Contains(target));
            counts.Add(targets.Count);
        }

        counts.ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public void Null_options_are_rejected()
    {
        var agent = new RandomAgent(new TestRandom(1));

        Should.Throw<ArgumentNullException>(() => agent.DecideEvolution(Board, null!));
        Should.Throw<ArgumentNullException>(() => agent.DecideIntent(Board, null!));
        Should.Throw<ArgumentNullException>(() => agent.DecideTargets(Board, null!));
        Should.Throw<InvalidOperationException>(() => agent.DecideIntent(Board, new IntentOption(CreatureId.From(1), [])));
        Should.Throw<ArgumentNullException>(() => EvolutionDecision.Unlock(null!));
    }
}
