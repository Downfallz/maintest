using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Matches;

/// <summary>
/// A complete game between two players. The aggregate root of the Matches context: it owns the teams and the
/// current round, exposes one method per player action, and drives the round through its sub-phases. Every
/// player action is validated by the rules before anything changes; the driver then runs every automatic
/// step and progression gate until the round waits on a player again (ADR 0010).
/// </summary>
public sealed class Match : AggregateRoot<MatchId>
{
    // Collaborators, not state: a match reconstituted from its state gets them supplied again. The content hash
    // pins which resources they must be (ADR 0009).
    private readonly IGameResources _resources;
    private readonly IRandomSource _random;

    private readonly Dictionary<PlayerSlot, PlayerId> _players = [];
    private readonly Dictionary<PlayerSlot, Team> _teams = [];
    private readonly List<Creature> _creatures = [];

    private Match(MatchId id, IGameResources resources, RuleSet ruleSet, IRandomSource random)
        : base(id)
    {
        _resources = resources;
        _random = random;
        RuleSet = ruleSet;
        ContentHash = resources.Version;
    }

    public MatchState State { get; private set; } = MatchState.WaitingForPlayers;

    public RuleSet RuleSet { get; }

    /// <summary>The content hash of the game resources the match plays with (ADR 0009).</summary>
    public string ContentHash { get; }

    public Round? CurrentRound { get; private set; }

    /// <summary>How the match ended; <c>null</c> while it has not.</summary>
    public MatchOutcome? Outcome { get; private set; }

    public IReadOnlyDictionary<PlayerSlot, PlayerId> Players => _players;

    /// <summary>Every creature of the match, Player1's team first.</summary>
    public IReadOnlyList<Creature> Creatures => _creatures;

    public static Match Create(MatchId id, IGameResources resources, RuleSet ruleSet, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(random);
        return new Match(id, resources, ruleSet, random);
    }

    public PlayerSlot? SlotOf(PlayerId player) =>
        _players.Where(entry => entry.Value == player).Select(entry => (PlayerSlot?)entry.Key).FirstOrDefault();

    /// <summary>The team of a slot, or <c>null</c> while no player has joined as that slot.</summary>
    public Team? TeamOf(PlayerSlot slot) => _teams.GetValueOrDefault(slot);

    public IReadOnlyList<CreatureSnapshot> Snapshots() => [.. Creatures.Select(creature => creature.Snapshot())];

    /// <summary>
    /// Seats a player with their roster of creature definitions. The match starts when the second player joins.
    /// </summary>
    public Result<PlayerSlot> Join(PlayerId player, IReadOnlyList<CreatureDefinitionId> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        if (State != MatchState.WaitingForPlayers)
        {
            return Result.Failure<PlayerSlot>(MatchErrors.AlreadyStarted);
        }

        if (_players.ContainsValue(player))
        {
            return Result.Failure<PlayerSlot>(MatchErrors.PlayerAlreadyJoined);
        }

        if (roster.Count != RuleSet.TeamSize)
        {
            return Result.Failure<PlayerSlot>(MatchErrors.WrongTeamSize);
        }

        if (roster.Any(definition => !_resources.TryGetCreature(definition, out _)))
        {
            return Result.Failure<PlayerSlot>(MatchErrors.UnknownCreatureDefinition);
        }

        var slot = _players.ContainsKey(PlayerSlot.Player1) ? PlayerSlot.Player2 : PlayerSlot.Player1;
        var team = Team.Form(slot, Spawn(slot, roster));
        _players[slot] = player;
        _teams[slot] = team;
        _creatures.AddRange(team.Creatures);
        RaiseDomainEvent(new PlayerJoined(Id, slot, player));

        if (_players.Count == 2)
        {
            Start();
        }

        return Result.Success(slot);
    }

