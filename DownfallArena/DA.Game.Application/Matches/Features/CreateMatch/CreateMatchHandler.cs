using DA.Game.Application.Matches.Features.CreateMatch.Notifications;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Application.Shared.RuleSets;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.CreateMatch;

public sealed class CreateMatchHandler(IMatchRepository repo, 
    IApplicationEventCollector appEvents,
    IGameResources gameResources,
    IRuleSetProvider ruleSetProvider,
    IClock clock) : IRequestHandler<CreateMatchCommand, Result<MatchId>>
{
    public async Task<Result<MatchId>> Handle(CreateMatchCommand cmd, CancellationToken cancellationToken)
    {
        var rulebook = ruleSetProvider.Current;

        var match = Match.Create(gameResources, rulebook, clock);
        var res = await repo.SaveAsync(match, cancellationToken);


        appEvents.Add(new MatchCreated(match.Id, clock.UtcNow));

        return Result<MatchId>.Ok(res.Value!.Id);
    }
}
