using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Services.Queries;

public sealed class BoardStateQueryService : IBoardStateQueryService
{
    public Result<PlayerBoardState> GetBoardStateForPlayer(
        Match match,
        PlayerSlot slot)
    {
        ArgumentNullException.ThrowIfNull(match);

        // Snapshot all creatures once
        var allSnapshots = match.AllCreatures
            .Select(CreatureSnapshot.From)
            .ToArray();

        var friendly = allSnapshots
            .Where(c => c.OwnerSlot == slot)
            .ToArray();

        if (friendly.Length == 0)
            return Result<PlayerBoardState>.Fail("D7B0_NO_CREATURE_FOR_PLAYER");

        var enemies = allSnapshots
            .Where(c => c.OwnerSlot != slot)
            .ToArray();

        var round = match.CurrentRound;

        static T Pick<T>(PlayerSlot s, T p1, T p2)
            => s == PlayerSlot.Player1 ? p1 : p2;

        var boardState = new PlayerBoardState
        {
            MatchId = match.Id,
            Slot = slot,
            MatchState = match.State,

            RoundPhase = round?.Phase,
            RoundSubPhase = round?.SubPhase,

            FriendlyCreatures = friendly,
            EnemyCreatures = enemies,

            EvolutionChoices = round is null
                ? Array.Empty<SpellUnlockChoice>()
                : Pick(
                    slot,
                    round.Player1EvolutionChoices,
                    round.Player2EvolutionChoices),

            SpeedChoices = round is null
                ? Array.Empty<SpeedChoice>()
                : Pick(
                    slot,
                    round.Player1SpeedChoices,
                    round.Player2SpeedChoices),

            CombatIntentsByCreature = round is null
                ? new Dictionary<CreatureId, CombatActionIntent>()
                : Pick(
                    slot,
                    round.Player1CombatIntentsByCreature,
                    round.Player2CombatIntentsByCreature),

            Timeline = round?.Timeline,
            RevealCursor = round?.RevealCursor,
            ResolveCursor = round?.ResolveCursor
        };

        return Result<PlayerBoardState>.Ok(boardState);
    }
}