    public Result SubmitEvolutionChoice(PlayerSlot slot, EvolutionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        var open = RequireSubPhase(RoundSubPhase.Evolution, RoundErrors.EvolutionNotOpen);
        if (open.IsFailure)
        {
            return open;
        }

        var round = ActiveRound;
        var validated = EvolutionRules.ValidateChoice(slot, choice, Snapshots(), round, _resources, RuleSet);
        if (validated.IsFailure)
        {
            return validated;
        }

        // The pick is face down until the sub-phase ends (ADR 0089): the round records it and nothing on the
        // board changes, so the other player chooses without seeing it. RevealPurchases buys it.
        var accepted = round.SubmitEvolutionChoice(slot, choice);
        if (accepted.IsFailure)
        {
            return accepted;
        }

        RaiseDomainEvent(new EvolutionChoiceSubmitted(Id, round.Id, slot, choice));
        Drive();
        return Result.Success();
    }

    /// <summary>Gives up the player's remaining evolution picks for the round.</summary>
    public Result PassEvolution(PlayerSlot slot)
    {
        var open = RequireSubPhase(RoundSubPhase.Evolution, RoundErrors.EvolutionNotOpen);
        if (open.IsFailure)
        {
            return open;
        }

        var round = ActiveRound;
        var passed = round.PassEvolution(slot);
        if (passed.IsFailure)
        {
            return passed;
        }

        RaiseDomainEvent(new EvolutionPassed(Id, round.Id, slot));
        Drive();
        return Result.Success();
    }

    public Result SubmitSpeedChoice(PlayerSlot slot, SpeedChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        var open = RequireSubPhase(RoundSubPhase.Speed, RoundErrors.SpeedNotOpen);
        if (open.IsFailure)
        {
            return open;
        }

        var round = ActiveRound;
        var validated = SpeedRules.ValidateChoice(slot, choice, Snapshots());
        if (validated.IsFailure)
        {
            return validated;
        }

        var accepted = round.SubmitSpeedChoice(choice);
        if (accepted.IsFailure)
        {
            return accepted;
        }

        RaiseDomainEvent(new SpeedChoiceSubmitted(Id, round.Id, slot, choice));
        Drive();
        return Result.Success();
    }

    /// <summary>
    /// Orders the player's own tied creatures among the places their side won in the roll-off (ADR 0063).
    /// </summary>
    public Result SubmitTieOrder(PlayerSlot slot, IReadOnlyList<CreatureId> order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var open = RequireSubPhase(RoundSubPhase.TieOrder, RoundErrors.TieOrderNotOpen);
        if (open.IsFailure)
        {
            return open;
        }

        var round = ActiveRound;
        var validated = TieOrderRules.ValidateOrder(slot, order, round.Timeline);
        if (validated.IsFailure)
        {
            return validated;
        }

        var accepted = round.SubmitTieOrder(slot, order);
        if (accepted.IsFailure)
        {
            return accepted;
        }

        RaiseDomainEvent(new TieOrderSubmitted(Id, round.Id, slot, [.. order]));
        Drive();
        return Result.Success();
    }

    public Result SubmitIntent(PlayerSlot slot, CombatIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var open = RequireSubPhase(RoundSubPhase.IntentSelection, RoundErrors.IntentsNotOpen);
        if (open.IsFailure)
        {
            return open;
        }

        var round = ActiveRound;
        var validated = IntentRules.ValidateIntent(slot, intent, Snapshots(), _resources);
        if (validated.IsFailure)
        {
            return validated;
        }

        var accepted = round.SubmitIntent(slot, intent);
        if (accepted.IsFailure)
        {
            return accepted;
        }

        RaiseDomainEvent(new IntentSubmitted(Id, round.Id, slot, intent));
        Drive();
        return Result.Success();
    }

