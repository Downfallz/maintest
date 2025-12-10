using DA.Game.Application.Matches.DTOs;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.Commands.SubmitEvolutionChoice;

public sealed record GetUnlockableSpellsForPlayerQuery(MatchId MatchId, PlayerSlot slot) : IQuery<Result<PlayerUnlockableSpellsView>>;
