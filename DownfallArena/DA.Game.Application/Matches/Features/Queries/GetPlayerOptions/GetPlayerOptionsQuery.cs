using DA.Game.Application.Matches.ReadModels;
using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.Queries.GetPlayerOptions;

public sealed record GetPlayerOptionsQuery(
    MatchId MatchId,
    PlayerSlot Slot) : IQuery<Result<PlayerOptionsView>>;