    /// <summary>
    /// Binds the targets of the creature whose slot has come up and resolves its action at once (ADR 0083). Only
    /// the owner of that creature may do so. When the action wipes a team, the match ends on it.
    /// </summary>
    public Result SubmitAction(PlayerSlot slot, CombatAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var open = RequireSubPhase(RoundSubPhase.Activation, RoundErrors.TargetingNotOpen);
        if (open.IsFailure)
        {
            return open;
        }

        var round = ActiveRound;
        var validated = ActionRules.ValidateAction(slot, action, Snapshots(), _resources);
        if (validated.IsFailure)
        {
            return validated;
        }

        var accepted = round.SubmitAction(action);
        if (accepted.IsFailure)
        {
            return accepted;
        }

        RaiseDomainEvent(new ActionRevealed(Id, round.Id, action));
        Activate(round, action);
        Drive();
        return Result.Success();
    }

    /// <summary>
    /// Ends the match now, with the other player as the winner (ADR 0087). A concession is a player's act and
    /// not a state of the board, so it is legal at any point of a match in progress, whatever the round is
    /// waiting on; the round it lands in is left where it was, not played out.
    /// </summary>
    public Result Concede(PlayerSlot slot)
    {
        if (State != MatchState.InProgress)
        {
            return Result.Failure(MatchErrors.NotInProgress);
        }

        var winner = slot == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;
        End(ActiveRound, new MatchOutcome(winner, MatchEndReason.Concession));
        return Result.Success();
    }

    private Round ActiveRound =>
        CurrentRound ?? throw new InvalidOperationException($"Match {Id} has no round in progress.");

    private List<Creature> Spawn(PlayerSlot slot, IReadOnlyList<CreatureDefinitionId> roster)
    {
        var creatures = new List<Creature>(roster.Count);
        foreach (var definition in roster)
        {
            var number = _creatures.Count + creatures.Count + 1;
            creatures.Add(Creature.Spawn(CreatureId.From(number), slot, _resources.GetCreature(definition)));
        }

        return creatures;
    }

    private void Start()
    {
        State = MatchState.InProgress;
        RaiseDomainEvent(new MatchStarted(Id, _players[PlayerSlot.Player1], _players[PlayerSlot.Player2], ContentHash));
        BeginRound(Round.First());
        Drive();
    }

    private void BeginRound(Round round)
    {
        CurrentRound = round;
        RaiseDomainEvent(new RoundStarted(Id, round.Id));
        RaiseDomainEvent(new SubPhaseEntered(Id, round.Id, round.SubPhase));
    }

    private Creature CreatureOf(CreatureId id) =>
        _creatures.Find(creature => creature.Id == id)
            ?? throw new InvalidOperationException($"Creature {id} is not in match {Id}.");

    private Result RequireSubPhase(RoundSubPhase expected, DomainError notOpen)
    {
        if (State != MatchState.InProgress)
        {
            return Result.Failure(MatchErrors.NotInProgress);
        }

        return ActiveRound.SubPhase == expected ? Result.Success() : Result.Failure(notOpen);
    }

    /// <summary>
    /// The phase driver: runs automatic steps and, when a progression gate says so, moves to the next sub-phase,
    /// until the round waits on a player or the match ends.
    /// </summary>
    private void Drive()
    {
        var progressed = true;
        while (State == MatchState.InProgress && progressed)
        {
            progressed = Step();
        }
    }

