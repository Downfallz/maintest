using DA.Game.Application.Matches.DTOs;
using DA.Game.Application.Matches.Features.Commands.RevealNextActionBindTargets;
using DA.Game.Application.Matches.Features.Commands.SubmitCombatIntent;
using DA.Game.Application.Matches.Features.Commands.SubmitEvolutionChoice;
using DA.Game.Application.Matches.Features.Commands.SubmitSpeedChoice;
using DA.Game.Application.Matches.Features.Queries.get_;
using DA.Game.Application.Matches.Features.Queries.GetPlayerOptions;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Simulation.NewFolder;
public sealed class RandomBotAgent(IMediator mediator, IGameResources resources) : IPlayerAgent
{
    public async Task DecideEvolutionAsync(AgentCtx ctx, PlayerOptionsView opt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(opt);
        ArgumentNullException.ThrowIfNull(ctx);

        var evol = opt.Evolution!;
        for (var i = 0; i < evol.RemainingPicks; i++)
        {
            // Refresh each pick to stay legal.
            opt = (await mediator.Send(new GetPlayerOptionsQuery(ctx.MatchId, ctx.Slot), ct)).Value!;
            var unlocks = (await mediator.Send(new GetUnlockableSpellsForPlayerQuery(ctx.MatchId, ctx.Slot), ct)).Value!;
            evol = opt.Evolution!;

            var creatureId = evol.LegalCreatureIds[Random.Shared.Next(evol.LegalCreatureIds.Count)];
            var unlockableSpells = unlocks.Creatures.Single(c => c.CreatureId == creatureId).UnlockableSpellIds;
            var spellId = unlockableSpells[Random.Shared.Next(unlockableSpells.Count)];
            var spell = resources.Spells.Single(s => s.Id == spellId);

            await mediator.Send(new SubmitEvolutionChoiceCommand(
                ctx.MatchId,
                ctx.Slot,
                new SpellUnlockChoiceDto(creatureId, spell)), ct);
        }
    }

    public async Task DecideSpeedAsync(AgentCtx ctx, PlayerOptionsView opt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(opt);
        ArgumentNullException.ThrowIfNull(ctx);

        var speed = opt.Speed!;
        foreach (var creatureId in speed.RequiredCreatures)
        {
            var chosen = Random.Shared.Next(2) == 0 ? SkillSpeed.Quick : SkillSpeed.Standard;

            await mediator.Send(new SubmitSpeedChoiceCommand(
                ctx.MatchId,
                ctx.Slot,
                new SpeedChoiceDto(creatureId, chosen)), ct);
        }
    }

    public async Task DecideCombatIntentAsync(AgentCtx ctx, PlayerOptionsView opt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(opt);
        ArgumentNullException.ThrowIfNull(ctx);

        var planning = opt.CombatPlanning!;
        var board = (await mediator.Send(new GetBoardStateForPlayerQuery(ctx.MatchId, ctx.Slot), ct)).Value!;

        foreach (var creatureId in planning.MissingCreatureIds)
        {
            var creature = board.FriendlyCreatures.Single(c => c.Id == creatureId);
            var spellId = creature.KnownSpellIds[Random.Shared.Next(creature.KnownSpellIds.Count)];
            var spell = resources.Spells.Single(s => s.Id == spellId);

            await mediator.Send(new SubmitCombatIntentCommand(
                ctx.MatchId,
                ctx.Slot,
                new CombatIntentDto(creatureId, spell)), ct);
        }
    }

    public async Task DecideRevealForActorAsync(
        AgentCtx ctx,
        PlayerOptionsView playerAction,
        CreatureId actorId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(playerAction);
        ArgumentNullException.ThrowIfNull(ctx);
        var action = playerAction.CombatAction!;
        // English comments as requested.
        if (action.MinTargets > 0 && action.LegalTargetIds.Count == 0)
            throw new InvalidOperationException($"No legal targets for actor {actorId.Value}.");

        var k = action.MaxTargets <= 0
            ? 0
            : Random.Shared.Next(action.MinTargets, action.MaxTargets + 1);

        var targets = PickDistinctTargets(action.LegalTargetIds, k);

        await mediator.Send(new RevealNextActionBindTargetsCommand(
            ctx.MatchId,
            actorId,
            targets.ToList()), ct);
    }

    private static IReadOnlyList<CreatureId> PickDistinctTargets(IReadOnlyList<CreatureId> pool, int count)
    {
        // English comments as requested.
        if (count <= 0) return Array.Empty<CreatureId>();
        if (pool.Count < count) throw new InvalidOperationException("Not enough targets.");

        var remaining = pool.ToList();
        var selected = new List<CreatureId>(count);

        for (var i = 0; i < count; i++)
        {
            var idx = Random.Shared.Next(remaining.Count);
            selected.Add(remaining[idx]);
            remaining.RemoveAt(idx);
        }
        return selected;
    }
}
