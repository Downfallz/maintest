using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.Domain.Resources;
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

        var accepted = round.SubmitEvolutionChoice(slot, choice);
        if (accepted.IsFailure)
        {
            return accepted;
        }

        var unlocked = CreatureOf(choice.Creature).UnlockSpell(choice.Spell);
        if (unlocked.IsFailure)
        {
            throw new InvalidOperationException($"Creature {choice.Creature} refused a validated unlock: {unlocked.Error.Message}");
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
    /// Reveals the next intent of the timeline by binding its targets. Only the owner of that intent may do so.
    /// </summary>
    public Result SubmitAction(PlayerSlot slot, CombatAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var open = RequireSubPhase(RoundSubPhase.RevealAndTarget, RoundErrors.TargetingNotOpen);
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
        Drive();
        return Result.Success();
    }

    /// <summary>
    /// Resolves the action at the resolve cursor and applies it. When it was the last one, the round is
    /// finalized and either the match ends or the next round starts.
    /// </summary>
    public Result<CombatStep> ResolveNextAction()
    {
        var open = RequireSubPhase(RoundSubPhase.ActionResolution, RoundErrors.ResolutionNotOpen);
        if (open.IsFailure)
        {
            return Result.Failure<CombatStep>(open.Error);
        }

        var round = ActiveRound;
        var action = round.NextActionToResolve();
        var resolution = ResolutionRules.Resolve(action, Snapshots(), _resources, RuleSet, _random);
        CombatExecution.Apply(resolution, Creatures);
        round.MarkActionResolved();
        RaiseDomainEvent(new CombatActionResolved(Id, round.Id, resolution));
        Drive();

        return Result.Success(new CombatStep(round.Id, resolution, round.IsFinalized, State == MatchState.Ended));
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
            RoundSubPhase.OngoingEffects => Automatic(() => RaiseDomainEvent(new OngoingEffectsApplied(Id, round.Id, UpkeepRules.OngoingEffects(creatures)))),
            RoundSubPhase.Evolution => AdvanceIf(EvolutionRules.Evaluate(Snapshots(), round, _resources, RuleSet).CanAdvance),
            RoundSubPhase.Speed => AdvanceIf(SpeedRules.Evaluate(Snapshots(), round).CanAdvance),
            RoundSubPhase.TurnOrderResolution => Automatic(BuildTimeline),
            RoundSubPhase.IntentSelection => AdvanceIf(IntentRules.Evaluate(round).CanAdvance),
            RoundSubPhase.RevealAndTarget => AdvanceIf(ActionRules.Evaluate(round).CanAdvance),
            RoundSubPhase.ActionResolution => AdvanceIf(round.IsCombatResolved),
            RoundSubPhase.Cleanup => Automatic(() => RaiseDomainEvent(new ConditionsExpired(Id, round.Id, Expired(UpkeepRules.Cleanup(creatures))))),
            RoundSubPhase.Finalization => FinalizeRound(),
            _ => throw new InvalidOperationException($"Sub-phase {round.SubPhase} has no driver step."),
        };
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
        var timeline = TimelineBuilder.Build(Snapshots(), round.SpeedChoices);
        round.SetTimeline(timeline);
        RaiseDomainEvent(new TimelineBuilt(Id, round.Id, timeline));
    }

    private bool FinalizeRound()
    {
        var round = ActiveRound;
        RaiseDomainEvent(new RoundEnded(Id, round.Id));

        var outcome = WinCondition.Evaluate(_teams[PlayerSlot.Player1], _teams[PlayerSlot.Player2], round.Number, RuleSet);
        if (outcome is not null)
        {
            State = MatchState.Ended;
            Outcome = outcome;
            RaiseDomainEvent(new MatchEnded(Id, round.Id, outcome));
            return false;
        }

        BeginRound(round.Next());
        return true;
    }
}
