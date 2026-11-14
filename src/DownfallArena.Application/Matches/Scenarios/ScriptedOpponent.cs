using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// The seat a person does not play in a match scenario (docs/tabletop/scenarios-plan.md, stage S2): it gives the
/// source's recorded answers, in order, while each one answers the question asked (same kind, same creature) and
/// is among the options offered, then hands over to <paramref name="fallback" /> for good at the first one that
/// does not. Under hidden intents its answers for the round being replayed were given before the person's, which is
/// what the opponent would have done.
/// </summary>
public sealed class ScriptedOpponent(IEnumerable<PlayerDecision> recorded, IPlayerAgent fallback) : IPlayerAgent
{
    private readonly Queue<PlayerDecision> _script = new(recorded ?? throw new ArgumentNullException(nameof(recorded)));
    private readonly IPlayerAgent _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

    /// <summary>Whether the recorded answers stopped fitting and the fallback answers from now on.</summary>
    public bool TookOver { get; private set; }

    /// <summary>Said once, when the fallback takes over.</summary>
    public event EventHandler? HandedOver;

    /// <summary>The seat's recorded answers after a scenario's cut, scripted over <paramref name="fallback" />.</summary>
    public static ScriptedOpponent Of(ScenarioCut scenario, PlayerSlot slot, IPlayerAgent fallback)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return new ScriptedOpponent(scenario.After.OfType<DecisionEntry>().Where(entry => entry.Decision.Slot == slot).Select(entry => entry.Decision.Decision), fallback);
    }

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) =>
        Follows(new PlayerOptions { Kind = PlayerOptionsKind.Evolution, Evolution = options }) is { } next
            ? next.IsPass ? EvolutionDecision.Pass : EvolutionDecision.Unlock(new EvolutionChoice(next.Creature!.Value, next.Tier!))
            : _fallback.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) =>
        Follows(new PlayerOptions { Kind = PlayerOptionsKind.Speed, Speed = new SpeedOptions([creature]) }) is { } next
            ? next.Speed!.Value
            : _fallback.DecideSpeed(board, creature);

    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) =>
        Follows(new PlayerOptions { Kind = PlayerOptionsKind.TieOrder, TieOrder = options }) is { } next
            ? next.Order
            : _fallback.DecideTieOrder(board, options);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) =>
        Follows(new PlayerOptions { Kind = PlayerOptionsKind.Intent, Intent = new IntentOptions([intentOption]) }) is { } next
            ? next.Spell!
            : _fallback.DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) =>
        Follows(new PlayerOptions { Kind = PlayerOptionsKind.Target, Target = options }) is { } next
            ? next.Targets
            : _fallback.DecideTargets(board, options);

    public ChosenAction DecideAction(PlayerBoardState board, ActionOptions options) =>
        Follows(new PlayerOptions { Kind = PlayerOptionsKind.Action, Action = options }) is { } next
            ? new ChosenAction(next.Spell!, next.Targets)
            : _fallback.DecideAction(board, options);

    /// <summary>
    /// The next recorded answer when it answers what is asked, taken off the script; none once the fallback has
    /// taken over, which it does here, for good, at the first answer that does not fit or when there is none left.
    /// </summary>
    private PlayerDecision? Follows(PlayerOptions asked)
    {
        if (TookOver)
        {
            return null;
        }

        if (_script.TryPeek(out var next) && PlayerDecisionCheck.Validate(asked, next).IsSuccess)
        {
            return _script.Dequeue();
        }

        TookOver = true;
        _script.Clear();
        HandedOver?.Invoke(this, EventArgs.Empty);
        return null;
    }
}
