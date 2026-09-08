using DA.Game.Application.Matches.ReadModels;
using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.Features.Queries.get_;

public sealed record GetBoardStateForPlayerQuery(
    MatchId MatchId,
    PlayerSlot Slot
) : IQuery<Result<PlayerBoardStateView>>;