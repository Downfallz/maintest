using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Application.Simulation.NewFolder;

public sealed record AgentCtx(MatchId MatchId, PlayerSlot Slot);
