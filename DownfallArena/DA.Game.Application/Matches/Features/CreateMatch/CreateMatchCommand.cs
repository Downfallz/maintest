using DA.Game.Application.Shared.Primitives;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Matches.Features.CreateMatch;

public sealed record CreateMatchCommand() : ICommand<Result<Match>>;
