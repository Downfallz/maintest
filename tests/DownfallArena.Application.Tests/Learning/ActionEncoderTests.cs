using DownfallArena.Application.Learning;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Learning;

public sealed class ActionEncoderTests
{
    private static readonly FeatureSchema Schema = FeatureSchema.Build(TestContent.Resources, MatchStore.TwoOnTwo());
    private static readonly ActionEncoder Encoder = new(Schema);
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Two = CreatureId.From(2);
    private static readonly CreatureId Three = CreatureId.From(3);
    private static readonly CreatureId Four = CreatureId.From(4);

    private static readonly BoardSlots Slots = BoardSlots.Of(
        Boards.Board(
            PlayerSlot.Player1,
            [Boards.Creature(1, PlayerSlot.Player1), Boards.Creature(2, PlayerSlot.Player1)],
            [Boards.Creature(3, PlayerSlot.Player2), Boards.Creature(4, PlayerSlot.Player2)]),
        teamSize: 2);

    [Fact]
    public void Every_kind_of_decision_has_a_key_naming_the_acting_slot_and_a_code()
    {
        ActionEncoder.Pass().ShouldBe(new EncodedAction("pass", ActionCode.Pass));
        ActionCode.Pass.ShouldBe(new ActionCode(ActionKind.Pass, -1, -1, -1, 0));
        Encoder.Evolve(Slots, new EvolutionChoice(Two, TestContent.Guard))
            .ShouldBe(new EncodedAction("evolve:1:spell:guard:v1", new ActionCode(ActionKind.Evolve, 1, 0, -1, 0)));
        ActionEncoder.Speed(Slots, new SpeedChoice(One, Speed.Quick))
            .ShouldBe(new EncodedAction("speed:0:Quick", new ActionCode(ActionKind.Speed, 0, -1, 0, 0)));
        ActionEncoder.Speed(Slots, new SpeedChoice(Two, Speed.Standard))
            .ShouldBe(new EncodedAction("speed:1:Standard", new ActionCode(ActionKind.Speed, 1, -1, 1, 0)));
        Encoder.Intent(Slots, new CombatIntent(One, TestContent.Strike))
            .ShouldBe(new EncodedAction("intent:0:spell:strike:v1", new ActionCode(ActionKind.Intent, 0, 3, -1, 0)));
        Encoder.Targets(Slots, Two, TestContent.Slam, [Four, Three])
            .ShouldBe(new EncodedAction("targets:1:spell:slam:v1:2,3", new ActionCode(ActionKind.Targets, 1, 2, -1, 0b1100)));
        Encoder.Targets(Slots, One, TestContent.Strike, [])
            .ShouldBe(new EncodedAction("targets:0:spell:strike:v1:", new ActionCode(ActionKind.Targets, 0, 3, -1, 0)));
        Encoder.Schema.ShouldBeSameAs(Schema);
    }

    [Fact]
    public void A_spell_outside_the_content_has_no_index_but_keeps_its_key()
    {
        var unknown = SpellId.Parse("spell:unknown:v1");

        Encoder.Intent(Slots, new CombatIntent(One, unknown))
            .ShouldBe(new EncodedAction("intent:0:spell:unknown:v1", new ActionCode(ActionKind.Intent, 0, -1, -1, 0)));
        Encoder.Targets(Slots, One, unknown, [Three])
            .ShouldBe(new EncodedAction("targets:0:spell:unknown:v1:2", new ActionCode(ActionKind.Targets, 0, -1, -1, 0b0100)));
    }

    [Fact]
    public void Evolution_candidates_list_every_unlock_then_the_pass()
    {
        var options = PlayerOptionsProjection.Build(new MatchStore().Started(), PlayerSlot.Player1, TestContent.Resources);
        options.Kind.ShouldBe(PlayerOptionsKind.Evolution);

        var candidates = Encoder.Candidates(Slots, options);

        candidates.Select(candidate => candidate.Key).ShouldBe(["evolve:0:spell:guard:v1", "evolve:1:spell:guard:v1", "pass"]);
        candidates[0].Code.ShouldBe(new ActionCode(ActionKind.Evolve, 0, 0, -1, 0));
        candidates[2].Code.ShouldBe(ActionCode.Pass);
    }

    [Fact]
    public void Speed_candidates_offer_both_speeds_per_creature()
    {
        var match = new MatchStore().Started();
        MatchStore.PassEvolution(match);
        var options = PlayerOptionsProjection.Build(match, PlayerSlot.Player1, TestContent.Resources);
        options.Kind.ShouldBe(PlayerOptionsKind.Speed);

        var candidates = Encoder.Candidates(Slots, options);

        candidates.Select(candidate => candidate.Key).Order(StringComparer.Ordinal)
            .ShouldBe(["speed:0:Quick", "speed:0:Standard", "speed:1:Quick", "speed:1:Standard"]);
        candidates.ShouldAllBe(candidate => candidate.Code.Kind == ActionKind.Speed);
    }

    [Fact]
    public void Intent_candidates_list_every_castable_spell_per_creature()
    {
        var match = new MatchStore().Started();
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);
        var options = PlayerOptionsProjection.Build(match, PlayerSlot.Player1, TestContent.Resources);
        options.Kind.ShouldBe(PlayerOptionsKind.Intent);