    private bool Step()
    {
        var round = ActiveRound;
        var creatures = Creatures;
        return round.SubPhase switch
        {
            RoundSubPhase.EnergyGain => Automatic(() => UpkeepRules.EnergyGain(creatures, RuleSet)),
            RoundSubPhase.OngoingEffects => ApplyOngoingEffects(round, creatures),
            RoundSubPhase.Evolution => EvolutionRules.Evaluate(Snapshots(), round, _resources, RuleSet).CanAdvance && Automatic(() => RevealPurchases(round)),
            RoundSubPhase.Speed => AdvanceIf(SpeedRules.Evaluate(Snapshots(), round).CanAdvance),
            RoundSubPhase.TurnOrderResolution => Automatic(BuildTimeline),
            RoundSubPhase.TieOrder => TieOrderRules.Evaluate(round).CanAdvance && Automatic(() => ApplyTieOrders(round)),
            RoundSubPhase.IntentSelection => AdvanceIf(IntentRules.Evaluate(round).CanAdvance),
            RoundSubPhase.Activation => ActivateUnavailableSlot(round) || AdvanceIf(ActionRules.Evaluate(round).CanAdvance),
            RoundSubPhase.Cleanup => Automatic(() => Cleanup(round, creatures)),
            RoundSubPhase.Finalization => FinalizeRound(),
            _ => throw new InvalidOperationException($"Sub-phase {round.SubPhase} has no driver step."),
        };
    }

    /// <summary>
    /// A creature that cannot take its slot is revealed with no targets and fizzles, without its owner being
    /// asked (ADR 0083). Returns whether a slot was activated this way.
    /// </summary>
    private bool ActivateUnavailableSlot(Round round)
    {
        if (round.PeekNextIntent() is not { } intent || ActionRules.CanTakeItsSlot(intent, Snapshots(), _resources))
        {
            return false;
        }

        var action = CombatAction.Bind(intent, []);
        var accepted = round.SubmitAction(action);
        if (accepted.IsFailure)
        {
            throw new InvalidOperationException($"Round {round.Id} refused the fizzle of {intent.Actor}: {accepted.Error.Message}");
        }

        RaiseDomainEvent(new ActionRevealed(Id, round.Id, action));
        Activate(round, action);
        return true;
    }

    /// <summary>Resolves a bound action on the board as it stands, applies it, and moves past its slot.</summary>
    private void Activate(Round round, CombatAction action)
    {
        // Every creature on the timeline chose a speed in planning, so the lookup cannot miss: a slot is only
        // built from a speed choice.
        var speed = round.SpeedChoiceOf(action.Actor)?.Speed
            ?? throw new InvalidOperationException($"Actor {action.Actor} is acting without a speed choice.");
        var before = Snapshots();
        var resolution = ResolutionRules.Resolve(action, before, _resources, RuleSet, _random, speed);
        var applied = CombatExecution.Apply(resolution, Creatures);
        var frame = new CombatActionFrame(before, Snapshots(), [.. round.Timeline.Slots], [.. round.Timeline.RollOffs]);
        round.MarkSlotActivated();
        RaiseDomainEvent(new CombatActionResolved(Id, round.Id, resolution, applied, frame));
        EndIfEliminated(round);
    }

    /// <summary>
    /// Ends the round and the match the moment a team is wiped (ADR 0083). Returns whether it did.
    /// </summary>
    private bool EndIfEliminated(Round round)
    {
        if (WinCondition.Elimination(_teams[PlayerSlot.Player1], _teams[PlayerSlot.Player2]) is not { } outcome)
        {
            return false;
        }

        RaiseDomainEvent(new RoundEnded(Id, round.Id));
        End(round, outcome);
        return true;
    }

    private void End(Round round, MatchOutcome outcome)
    {
        State = MatchState.Ended;
        Outcome = outcome;
        RaiseDomainEvent(new MatchEnded(Id, round.Id, outcome));
    }

    private void Cleanup(Round round, IReadOnlyList<Creature> creatures)
    {
        var expired = UpkeepRules.Cleanup(creatures);
        RaiseDomainEvent(new ConditionsExpired(Id, round.Id, Expired(expired)));
        List<CreatureId> immune = [.. creatures
            // A creature immune for good (ADR 0100) gains nothing when a stun it carried from before ends.
            .Where(creature => creature.IsStunImmune && !creature.Passive.StunImmunity && expired.TryGetValue(creature.Id, out var gone) && gone.Any(condition => condition.Effect is Stun))
            .Select(creature => creature.Id)];
        if (immune.Count > 0)
        {
            RaiseDomainEvent(new StunImmunityGained(Id, round.Id, immune));
        }
    }

