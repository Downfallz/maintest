using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// Counts, per match and per player, the actions that resolved, the ones that fizzled, and the critical hits,
/// from the combat events; and the same per spell, with what each spell's casts added up to. Registered as a
/// listener wherever an evaluation reports fizzle and crit rates or what a spell did.
/// </summary>
public sealed class CombatStatsRecorder(IMatchRepository matches) : DomainEventListener<CombatActionResolved>
{
    private readonly Dictionary<(MatchId Match, PlayerSlot Slot), CombatStats> _stats = [];
    private readonly Dictionary<(MatchId Match, PlayerSlot Slot), Dictionary<string, SpellEffects>> _spells = [];

    public CombatStats Of(MatchId matchId, PlayerSlot slot) => _stats.GetValueOrDefault((matchId, slot)) ?? new CombatStats(0, 0, 0);

    /// <summary>What each spell one side cast in a match did, by spell id.</summary>
    public IReadOnlyDictionary<string, SpellEffects> SpellsOf(MatchId matchId, PlayerSlot slot) =>
        _spells.GetValueOrDefault((matchId, slot)) ?? new Dictionary<string, SpellEffects>(StringComparer.Ordinal);

    /// <summary>Forgets a match once its numbers were read.</summary>
    public void Forget(MatchId matchId)
    {
        foreach (var key in _stats.Keys.Where(key => key.Match == matchId).ToList())
        {
            _stats.Remove(key);
        }

        foreach (var key in _spells.Keys.Where(key => key.Match == matchId).ToList())
        {
            _spells.Remove(key);
        }
    }

    protected override async Task HandleAsync(CombatActionResolved domainEvent, CancellationToken cancellationToken)
    {
        var match = await matches.FindAsync(domainEvent.MatchId, cancellationToken)
            ?? throw new InvalidOperationException($"Match {domainEvent.MatchId} raised an event but is not stored.");
        var resolution = domainEvent.Resolution;
        var actor = resolution.Action.Actor;
        var owner = match.Creatures.First(creature => creature.Id == actor).Owner;
        var key = (domainEvent.MatchId, owner);

        var current = _stats.GetValueOrDefault(key) ?? new CombatStats(0, 0, 0);
        _stats[key] = new CombatStats(current.Actions + 1, current.Fizzles + (resolution.Fizzled ? 1 : 0), current.Criticals + (resolution.IsCritical ? 1 : 0));

        if (!_spells.TryGetValue(key, out var spells))
        {
            spells = new Dictionary<string, SpellEffects>(StringComparer.Ordinal);
            _spells[key] = spells;
        }

        var spell = resolution.Action.Spell.Value;
        spells[spell] = (spells.GetValueOrDefault(spell) ?? SpellEffects.None).Plus(Effects(resolution));
    }

    /// <summary>One resolution as a tally: what it was, and what its outcomes did.</summary>
    private static SpellEffects Effects(CombatResolution resolution)
    {
        if (resolution.Fizzled)
        {
            return SpellEffects.None with { Fizzled = 1 };
        }

        return new SpellEffects(
            Resolved: 1,
            Fizzled: 0,
            Criticals: resolution.IsCritical ? 1 : 0,
            Damage: resolution.Outcomes.OfType<DamageOutcome>().Sum(outcome => outcome.Amount),
            Healing: resolution.Outcomes.OfType<HealOutcome>().Sum(outcome => outcome.Amount),
            Stuns: Conditions<Stun>(resolution),
            Bleeds: Conditions<Bleed>(resolution),
            Buffs: Conditions<DefenseBuff>(resolution) + Conditions<InitiativeDebuff>(resolution));
    }

    private static int Conditions<TEffect>(CombatResolution resolution)
        where TEffect : LastingEffect =>
        resolution.Outcomes.OfType<ConditionOutcome>().Count(outcome => outcome.Effect is TEffect);
}
