using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

public sealed record PlayerBoardState
{
    public required MatchId MatchId { get; init; }
    public required PlayerSlot Slot { get; init; }
    public required MatchState MatchState { get; init; }
    public RoundPhase? RoundPhase { get; init; }
    public RoundSubPhase? RoundSubPhase { get; init; }

    public required IReadOnlyList<CreatureSnapshot> FriendlyCreatures { get; init; }
    public required IReadOnlyList<CreatureSnapshot> EnemyCreatures { get; init; }

    public IReadOnlyCollection<SpellUnlockChoice> EvolutionChoices { get; init; } = Array.Empty<SpellUnlockChoice>();
    public IReadOnlyCollection<SpeedChoice> SpeedChoices { get; init; } = Array.Empty<SpeedChoice>();
    public IReadOnlyDictionary<CreatureId, CombatActionIntent> CombatIntentsByCreature { get; init; }
        = new Dictionary<CreatureId, CombatActionIntent>();

    public CombatTimeline? Timeline { get; init; }
    public TurnCursor? RevealCursor { get; init; }
    public TurnCursor? ResolveCursor { get; init; }
}
