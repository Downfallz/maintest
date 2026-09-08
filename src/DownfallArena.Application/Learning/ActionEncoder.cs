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

    public FeatureSchema Schema => schema;

    public static EncodedAction Pass() => new(PassKey, ActionCode.Pass);

    public EncodedAction Evolve(BoardSlots slots, EvolutionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(choice);

        var slot = slots.SlotOf(choice.Creature);
        return new EncodedAction(string.Create(CultureInfo.InvariantCulture, $"evolve:{slot}:{choice.Spell.Value}"), new ActionCode(ActionKind.Evolve, slot, schema.SpellIndex(choice.Spell), -1, 0));
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
            _ => [],
        };
    }

    private IEnumerable<EncodedAction> EvolutionCandidates(BoardSlots slots, EvolutionOptions options) =>
        options.Creatures.SelectMany(creature => creature.UnlockableSpells.Select(spell => Evolve(slots, new EvolutionChoice(creature.Creature, spell))));

    private static IEnumerable<EncodedAction> SpeedCandidates(BoardSlots slots, SpeedOptions options) =>
        options.Missing.SelectMany(creature => new[]
        {
            Speed(slots, new SpeedChoice(creature, SpeedValue.Quick)),
            Speed(slots, new SpeedChoice(creature, SpeedValue.Standard)),
        });

    private IEnumerable<EncodedAction> IntentCandidates(BoardSlots slots, IntentOptions options) =>
        options.Creatures.SelectMany(creature => creature.CastableSpells.Select(spell => Intent(slots, new CombatIntent(creature.Creature, spell))));

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
