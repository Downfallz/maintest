using DownfallArena.Application.Learning;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Learning;

public sealed class ObservationBuilderTests
{
    private static readonly FeatureSchema Schema = FeatureSchema.Build(TestContent.Resources, MatchStore.TwoOnTwo());
    private static readonly ObservationBuilder Builder = new(Schema, TestContent.Resources);

    [Fact]
    public void The_same_board_gives_the_same_observation()
    {
        var board = PlayerBoardStateProjection.Build(new MatchStore().Started(), PlayerSlot.Player1);

        var observation = Builder.Build(board);

        observation.ShouldBe(Builder.Build(board));
        observation.SchemaId.ShouldBe(Schema.Id);
        observation.SchemaId.ShouldStartWith("features:v3+");
        observation.Features.Count.ShouldBe(Schema.Length);
        Builder.Schema.ShouldBeSameAs(Schema);
    }

    [Fact]
    public void The_globals_describe_the_round_position()
    {
        var board = PlayerBoardStateProjection.Build(new MatchStore().Started(), PlayerSlot.Player1);

        var features = Builder.Build(board).Features;

        features[Schema.IndexOf("round_fraction")].ShouldBe(1f / 30f);
        features[Schema.IndexOf("phase")].ShouldBe((float)RoundPhase.Planning / 3f);
        features[Schema.IndexOf("sub_phase")].ShouldBe((float)RoundSubPhase.Evolution / 9f);
        features[Schema.IndexOf("reveal_progress")].ShouldBe(0f);
        features[Schema.IndexOf("revealed_enemy_actions")].ShouldBe(0f);
    }

