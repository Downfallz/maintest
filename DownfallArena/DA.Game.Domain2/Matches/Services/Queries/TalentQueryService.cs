using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Services.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Services.Queries;

public sealed class TalentQueryService : ITalentQueryService
{
    private readonly ITalentUnlockService _unlockService;

    public TalentQueryService(ITalentUnlockService unlockService)
    {
        _unlockService = unlockService;
    }

    public Result<PlayerUnlockableSpells> GetUnlockableSpellsForPlayer(
        Match match,
        PlayerSlot slot,
        IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(match);

        var creatures = match.AllCreatures
            .Where(c => c.OwnerSlot == slot)
            .ToList();

        if (creatures.Count == 0)
            return Result<PlayerUnlockableSpells>.Fail("D7N0_NO_CREATURE_FOR_PLAYER");

        var views = new List<CreatureUnlockableSpells>(creatures.Count);

        foreach (var combatCreature in creatures)
        {
            // Ici tu utiliseras un snapshot enrichi quand tu y mettras KnownSpellIds
            var snapshot = CreatureSnapshot.From(combatCreature);

            var talentTree = resources.TalentTrees
                .FirstOrDefault(tt => tt.Id == snapshot.TalentTreeId);

            var unlockableResult = _unlockService.GetUnlockableSpells(snapshot, talentTree);

            if (!unlockableResult.IsSuccess)
                return Result<PlayerUnlockableSpells>.Fail(unlockableResult.Error!);

            views.Add(new CreatureUnlockableSpells(
                combatCreature.Id,
                unlockableResult.Value!));
        }

        return Result<PlayerUnlockableSpells>.Ok(
            new PlayerUnlockableSpells(slot, views));
    }
}