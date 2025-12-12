using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Queries;


public interface IPlayerOptionsQueryService
{
    Result<PlayerOptions> GetOptionsForPlayer(
        Match match,
        PlayerSlot slot,
        IGameResources resources);
}
