using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;
using System.Collections.ObjectModel;

namespace DA.Game.Domain2.Matches.Contexts;

public sealed record CreaturePerspective(
    CreatureId ActorId,
    IReadOnlyList<CreatureSnapshot> Creatures,
    MatchState State,
    RoundPhase? Phase,
    IReadOnlyCollection<SpellUnlockChoice>? Player1Choices,
    IReadOnlyCollection<SpellUnlockChoice>? Player2Choices,
    IReadOnlyCollection<SpeedChoice>? Player1SpeedChoices,
    IReadOnlyCollection<SpeedChoice>? Player2SpeedChoices,
    ReadOnlyDictionary<CreatureId, CombatActionChoice>? CombatActionChoices,
    CombatTimeline? Timeline,
    TurnCursor? RevealCursor,
    TurnCursor? ResolveCursor
    )
{

    public CreatureSnapshot Actor =>
        Creatures.Single(c => c.CharacterId == ActorId);

    // Vue allié/ennemi du point de vue de l’acteur
    public IReadOnlyList<CreatureSnapshot> AlliesOfActor =>
        Creatures.Where(c => c.OwnerSlot == Actor.OwnerSlot).ToList();

    public IReadOnlyList<CreatureSnapshot> EnemiesOfActor =>
        Creatures.Where(c => c.OwnerSlot != Actor.OwnerSlot).ToList();

    public static Result<CreaturePerspective> FromMatch(
        CreatureId creatureId,
        Match match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var snapshots = match.AllCreatures
            .Select(CreatureSnapshot.From)
            .ToList();

        if (snapshots.Count == 0)
            return Result<CreaturePerspective>.Fail("Match has no creatures.");

        // Actor must exist
        var actor = snapshots.SingleOrDefault(c => c.CharacterId == creatureId);
        if (actor is null)
            return Result<CreaturePerspective>.Fail("Actor not found in match.");

        return Result<CreaturePerspective>.Ok(
            new CreaturePerspective(creatureId, 
            snapshots.AsReadOnly(),
            match.State,
            match.CurrentRound?.Phase,
            match.CurrentRound?.Player1EvolutionChoices,
            match.CurrentRound?.Player2EvolutionChoices,
            match.CurrentRound?.Player1SpeedChoices,
            match.CurrentRound?.Player2SpeedChoices,
            match.CurrentRound?.CombatActionsByCreature,
            match.CurrentRound?.Timeline,
            match.CurrentRound?.RevealCursor,
            match.CurrentRound?.ResolveCursor));
    }
}