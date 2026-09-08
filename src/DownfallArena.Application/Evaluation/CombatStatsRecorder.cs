using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// Counts, per match and per player, the actions that resolved, the ones that fizzled, and the critical hits,
/// from the combat events. Registered as a listener wherever an evaluation reports fizzle and crit rates.
/// </summary>
public sealed class CombatStatsRecorder(IMatchRepository matches) : DomainEventListener<CombatActionResolved>
{
    private readonly Dictionary<(MatchId Match, PlayerSlot Slot), CombatStats> _stats = [];

    public CombatStats Of(MatchId matchId, PlayerSlot slot) => _stats.GetValueOrDefault((matchId, slot)) ?? new CombatStats(0, 0, 0);

    /// <summary>Forgets a match once its numbers were read.</summary>
    public void Forget(MatchId matchId)
    {
        foreach (var key in _stats.Keys.Where(key => key.Match == matchId).ToList())
        {
            _stats.Remove(key);
        }
    }

    protected override async Task HandleAsync(CombatActionResolved domainEvent, CancellationToken cancellationToken)
    {
        var match = await matches.FindAsync(domainEvent.MatchId, cancellationToken)
            ?? throw new InvalidOperationException($"Match {domainEvent.MatchId} raised an event but is not stored.");
        var actor = domainEvent.Resolution.Action.Actor;
        var owner = match.Creatures.First(creature => creature.Id == actor).Owner;
        var key = (domainEvent.MatchId, owner);
        var current = _stats.GetValueOrDefault(key) ?? new CombatStats(0, 0, 0);
        var resolution = domainEvent.Resolution;
        _stats[key] = new CombatStats(current.Actions + 1, current.Fizzles + (resolution.Fizzled ? 1 : 0), current.Criticals + (resolution.IsCritical ? 1 : 0));
    }
}
