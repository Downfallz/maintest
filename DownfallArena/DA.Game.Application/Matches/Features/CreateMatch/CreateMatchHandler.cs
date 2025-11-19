using DA.Game.Application.Matches.Features.CreateMatch.Notifications;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Application.Shared.RuleSets;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Resources;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.CreateMatch;

public sealed class CreateMatchHandler(IMatchRepository repo, 
    IApplicationEventCollector appEvents,
    IGameResources gameResources,
    IRuleSetProvider ruleSetProvider,
    IClock clock) : IRequestHandler<CreateMatchCommand, Result<MatchId>>
{
    public async Task<Result<MatchId>> Handle(CreateMatchCommand cmd, CancellationToken ct = default)
    {
        var rulebook = ruleSetProvider.Current;

        var match = Match.Create(gameResources, rulebook);
        var res = await repo.SaveAsync(match, ct);


        appEvents.Add(new MatchCreated(match.Id, clock.UtcNow));

        return Result<MatchId>.Ok(res.Value!.Id);
    }
}