    [Fact]
    public void Revealed_actions_count_the_enemy_ones_and_the_reveal_progress()
    {
        var match = new MatchStore().Started();
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);
        MatchStore.DeclareStrikes(match);
        var board = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);
        var timeline = board.Timeline;
        var revealed = timeline.Take(2).Select(slot => CombatAction.Bind(new CombatIntent(slot.Creature, TestContent.Strike), [])).ToList();
        var enemyRevealed = revealed.Count(action => board.Enemies.Any(enemy => enemy.Id == action.Actor));

        var features = Builder.Build(board with { RevealedActions = revealed, RevealCursor = 2 }).Features;

        features[Schema.IndexOf("reveal_progress")].ShouldBe(2f / timeline.Count);
        features[Schema.IndexOf("revealed_enemy_actions")].ShouldBe(enemyRevealed / 2f);
        features[Schema.IndexOf("sub_phase")].ShouldBe((float)RoundSubPhase.RevealAndTarget / 9f);
    }

    [Fact]
    public void A_board_before_the_first_round_has_zero_globals()
    {
        var board = PlayerBoardStateProjection.Build(new MatchStore().Empty(), PlayerSlot.Player1);

        var features = Builder.Build(board).Features;

        features.ShouldBe(new float[Schema.Length]);
    }

    [Fact]
    public void Each_player_sees_the_other_team_in_the_enemy_block()
    {
        var alice = Boards.Creature(1, PlayerSlot.Player1) with { Health = Health.Of(5) };
        var arthur = Boards.Creature(2, PlayerSlot.Player1) with { Energy = Energy.Of(3) };
        var bob = Boards.Creature(3, PlayerSlot.Player2) with { IsStunned = true };
        var bella = Boards.Creature(4, PlayerSlot.Player2) with { Health = Health.Of(0) };

        var player1 = Builder.Build(Boards.Board(PlayerSlot.Player1, [alice, arthur], [bob, bella])).Features;
        var player2 = Builder.Build(Boards.Board(PlayerSlot.Player2, [bob, bella], [alice, arthur])).Features;

        Block(player1, 0).ShouldBe(Block(player2, 2));
        Block(player1, 1).ShouldBe(Block(player2, 3));
        Block(player1, 2).ShouldBe(Block(player2, 0));
        Block(player1, 3).ShouldBe(Block(player2, 1));
        Block(player1, 0).ShouldNotBe(Block(player1, 1));
        player1.Take(FeatureSchema.GlobalFeatures.Count).ShouldBe(player2.Take(FeatureSchema.GlobalFeatures.Count));
    }

    [Fact]
    public void A_creature_block_holds_its_stats_spells_and_nodes()
    {
        var creature = Boards.Creature(1, PlayerSlot.Player1) with
        {
            Health = Health.Of(10),
            Energy = Energy.Of(2),
            TotalDefense = Defense.Of(3),
            BaseInitiative = Initiative.Of(6),
            CurrentInitiative = Initiative.Of(4),
            IsStunned = true,
            KnownSpells = new HashSet<SpellId> { TestContent.Strike, TestContent.Guard },
        };

        var features = Builder.Build(Boards.Board(PlayerSlot.Player1, [creature], [])).Features;

        features[Schema.IndexOf("own0_alive")].ShouldBe(1f);
        features[Schema.IndexOf("own0_health_fraction")].ShouldBe(0.5f);
        features[Schema.IndexOf("own0_energy")].ShouldBe(2f);
        features[Schema.IndexOf("own0_stunned")].ShouldBe(1f);
        features[Schema.IndexOf("own0_defense")].ShouldBe(3f);
        features[Schema.IndexOf("own0_initiative")].ShouldBe(4f);
        features[Schema.IndexOf("own0_knows_spell:strike:v1")].ShouldBe(1f);
        features[Schema.IndexOf("own0_knows_spell:guard:v1")].ShouldBe(1f);
        features[Schema.IndexOf("own0_knows_spell:slam:v1")].ShouldBe(0f);
        features[Schema.IndexOf("own0_knows_spell:rend:v1")].ShouldBe(0f);
        features[Schema.IndexOf("own0_node_talent-tree:base:v1/root")].ShouldBe(1f);
        features[Schema.IndexOf("own0_node_talent-tree:base:v1/brawler")].ShouldBe(0f);
    }

    [Fact]
    public void A_dead_creature_and_an_empty_slot_read_as_zero()
    {
        var dead = Boards.Creature(3, PlayerSlot.Player2) with { Health = Health.Of(0) };

        var features = Builder.Build(Boards.Board(PlayerSlot.Player1, [Boards.Creature(1, PlayerSlot.Player1)], [dead])).Features;

        features[Schema.IndexOf("enemy0_alive")].ShouldBe(0f);
        features[Schema.IndexOf("enemy0_health_fraction")].ShouldBe(0f);
        features[Schema.IndexOf("enemy0_initiative")].ShouldBe(5f);
        Block(features, 1).ShouldBe(new float[Schema.CreatureLength]);
        Block(features, 3).ShouldBe(new float[Schema.CreatureLength]);
    }

    [Fact]
    public void Conditions_sum_their_amounts_and_keep_the_longest_remaining_duration()
    {
        var creature = Boards.Creature(4, PlayerSlot.Player2) with
        {
            Conditions =
            [
                new ConditionSnapshot(Bleed.Of(19, rounds: 1), 1),
                new ConditionSnapshot(DefenseBuff.Of(2, Duration.Permanent), null),
                new ConditionSnapshot(DefenseBuff.Of(3, Duration.OfRounds(2)), 2),
                new ConditionSnapshot(Stun.For(1), 1),
                new ConditionSnapshot(InitiativeDebuff.Of(1, Duration.OfRounds(3)), 3),
                new ConditionSnapshot(InitiativeDebuff.Of(2, Duration.OfRounds(1)), 1),
            ],
        };

        var features = Builder.Build(Boards.Board(PlayerSlot.Player1, [], [Boards.Creature(3, PlayerSlot.Player2), creature])).Features;

        features[Schema.IndexOf("enemy1_Bleed_amount")].ShouldBe(19f);
        features[Schema.IndexOf("enemy1_Bleed_remaining")].ShouldBe(1f);
        features[Schema.IndexOf("enemy1_DefenseBuff_amount")].ShouldBe(5f);
        features[Schema.IndexOf("enemy1_DefenseBuff_remaining")].ShouldBe(ObservationBuilder.PermanentCondition);
        features[Schema.IndexOf("enemy1_Stun_amount")].ShouldBe(1f);
        features[Schema.IndexOf("enemy1_Stun_remaining")].ShouldBe(1f);
        features[Schema.IndexOf("enemy1_InitiativeDebuff_amount")].ShouldBe(3f);
        features[Schema.IndexOf("enemy1_InitiativeDebuff_remaining")].ShouldBe(3f);
        features[Schema.IndexOf("enemy0_Bleed_amount")].ShouldBe(0f);
    }

    [Fact]
    public void Conditions_from_a_played_round_appear_on_the_board()
    {
        var match = new MatchStore().Started();
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);
        MatchStore.DeclareStrikes(match);
        var board = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);
        var conditioned = board.Enemies[0] with { Conditions = [new ConditionSnapshot(Stun.For(1), 1)] };

        var features = Builder.Build(board with { Enemies = [conditioned, board.Enemies[1]] }).Features;

        features[Schema.IndexOf("enemy0_Stun_amount")].ShouldBe(1f);
        features[Schema.IndexOf("enemy1_Stun_amount")].ShouldBe(0f);
    }

    [Fact]
    public void A_condition_kind_outside_the_schema_is_refused()
    {
        var creature = Boards.Creature(1, PlayerSlot.Player1) with { Conditions = [new ConditionSnapshot(new Unpublished(), null)] };

        var exception = Should.Throw<InvalidOperationException>(() => Builder.Build(Boards.Board(PlayerSlot.Player1, [creature], [])));

        exception.Message.ShouldContain("Unpublished");
        exception.Message.ShouldContain("features:v3");
    }

    [Fact]
    public void Invalid_boards_are_rejected()
    {
        var three = Enumerable.Range(1, 3).Select(id => Boards.Creature(id, PlayerSlot.Player1)).ToList();

        Should.Throw<ArgumentNullException>(() => Builder.Build(null!));
        Should.Throw<InvalidOperationException>(() => Builder.Build(Boards.Board(PlayerSlot.Player1, three, [])));
    }

    private static IReadOnlyList<float> Block(IReadOnlyList<float> features, int boardSlot) =>
        [.. features.Skip(Schema.CreatureOffset(boardSlot)).Take(Schema.CreatureLength)];

    private sealed record Unpublished() : LastingEffect(Duration.Permanent, StackingPolicy.Stack);
}
