using System.Globalization;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using SpeedValue = DownfallArena.Domain.Matches.Rounds.Speed;

namespace DownfallArena.Application.Learning;

/// <summary>
/// Turns decisions into stable keys and numeric codes, and lists the actions a set of options offers. Every
/// key names the acting creature's board slot, since the same board asks several creatures in turn.
/// </summary>
public sealed class ActionEncoder(FeatureSchema schema)
{
    public const string PassKey = "pass";

    /// <summary>What every key of a spell and its targets chosen at the slot starts with (ADR 0110).</summary>
    public const string ActKeyPrefix = "act:";

    public FeatureSchema Schema => schema;

    public static EncodedAction Pass() => new(PassKey, ActionCode.Pass);

    public EncodedAction Evolve(BoardSlots slots, EvolutionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(choice);

        var slot = slots.SlotOf(choice.Creature);
        return new EncodedAction(string.Create(CultureInfo.InvariantCulture, $"evolve:{slot}:{choice.Tier.Value}"), new ActionCode(ActionKind.Evolve, slot, schema.TierIndex(choice.Tier), -1, 0));
    }

    public static EncodedAction Speed(BoardSlots slots, SpeedChoice choice)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(choice);

        var slot = slots.SlotOf(choice.Creature);
        return new EncodedAction(string.Create(CultureInfo.InvariantCulture, $"speed:{slot}:{choice.Speed}"), new ActionCode(ActionKind.Speed, slot, -1, (int)choice.Speed, 0));
    }

    public EncodedAction Intent(BoardSlots slots, CombatIntent intent)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(intent);

        var slot = slots.SlotOf(intent.Actor);
        return new EncodedAction(string.Create(CultureInfo.InvariantCulture, $"intent:{slot}:{intent.Spell.Value}"), new ActionCode(ActionKind.Intent, slot, schema.SpellIndex(intent.Spell), -1, 0));
    }

    /// <summary>
    /// The targets bound to a revealed spell. The spell is part of the action: the observation does not carry the
    /// actor's intent, and two spells can share a legal target set.
    /// </summary>
    public EncodedAction Targets(BoardSlots slots, CreatureId actor, SpellId spell, IReadOnlyList<CreatureId> targets)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(targets);

        var slot = slots.SlotOf(actor);
        var targetSlots = string.Join(',', targets.Select(slots.SlotOf).Order().Select(target => target.ToString(CultureInfo.InvariantCulture)));
        var key = string.Create(CultureInfo.InvariantCulture, $"targets:{slot}:{spell.Value}:{targetSlots}");
        return new EncodedAction(key, new ActionCode(ActionKind.Targets, slot, schema.SpellIndex(spell), -1, slots.MaskOf(targets)));
    }

    /// <summary>
    /// A spell and its targets chosen together at the slot (ADR 0110): one action, keyed apart from a revealed
    /// intent's targets, since the spell was a choice here and not a declaration made before combat.
    /// </summary>
    public EncodedAction Act(BoardSlots slots, CreatureId actor, SpellId spell, IReadOnlyList<CreatureId> targets)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(targets);

        var slot = slots.SlotOf(actor);
        var targetSlots = string.Join(',', targets.Select(slots.SlotOf).Order().Select(target => target.ToString(CultureInfo.InvariantCulture)));
        var key = string.Create(CultureInfo.InvariantCulture, $"{ActKeyPrefix}{slot}:{spell.Value}:{targetSlots}");
        return new EncodedAction(key, new ActionCode(ActionKind.Act, slot, schema.SpellIndex(spell), -1, slots.MaskOf(targets)));
    }

    /// <summary>
    /// Every action the options offer, in a stable order: a dataset records them next to the chosen one, and a
    /// policy scores them.
    /// </summary>
    public IReadOnlyList<EncodedAction> Candidates(BoardSlots slots, PlayerOptions options)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(options);

        return options.Kind switch
        {
            PlayerOptionsKind.Evolution when options.Evolution is { } evolution => [.. EvolutionCandidates(slots, evolution), Pass()],
            PlayerOptionsKind.Speed when options.Speed is { } speed => [.. SpeedCandidates(slots, speed)],
            PlayerOptionsKind.Intent when options.Intent is { } intent => [.. IntentCandidates(slots, intent)],
            PlayerOptionsKind.Target when options.Target is { } target => [.. TargetCandidates(slots, target)],
            PlayerOptionsKind.Action when options.Action is { } action => [.. ActCandidates(slots, action)],
            _ => [],
        };
    }

    private IEnumerable<EncodedAction> EvolutionCandidates(BoardSlots slots, EvolutionOptions options) =>
        options.Creatures.SelectMany(creature => creature.AvailableTiers.Select(tier => Evolve(slots, new EvolutionChoice(creature.Creature, tier))));

    private static IEnumerable<EncodedAction> SpeedCandidates(BoardSlots slots, SpeedOptions options) =>
        options.Missing.SelectMany(creature => new[]
        {
            Speed(slots, new SpeedChoice(creature, SpeedValue.Quick)),
            Speed(slots, new SpeedChoice(creature, SpeedValue.Standard)),
        });

    private IEnumerable<EncodedAction> IntentCandidates(BoardSlots slots, IntentOptions options) =>
        options.Creatures.SelectMany(creature => creature.CastableSpells.Select(spell => Intent(slots, new CombatIntent(creature.Creature, spell))));

    /// <summary>Every spell on offer with every legal target set of it, spells in the options' order (ADR 0110).</summary>
    private IEnumerable<EncodedAction> ActCandidates(BoardSlots slots, ActionOptions options) =>
        options.Spells.SelectMany(offered => TargetSets.Of(offered.LegalTargets).Select(targets => Act(slots, options.Actor, offered.Spell, targets)));

    private IEnumerable<EncodedAction> TargetCandidates(BoardSlots slots, TargetOptions options)
    {
        var legal = options.LegalTargets;
        if (!legal.IsCastable)
        {
            return [Targets(slots, options.Actor, options.Spell, [])];
        }

        return TargetSets.Of(legal).Select(targets => Targets(slots, options.Actor, options.Spell, targets));
    }
}
