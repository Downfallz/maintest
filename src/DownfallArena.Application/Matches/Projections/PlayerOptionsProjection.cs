using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// Derives a player's options from the progression gates and targeting rules of the domain, so the UI, the
/// bots, and the aggregate agree on what is legal.
/// </summary>
public static class PlayerOptionsProjection
{
    public static PlayerOptions Build(Match match, PlayerSlot slot, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(resources);

        if (match.State == MatchState.Ended)
        {
            return new PlayerOptions { Kind = PlayerOptionsKind.Ended };
        }

        if (match.CurrentRound is not { } round)
        {
            return new PlayerOptions { Kind = PlayerOptionsKind.Waiting };
        }

        var snapshots = match.Snapshots();
        return round.SubPhase switch
        {
            RoundSubPhase.Evolution => Evolution(match, round, slot, snapshots, resources),
            RoundSubPhase.Speed => Speed(round, slot, snapshots),
            RoundSubPhase.IntentSelection => Intent(round, slot, snapshots, resources),
            RoundSubPhase.RevealAndTarget => Target(round, slot, snapshots, resources),
            RoundSubPhase.ActionResolution => new PlayerOptions { Kind = PlayerOptionsKind.Resolution, SubPhase = round.SubPhase },
            _ => Waiting(round),
        };
    }

    private static PlayerOptions Waiting(Round round) => new() { Kind = PlayerOptionsKind.Waiting, SubPhase = round.SubPhase };

    private static PlayerOptions Evolution(Match match, Round round, PlayerSlot slot, IReadOnlyList<CreatureSnapshot> snapshots, IGameResources resources)
    {
        var remaining = EvolutionRules.Evaluate(snapshots, round, resources, match.RuleSet).RemainingPicksOf(slot);
        if (remaining == 0)
        {
            return Waiting(round);
        }

        var creatures = snapshots
            .Where(creature => creature.Owner == slot && creature.IsAlive)
            .Select(creature => new EvolutionOption(creature.Id, TalentUnlocks.UnlockableSpells(creature, resources.GetTalentTree(creature.TalentTree))))
            .Where(option => option.UnlockableSpells.Count > 0)
            .ToList();

        return new PlayerOptions
        {
            Kind = PlayerOptionsKind.Evolution,
            SubPhase = round.SubPhase,
            Evolution = new EvolutionOptions(remaining, creatures),
        };
    }

    private static PlayerOptions Speed(Round round, PlayerSlot slot, IReadOnlyList<CreatureSnapshot> snapshots)
    {
        var missing = SpeedRules.Evaluate(snapshots, round).MissingOf(slot);
        return missing.Count == 0
            ? Waiting(round)
            : new PlayerOptions { Kind = PlayerOptionsKind.Speed, SubPhase = round.SubPhase, Speed = new SpeedOptions(missing) };
    }

    private static PlayerOptions Intent(Round round, PlayerSlot slot, IReadOnlyList<CreatureSnapshot> snapshots, IGameResources resources)
    {
        var own = snapshots.Where(creature => creature.Owner == slot).ToDictionary(creature => creature.Id);
        var creatures = IntentRules.Evaluate(round).Missing
            .Where(own.ContainsKey)
            .Select(id => new IntentOption(id, CastableSpells(own[id], resources)))
            .ToList();

        return creatures.Count == 0
            ? Waiting(round)
            : new PlayerOptions { Kind = PlayerOptionsKind.Intent, SubPhase = round.SubPhase, Intent = new IntentOptions(creatures) };
    }

    private static PlayerOptions Target(Round round, PlayerSlot slot, IReadOnlyList<CreatureSnapshot> snapshots, IGameResources resources)
    {
        if (round.NextSlotToReveal is not { } next || next.Owner != slot)
        {
            return Waiting(round);
        }

        var intent = round.IntentOf(next.Creature)
            ?? throw new InvalidOperationException($"Creature {next.Creature} is on the timeline without an intent.");
        var actor = snapshots.First(creature => creature.Id == next.Creature);
        var legal = TargetingRules.LegalTargets(actor, resources.GetSpell(intent.Spell), snapshots);

        return new PlayerOptions
        {
            Kind = PlayerOptionsKind.Target,
            SubPhase = round.SubPhase,
            Target = new TargetOptions(actor.Id, intent.Spell, legal),
        };
    }

    private static List<SpellId> CastableSpells(CreatureSnapshot creature, IGameResources resources) =>
        [.. creature.KnownSpells.Where(spell => resources.GetSpell(spell).Stats.Cost <= creature.Energy).OrderBy(spell => spell.Value, StringComparer.Ordinal)];
}
