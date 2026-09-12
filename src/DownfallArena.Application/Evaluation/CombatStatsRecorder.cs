using System.Collections.Concurrent;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// Counts, per match and per player, the actions that resolved, the ones that fizzled, and the critical hits,
/// from the combat events; and the same per spell, with what each spell's casts added up to. Registered as a
/// listener wherever an evaluation reports fizzle and crit rates or what a spell did.
/// <para>
/// The effect totals are what the board took. The counts around them -- resolved, fizzled, critical -- are
/// what the roll said, so a critical a target's defense absorbs entirely is a critical that dealt nothing.
/// </para>
/// </summary>
public sealed class CombatStatsRecorder(IMatchRepository matches) : DomainEventListener<CombatActionResolved>
{
    // Concurrent on the outside only, because matches may play at the same time (`BatchRunner`) while each
    // match plays its own rounds in order. Two matches never share a key -- the MatchId is part of it -- and
    // the two slots of one match are walked by that one match, so every inner tally has a single writer and
    // needs no protection of its own. What the outer maps have to survive is an insert from one match while
    // another reads or enumerates, which is what `Attribute` and `Forget` do.
    private readonly ConcurrentDictionary<(MatchId Match, PlayerSlot Slot), CombatStats> _stats = new();
    private readonly ConcurrentDictionary<(MatchId Match, PlayerSlot Slot), Dictionary<string, SpellEffects>> _spells = new();
    private readonly ConcurrentDictionary<(MatchId Match, PlayerSlot Slot), HashSet<CreatureId>> _casters = new();

    /// <summary>
    /// The two sides of a match, so a lookup names them instead of scanning every key of every match.
    /// `ConcurrentDictionary.Keys` allocates the whole key list on each access where `Dictionary.Keys` is a
    /// view, and the walk below runs once per condition tick, so scanning was both a growing allocation and
    /// a search for something that can only be in one of two places.
    /// </summary>
    private static readonly PlayerSlot[] Slots = [PlayerSlot.Player1, PlayerSlot.Player2];

    public CombatStats Of(MatchId matchId, PlayerSlot slot) => _stats.GetValueOrDefault((matchId, slot)) ?? new CombatStats(0, 0, 0);

    /// <summary>What each spell one side cast in a match did, by spell id.</summary>
    public IReadOnlyDictionary<string, SpellEffects> SpellsOf(MatchId matchId, PlayerSlot slot) =>
        _spells.GetValueOrDefault((matchId, slot)) ?? new Dictionary<string, SpellEffects>(StringComparer.Ordinal);

    /// <summary>Forgets a match once its numbers were read.</summary>
    public void Forget(MatchId matchId)
    {
        foreach (var slot in Slots)
        {
            var key = (matchId, slot);
            _stats.TryRemove(key, out _);
            _spells.TryRemove(key, out _);
            _casters.TryRemove(key, out _);
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

        var spells = _spells.GetOrAdd(key, _ => new Dictionary<string, SpellEffects>(StringComparer.Ordinal));
        _casters.GetOrAdd(key, _ => []).Add(actor);

        var spell = resolution.Action.Spell.Value;
        spells[spell] = (spells.GetValueOrDefault(spell) ?? SpellEffects.None).Plus(Effects(resolution, domainEvent.AppliedOutcomes));
    }

    /// <summary>
    /// What the start of a round took, gave and healed, counted against the cast that asked for it rather than
    /// against the creature it happened to (ADR 0027). A tick with no share is a condition nothing cast.
    /// </summary>
    public void Record(OngoingEffectsApplied domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        foreach (var share in domainEvent.BleedTicks.SelectMany(tick => tick.Shares))
        {
            Attribute(domainEvent.MatchId, share, effects => effects with { ConditionDamage = share.Amount });
        }

        foreach (var share in domainEvent.RegenerationTicks.SelectMany(tick => tick.Shares))
        {
            Attribute(domainEvent.MatchId, share, effects => effects with { ConditionHealing = share.Amount });
        }

        foreach (var share in domainEvent.EnergyRegenerationTicks.SelectMany(tick => tick.Shares))
        {
            Attribute(domainEvent.MatchId, share, effects => effects with { ConditionEnergy = share.Amount });
        }
    }

    /// <summary>
    /// Adds one share to the caster's side. The side is read from the tally the caster's own casts already
    /// built: a condition cannot tick before the cast that applied it was recorded, so the row is always there.
    /// <para>
    /// The walk stops at the first side that owns the caster, and which side that is does not depend on the
    /// order the keys come out in: a creature has one owner, so exactly one side of the match can claim it.
    /// </para>
    /// </summary>
    private void Attribute(MatchId matchId, ConditionShare share, Func<SpellEffects, SpellEffects> tally)
    {
        var spell = share.Source.Spell.Value;
        foreach (var slot in Slots)
        {
            var key = (matchId, slot);
            if (_casters.GetValueOrDefault(key)?.Contains(share.Source.Caster) != true
                || !_spells.TryGetValue(key, out var spells))
            {
                continue;
            }

            spells[spell] = (spells.GetValueOrDefault(spell) ?? SpellEffects.None).Plus(tally(SpellEffects.None));
            return;
        }
    }

    /// <summary>
    /// One resolution as a tally: what it was, and what its outcomes actually did. The applied outcomes, not
    /// the computed ones: a hit that overkills, a heal on a creature already full and a condition its stacking
    /// policy refuses all resolve, and all of them would otherwise be counted at the size they aimed for.
    /// </summary>
    private static SpellEffects Effects(CombatResolution resolution, IReadOnlyList<EffectOutcome> applied)
    {
        if (resolution.Fizzled)
        {
            return SpellEffects.None with { Fizzled = 1 };
        }

        return new SpellEffects(
            Resolved: 1,
            Fizzled: 0,
            Criticals: resolution.IsCritical ? 1 : 0,
            Damage: applied.OfType<DamageOutcome>().Sum(outcome => outcome.Amount),
            Healing: applied.OfType<HealOutcome>().Sum(outcome => outcome.Amount),
            Energy: applied.OfType<EnergyOutcome>().Sum(outcome => outcome.Amount),
            Stuns: Conditions<Stun>(applied),
            Bleeds: Conditions<Bleed>(applied),
            Regens: Conditions<Regeneration>(applied),
            EnergyRegenerations: Conditions<EnergyRegeneration>(applied),
            DefenseBuffs: Conditions<DefenseBuff>(applied),
            InitiativeDebuffs: Conditions<InitiativeDebuff>(applied));
    }

    private static int Conditions<TEffect>(IReadOnlyList<EffectOutcome> applied)
        where TEffect : LastingEffect =>
        applied.OfType<ConditionOutcome>().Count(outcome => outcome.Effect is TEffect);
}
