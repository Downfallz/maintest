using AutoMapper;
using DA.Game.Application.Matches.Features.Commands.CreateMatch;
using DA.Game.Application.Matches.Features.Commands.JoinMatch;
using DA.Game.Application.Matches.Features.Commands.ResolveNextCombatAction;
using DA.Game.Application.Matches.Features.Commands.SubmitEvolutionChoice;
using DA.Game.Application.Matches.Features.Queries.get_;
using DA.Game.Application.Matches.Features.Queries.GetPlayerOptions;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Application.Players.Features.Create;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Players;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Resources;
using MediatR;

namespace DA.Game.Application.Simulation.NewFolder;

public sealed class SimulationMatchDriver
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    private readonly IGameResources _res;

    private readonly IPlayerAgent _p1;
    private readonly IPlayerAgent _p2;

    public SimulationMatchDriver(IMediator mediator, IMapper mapper, IGameResources res, IPlayerAgent p1, IPlayerAgent p2)
    {
        _mediator = mediator;
        _mapper = mapper;
        _res = res;
        _p1 = p1;
        _p2 = p2;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        // 1) Setup match + players
        var matchId = (await _mediator.Send(new CreateMatchCommand(), ct)).Value!;
        var p1 = (await _mediator.Send(new CreatePlayerCommand("BotOne", ActorKind.Bot), ct)).Value!;
        var p2 = (await _mediator.Send(new CreatePlayerCommand("BotTwo", ActorKind.Bot), ct)).Value!;

        await _mediator.Send(new JoinMatchCommand(matchId, _mapper.Map<PlayerRef>(p1)), ct);
        await _mediator.Send(new JoinMatchCommand(matchId, _mapper.Map<PlayerRef>(p2)), ct);

        // 2) Main loop: options-driven
        while (true)
        {
            await TickPlayerAsync(matchId, PlayerSlot.Player1, _p1, ct);
            await TickPlayerAsync(matchId, PlayerSlot.Player2, _p2, ct);

            // Reveal is “global” in your POC (next actor can be either side)
            await TickRevealUntilNoneAsync(matchId, ct);

            // Resolve until round completes (your step-by-step engine)
            var last = await ResolveRoundAsync(matchId, ct);
            if (last.IsMatchEnded)
                break;
        }
    }

    private async Task TickPlayerAsync(MatchId matchId, PlayerSlot slot, IPlayerAgent agent, CancellationToken ct)
    {
        var ctx = new AgentCtx(matchId, slot);

        var opt = (await _mediator.Send(new GetPlayerOptionsQuery(matchId, slot), ct)).Value!;
        var unlocks = (await _mediator.Send(new GetUnlockableSpellsForPlayerQuery(matchId, slot), ct)).Value!;
        var board = (await _mediator.Send(new GetBoardStateForPlayerQuery(matchId, slot), ct)).Value!;

        // Evolution
        if (opt.Evolution is { RemainingPicks: > 0 })
            await agent.DecideEvolutionAsync(ctx, opt, ct);

        // Speed
        opt = (await _mediator.Send(new GetPlayerOptionsQuery(matchId, slot), ct)).Value!;
        if (opt.Speed is { Remaining: > 0 })
            await agent.DecideSpeedAsync(ctx, opt, ct);

        // Combat intent
        opt = (await _mediator.Send(new GetPlayerOptionsQuery(matchId, slot), ct)).Value!;
        if (opt.CombatPlanning is { MissingCreatureIds.Count: > 0 })
        {
            await agent.DecideCombatIntentAsync(ctx, opt, ct);
        }
    }

    private async Task TickRevealUntilNoneAsync(
        MatchId matchId,
        CancellationToken ct)
    {
        while (true)
        {
            var opt1 = (await _mediator.Send(new GetPlayerOptionsQuery(matchId, PlayerSlot.Player1), ct)).Value!;
            var opt2 = (await _mediator.Send(new GetPlayerOptionsQuery(matchId, PlayerSlot.Player2), ct)).Value!;

            var action = opt1.CombatAction ?? opt2.CombatAction;
            if (action is null || action.NextActorId is null || action.RemainingReveals <= 0)
                break;

            var actorId = action.NextActorId.Value;

            // Fetch boards to resolve ownership of the actor
            var boardP1 = (await _mediator.Send(new GetBoardStateForPlayerQuery(matchId, PlayerSlot.Player1), ct)).Value!;

            var ownerSlot = boardP1.Timeline!.Slots![boardP1.RevealCursor!.Index].PlayerSlot;

            var agent = ownerSlot == PlayerSlot.Player1 ? _p1 : _p2;
            var ctx = new AgentCtx(matchId, ownerSlot);
            var opt = (await _mediator.Send(new GetPlayerOptionsQuery(matchId, ownerSlot), ct)).Value!;
            await agent.DecideRevealForActorAsync(ctx,
                opt,
                actorId,
                ct);
        }
    }

    private async Task<CombatStepOutcomeView> ResolveRoundAsync(MatchId matchId, CancellationToken ct)
    {
        CombatStepOutcomeView last = null!;
        while (true)
        {
            var res = (await _mediator.Send(new ResolveNextCombatActionCommand(matchId), ct)).Value!;
            last = res.stepOutcome;
            if (last.IsRoundCompleted)
                return last;
        }
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
