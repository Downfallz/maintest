using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Services.Queries;

public interface ITalentQueryService
{
    Result<PlayerUnlockableSpells> GetUnlockableSpellsForPlayer(
        Match match,
        PlayerSlot slot,
        IGameResources resources);
}
