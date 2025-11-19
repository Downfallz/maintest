using DA.Game.Application.Matches.Features.CreateMatch.Notifications;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Matches.Resources;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Matches.Features.CreateMatch;

public sealed class CreateMatchHandler(IMatchRepository repo, 
    IApplicationEventCollector appEvents,
    IGameResources gameResources,
    IRuleSetProvider ruleSetProvider,
    IClock clock) : IRequestHandler<CreateMatchCommand, Result<Match>>
{
    public async Task<Result<Match>> Handle(CreateMatchCommand cmd, CancellationToken ct = default)
    {
        var rulebook = ruleSetProvider.Current;

        var match = Match.Create(gameResources, rulebook);
        var res = await repo.SaveAsync(match, ct);


        appEvents.Add(new MatchCreated(match.Id, clock.UtcNow));

        return res;
    }
}