    // A bleed can wipe a team at upkeep: the match ends there rather than playing a round for nobody (ADR 0083).
    private bool ApplyOngoingEffects(Round round, IReadOnlyList<Creature> creatures)
    {
        var ticks = UpkeepRules.OngoingEffects(creatures);
        RaiseDomainEvent(new OngoingEffectsApplied(Id, round.Id, ticks.EnergyRegenerationTicks, ticks.RegenerationTicks, ticks.BleedTicks));
        return !EndIfEliminated(round) && AdvanceIf(true);
    }

    private static Dictionary<CreatureId, IReadOnlyList<ConditionSnapshot>> Expired(IReadOnlyDictionary<CreatureId, IReadOnlyList<Condition>> expired) =>
        expired.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<ConditionSnapshot>)[.. entry.Value.Select(condition => condition.Snapshot())]);

    private bool Automatic(Action step)
    {
        step();
        return AdvanceIf(true);
    }

    private bool AdvanceIf(bool gateIsOpen)
    {
        if (!gateIsOpen)
        {
            return false;
        }

        var round = ActiveRound;
        round.Advance();
        RaiseDomainEvent(new SubPhaseEntered(Id, round.Id, round.SubPhase));
        return true;
    }

    private void BuildTimeline()
    {
        var round = ActiveRound;
        var timeline = TimelineBuilder.Build(Snapshots(), round.SpeedChoices, _random);
        round.SetTimeline(timeline);
        RaiseDomainEvent(new TimelineBuilt(Id, round.Id, timeline));
    }

    /// <summary>
    /// Buys every package picked this round, both players' at once, once neither has a pick left (ADR 0089).
    /// The picks cannot depend on each other: each creature takes at most one package an opportunity (ADR 0066)
    /// and a creature's prerequisites are its own, so the order they are bought in changes nothing.
    /// </summary>
    private void RevealPurchases(Round round)
    {
        List<EvolutionChoice> choices = [.. round.EvolutionChoicesOf(PlayerSlot.Player1), .. round.EvolutionChoicesOf(PlayerSlot.Player2)];
        if (choices.Count == 0)
        {
            return;
        }

        // Every package is looked up before any is bought: an unknown id would throw, and half a reveal is
        // worse than none. Unreachable while ValidateChoice only passes packages the catalogue holds.
        var tiers = choices.Select(choice => (choice.Creature, Tier: _resources.GetTier(choice.Tier))).ToList();
        foreach (var (creature, tier) in tiers)
        {
            // The whole package or none of it: BuyTier checks everything before it changes anything, so a
            // refusal here would mean the validation and the entity disagree, which is a bug rather than a rule.
            var bought = CreatureOf(creature).BuyTier(tier);
            if (bought.IsFailure)
            {
                throw new InvalidOperationException($"Creature {creature} refused a validated purchase: {bought.Error.Message}");
            }
        }

        RaiseDomainEvent(new PurchasesRevealed(Id, round.Id, choices));
    }

    private void ApplyTieOrders(Round round)
    {
        if (round.TieOrders.Count == 0)
        {
            return;
        }

        var timeline = TieOrderRules.Apply(round.Timeline, round.TieOrders);
        round.ReorderTimeline(timeline);
        RaiseDomainEvent(new TiesOrdered(Id, round.Id, timeline));
    }

    private bool FinalizeRound()
    {
        var round = ActiveRound;
        RaiseDomainEvent(new RoundEnded(Id, round.Id));

        var outcome = WinCondition.Evaluate(_teams[PlayerSlot.Player1], _teams[PlayerSlot.Player2], round.Number, RuleSet);
        if (outcome is not null)
        {
            End(round, outcome);
            return false;
        }

        BeginRound(round.Next());
        return true;
    }
}