        var candidates = Encoder.Candidates(Slots, options);

        candidates.Select(candidate => candidate.Key).Order(StringComparer.Ordinal)
            .ShouldBe(["intent:0:spell:strike:v1", "intent:1:spell:strike:v1"]);
        candidates.ShouldAllBe(candidate => candidate.Code.SpellIndex == Schema.SpellIndex(TestContent.Strike));
    }

    [Fact]
    public void Target_candidates_enumerate_every_legal_combination_in_candidate_order()
    {
        var options = new PlayerOptions
        {
            Kind = PlayerOptionsKind.Target,
            Target = new TargetOptions(One, TestContent.Slam, new LegalTargets(1, 2, [Three, Four])),
        };

        var candidates = Encoder.Candidates(Slots, options);

        candidates.Select(candidate => candidate.Key).ShouldBe(["targets:0:spell:slam:v1:2", "targets:0:spell:slam:v1:3", "targets:0:spell:slam:v1:2,3"]);
        candidates.Select(candidate => candidate.Code.TargetMask).ShouldBe([0b0100, 0b1000, 0b1100]);
        candidates.ShouldAllBe(candidate => candidate.Code.SpellIndex == Schema.SpellIndex(TestContent.Slam));
    }

    [Fact]
    public void The_same_targets_for_another_spell_are_another_action()
    {
        var slam = Encoder.Targets(Slots, One, TestContent.Slam, [Three]);
        var strike = Encoder.Targets(Slots, One, TestContent.Strike, [Three]);

        slam.Key.ShouldNotBe(strike.Key);
        slam.Code.ShouldNotBe(strike.Code);
        slam.Code.TargetMask.ShouldBe(strike.Code.TargetMask);
    }

    [Fact]
    public void Three_candidates_give_every_pair_and_triple()
    {
        var slots = BoardSlots.Of(
            Boards.Board(
                PlayerSlot.Player1,
                [Boards.Creature(1, PlayerSlot.Player1)],
                [Boards.Creature(3, PlayerSlot.Player2), Boards.Creature(4, PlayerSlot.Player2), Boards.Creature(5, PlayerSlot.Player2)]),
            teamSize: 3);
        var options = new PlayerOptions
        {
            Kind = PlayerOptionsKind.Target,
            Target = new TargetOptions(One, TestContent.Slam, new LegalTargets(2, 3, [Three, Four, CreatureId.From(5)])),
        };

        var keys = Encoder.Candidates(slots, options).Select(candidate => candidate.Key);

        keys.ShouldBe(["targets:0:spell:slam:v1:3,4", "targets:0:spell:slam:v1:3,5", "targets:0:spell:slam:v1:4,5", "targets:0:spell:slam:v1:3,4,5"]);
    }

    [Fact]
    public void An_uncastable_spell_has_the_single_empty_target_action()
    {
        var options = new PlayerOptions
        {
            Kind = PlayerOptionsKind.Target,
            Target = new TargetOptions(Two, TestContent.Strike, new LegalTargets(1, 1, [])),
        };

        Encoder.Candidates(Slots, options).ShouldBe([new EncodedAction("targets:1:spell:strike:v1:", new ActionCode(ActionKind.Targets, 1, 3, -1, 0))]);
    }

    [Theory]
    [InlineData(PlayerOptionsKind.Waiting)]
    [InlineData(PlayerOptionsKind.Resolution)]
    [InlineData(PlayerOptionsKind.Ended)]
    [InlineData(PlayerOptionsKind.Evolution)]
    public void Kinds_without_a_decision_have_no_candidates(PlayerOptionsKind kind)
    {
        Encoder.Candidates(Slots, new PlayerOptions { Kind = kind }).ShouldBeEmpty();
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => Encoder.Evolve(null!, new EvolutionChoice(One, TestContent.Guard)));
        Should.Throw<ArgumentNullException>(() => Encoder.Evolve(Slots, null!));
        Should.Throw<ArgumentNullException>(() => ActionEncoder.Speed(null!, new SpeedChoice(One, Speed.Quick)));
        Should.Throw<ArgumentNullException>(() => ActionEncoder.Speed(Slots, null!));
        Should.Throw<ArgumentNullException>(() => Encoder.Intent(null!, new CombatIntent(One, TestContent.Strike)));
        Should.Throw<ArgumentNullException>(() => Encoder.Intent(Slots, null!));
        Should.Throw<ArgumentNullException>(() => Encoder.Targets(null!, One, TestContent.Strike, []));
        Should.Throw<ArgumentNullException>(() => Encoder.Targets(Slots, One, null!, []));
        Should.Throw<ArgumentNullException>(() => Encoder.Targets(Slots, One, TestContent.Strike, null!));
        Should.Throw<ArgumentNullException>(() => Encoder.Candidates(null!, new PlayerOptions { Kind = PlayerOptionsKind.Waiting }));
        Should.Throw<ArgumentNullException>(() => Encoder.Candidates(Slots, null!));
        Should.Throw<ArgumentOutOfRangeException>(() => Encoder.Targets(Slots, CreatureId.From(9), TestContent.Strike, []));
    }
}
