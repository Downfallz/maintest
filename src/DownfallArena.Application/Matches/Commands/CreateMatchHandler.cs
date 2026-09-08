using DownfallArena.Application.Messaging;
using DownfallArena.Application.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class CreateMatchHandler(MatchWorkflow workflow, IGameResources resources, IRandomSourceFactory random)
    : ICommandHandler<CreateMatch, Result<MatchId>>
{
    public async Task<Result<MatchId>> HandleAsync(CreateMatch command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var match = Match.Create(MatchId.New(), resources, command.RuleSet, random.Create(command.Seed));
        await workflow.CommitAsync(match, cancellationToken);
        return Result.Success(match.Id);
    }
}
