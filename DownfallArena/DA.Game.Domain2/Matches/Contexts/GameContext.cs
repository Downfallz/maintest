using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Combat.Execution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.RoundVo;
using DA.Game.Domain2.Matches.ValueObjects.SpeedVo;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Contexts;

public sealed record GameContext(
    CreatureId ActorId,
    IReadOnlyList<CharacterSnapshot> Creatures,
    MatchState State,
    RoundPhase? Phase,
    IReadOnlyCollection<SpellUnlockChoice>? Player1Choices,
    IReadOnlyCollection<SpellUnlockChoice>? Player2Choices,
    IReadOnlyCollection<SpeedChoice>? Player1SpeedChoices,
    IReadOnlyCollection<SpeedChoice>? Player2SpeedChoices,
    ReadOnlyDictionary<CreatureId, CombatActionChoice>? CombatActionChoices,
    CombatTimeline? Timeline,
    TurnCursor? Cursor
    )
{

    public CharacterSnapshot Actor =>
        Creatures.Single(c => c.CharacterId == ActorId);

    // Vue allié/ennemi du point de vue de l’acteur
    public IReadOnlyList<CharacterSnapshot> AlliesOfActor =>
        Creatures.Where(c => c.OwnerSlot == Actor.OwnerSlot).ToList();

    public IReadOnlyList<CharacterSnapshot> EnemiesOfActor =>
        Creatures.Where(c => c.OwnerSlot != Actor.OwnerSlot).ToList();

    public static Result<GameContext> FromMatch(
        CreatureId creatureId,
        Match match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var snapshots = match.AllCreatures
            .Select(CharacterSnapshot.From)
            .ToList();

        if (snapshots.Count == 0)
            return Result<GameContext>.Fail("Match has no creatures.");

        // Actor must exist
        var actor = snapshots.SingleOrDefault(c => c.CharacterId == creatureId);
        if (actor is null)
            return Result<GameContext>.Fail("Actor not found in match.");

        return Result<GameContext>.Ok(
            new GameContext(creatureId, 
            snapshots.AsReadOnly(),
            match.State,
            match.CurrentRound?.Phase,
            match.CurrentRound?.Player1Choices,
            match.CurrentRound?.Player2Choices,
            match.CurrentRound?.Player1SpeedChoices,
            match.CurrentRound?.Player2SpeedChoices,
            match.CurrentRound?.CombatActionChoices,
            match.CurrentRound?.Timeline,
            match.CurrentRound?.Cursor));
    }
}