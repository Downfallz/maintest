using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Matches.Commands;

public sealed class CreateMatchHandler(MatchWorkflow workflow, IGameResources resources, IRandomSource random)
    : ICommandHandler<CreateMatch, Result<MatchId>>
{
    public async Task<Result<MatchId>> HandleAsync(CreateMatch command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var match = Match.Create(MatchId.New(), resources, command.RuleSet, random);
        await workflow.CommitAsync(match, cancellationToken);
        return Result.Success(match.Id);
    }
}
