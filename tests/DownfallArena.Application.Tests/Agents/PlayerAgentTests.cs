using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Agents;

/// <summary>The behaviour <see cref="IPlayerAgent" /> gives every agent that does not choose an action its own way.</summary>
public sealed class PlayerAgentTests
{
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Three = CreatureId.From(3);
    private static readonly CreatureId Four = CreatureId.From(4);

    private static readonly TargetOptions SlamOnEither = new(One, TestContent.Slam, new LegalTargets(1, 2, [Three, Four]));
    private static readonly TargetOptions StrikeOnEither = new(One, TestContent.Strike, new LegalTargets(1, 1, [Three, Four]));
    private static readonly ActionOptions SlamOrStrike = new(One, [SlamOnEither, StrikeOnEither]);

    /// <summary>
    /// ADR 0110. By default an action is the intent the agent would declare among the spells on offer, then the
    /// targets it would bind for that spell: it is asked the spells the options list, and the targets of the one
    /// it chose.
    /// </summary>
    [Fact]
    public void By_default_an_action_is_the_spell_the_agent_would_declare_then_the_targets_it_would_bind_for_it()
    {
        var agent = new Declaring(TestContent.Strike, [Four]);

        var chosen = ((IPlayerAgent)agent).DecideAction(Board(), SlamOrStrike);

        chosen.ShouldBe(new ChosenAction(TestContent.Strike, [Four]));
        agent.AskedIntent.ShouldBe(new IntentOption(One, [TestContent.Slam, TestContent.Strike]));
        agent.AskedTargets.ShouldBe(StrikeOnEither);
    }

    /// <summary>An agent that declares a spell the options do not list has a bug, and is not handed targets for it.</summary>
    [Fact]
    public void By_default_a_spell_the_options_do_not_offer_is_a_bug()
    {
        var agent = new Declaring(TestContent.Guard, [One]);

        Should.Throw<InvalidOperationException>(() => ((IPlayerAgent)agent).DecideAction(Board(), SlamOrStrike));

        agent.AskedTargets.ShouldBeNull();
    }

    [Fact]
    public void By_default_the_options_are_required() =>
        Should.Throw<ArgumentNullException>(() => ((IPlayerAgent)new Declaring(TestContent.Strike, [Four])).DecideAction(Board(), null!));

    private static PlayerBoardState Board() => PlayerBoardStateProjection.Build(new MatchStore().Started(), PlayerSlot.Player1);

    /// <summary>
    /// An agent that declares one spell and binds fixed targets, and remembers what it was asked. It does not
    /// implement <c>DecideAction</c>, so the interface's default is what answers.
    /// </summary>
    private sealed class Declaring(SpellId spell, IReadOnlyList<CreatureId> targets) : IPlayerAgent
    {
        public IntentOption? AskedIntent { get; private set; }

        public TargetOptions? AskedTargets { get; private set; }

        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => EvolutionDecision.Pass;

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Speed.Standard;

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => options.AsRolled;

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
        {
            AskedIntent = intentOption;
            return spell;
        }

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
        {
            AskedTargets = options;
            return targets;
        }
    }
}
